using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players;
using StickAndPuck.App.Rules;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.AI;

/// <summary>The AI's special teams. Runs after the defence and before decisions, and
/// only while one club has a man advantage from penalties (<see cref="Penalties.PowerPlay"/>).
/// <list type="bullet">
/// <item>Power play: once the puck is in the zone with the power play on it, its AI skaters take
/// the spots of an umbrella or a 1-3-1 (<see cref="Formations"/>), picked at the start of each
/// power play. Whoever is nearest a spot takes it, so the shape turns over as the puck moves; the
/// carrier drifts toward his spot while decisions pass, shoot or carry as usual.</item>
/// <item>Penalty kill: the short club holds a box, or a diamond against a 1-3-1, sliding and
/// stepping toward the puck. The killer nearest the carrier still presses him (the defence's
/// presser) while the carrier is near his spot; one further out is left to the shape.</item>
/// <item>Clear: a killer who gets the puck in his own half fires it down the ice, along the boards
/// or through the middle, whichever is less crowded, and flips it over sticks when every lane is.
/// Icing is waved off for the short club.</item>
/// <item>Hockey IQ sets the odds: the kill reads the power play's shape at each zone entry, a smart
/// one picking the right answer, a fumbling one the wrong one; a smart skater finds his spot
/// exactly and clears at once, a poor one stands a little off and hesitates with the puck.</item>
/// </list>
/// SAP_ST_SPOTS=1 or the match debug setting draws the spots on the ice.</summary>
internal sealed partial class SpecialTeamsSystem : ISystem
{
    const float PressFrom = 6.5f;       // a killer presses a carrier this close to his spot; further out he holds the shape
    const float Switch = 2.5f;          // a new pairing of skaters and spots has to save this many metres to replace the old
    const float OnSpot = 2.5f;          // within this of his spot a skater counts as holding the shape
    const float OpenRadius = 1.4f;      // how far from his spot a power play skater goes to get open
    const float ZoneMargin = 0.5f;      // how far past the blue line the puck has to be for the shapes
    const float BackFrom = 2f;          // a killer staying back stands at least this far into his own half
    const float SlotShot = 0.4f;        // a power play carrier with a lane shoots from a spot this good
    const float PerimeterShot = 0.12f;  // and from the point or the half wall on this share of his looks

    /// <summary>The dice seed without a match seed.</summary>
    internal const int Fallback = 3;

    readonly Random rng;
    readonly bool on;

    public SpecialTeamsSystem()
        : this(Seeds.Dice(Seeds.Environment, "specialteams", Fallback))
    {
    }

    /// <summary>Initializes the system with its own randomness, so tests repeat.</summary>
    /// <param name="rng">Randomness for shape choice, reads and clears.</param>
    /// <param name="on">False leaves special teams to the even-strength AI, for tests that compare.</param>
    public SpecialTeamsSystem(Random rng, bool on = true)
    {
        this.rng = rng;
        this.on = on;
    }


    public void Update(World world, float dt)
    {
        var board = SpecialTeams.Board(world);
        board.Spots[0].Clear();
        board.Spots[1].Clear();
        board.Shape = [Formation.None, Formation.None];
        if (!on || !Positioning.Drives(world) || world.Match.Phase is not (MatchPhase.Play or MatchPhase.Overtime)) return;
        var pk = world.Puck;
        TrackClear(world, board);
        if (!Penalties.PowerPlay(world).TryGetSide(out var pp))
        {
            board.PowerPlay = SideOrNone.None;
            return;
        }
        if (!pk.Live) return;

        var kill = pp == Side.Home ? Side.Away : Side.Home;
        var ppTeam = Skaters(world, pp);
        var killTeam = Skaters(world, kill);
        if (ppTeam.Count <= killTeam.Count) return;
        if (!board.PowerPlay.Is(pp)) Begin(board, pp);

        int end = PuckHandling.AttackEnd(world, pp);
        var puck = Flat(pk.Position);
        var carrier = pk.Carrier >= 0 && pk.Carrier < world.Players.Count ? world.Players[pk.Carrier] : Maybe<Player>.None;
        KillPuck(world, board, carrier, killTeam, ppTeam);

        bool ppPuck = carrier.TryGet(out var holder) ? holder.Side == pp : pk.LastTouch >= 0 && pk.LastTouch < world.Players.Count && world.Players[pk.LastTouch].Side == pp;
        // only with the puck over the blue line, so nobody skates into the zone ahead of it (offside)
        bool inZone = Formations.InZone(end, puck, ZoneMargin);
        if (!inZone || !ppPuck)
        {
            if (!inZone) board.InZone = false;
            return;
        }
        if (!board.InZone)
        {
            board.InZone = true;
            board.KillShape = ReadShape(board.PpShape, killTeam);
            board.Tally[(int)pp].Entries++;
        }

        board.Shape[(int)pp] = board.PpShape;
        board.Shape[(int)kill] = board.KillShape;
        PowerPlay(world, board, pp, ppTeam, end, puck, carrier, dt);
        Kill(world, board, kill, killTeam, end, puck, carrier, dt);
    }

    /// <summary>A killer on the puck clears it and the rest stay back; with the puck loose, the kill does not chase it into the other end.</summary>
    void KillPuck(World world, SpecialTeamsBoard board, Maybe<Player> carrier, List<Player> killTeam, List<Player> ppTeam)
    {
        if (carrier.TryGet(out var killer) && !killer.IsGoalie && killTeam.Count > 0 && killer.Side == killTeam[0].Side)
        {
            Clear(world, board, killer);
            StayBack(world, killTeam, killer);
            return;
        }
        board.ClearCarrier = -1;
        if (!carrier.HasValue) NoChase(world, killTeam, ppTeam);
    }

    /// <summary>A man short, the kill does not join a rush: out of its own zone with the puck, one killer
    /// (the nearest) supports the carrier and the rest skate back to their defensive spots.</summary>
    static void StayBack(World world, List<Player> team, Player carrier)
    {
        int end = PuckHandling.AttackEnd(world, carrier.Side);
        var puck = Flat(world.Puck.Position);
        if (end * puck.X < -RinkSpec.BlueLineX) return;
        var support = team.Where(p => p != carrier).MinBy(p => Vector3.Distance(p.Position, carrier.Position));
        foreach (var p in team)
        {
            if (p == carrier || p == support || p.Slot >= 0 || Positioning.JobOf(world, p) == Job.Chase) continue;
            Take(world, p, Back(world, p, end, puck), "kill stays back");
        }
    }

    /// <summary>A man short, the kill does not chase a cleared puck into the other end: its chaser goes only
    /// when he is clearly first to it, and otherwise skates back to his defensive spot.</summary>
    static void NoChase(World world, List<Player> killers, List<Player> powerPlay)
    {
        var puck = world.Puck.Position;
        int end = PuckHandling.AttackEnd(world, killers.Count > 0 ? killers[0].Side : Side.Home);
        if (end * puck.X <= 0) return;
        float theirs = powerPlay.Select(p => Vector3.Distance(p.Position, puck)).DefaultIfEmpty(99f).Min();
        foreach (var p in killers)
        {
            if (p.Slot >= 0 || Positioning.JobOf(world, p) != Job.Chase || Vector3.Distance(p.Position, puck) < theirs - 2f) continue;
            Take(world, p, Back(world, p, end, Flat(puck)), "kill does not chase");
        }
    }

    /// <summary>A killer's spot when he stays back: his defensive spot, but in his own half.</summary>
    static Vector2 Back(World world, Player p, int end, Vector2 puck)
    {
        var spot = TeamShape.Spot(Positioning.RoleOf(world, p), TeamPhase.Defend, end, puck);
        return end * spot.X > -BackFrom ? spot with { X = -end * BackFrom } : spot;
    }

    /// <summary>A new power play: it picks its shape (club tactics may pick it later).</summary>
    void Begin(SpecialTeamsBoard board, Side pp)
    {
        board.PowerPlay = pp.OrNone();
        board.PpShape = rng.NextSingle() < 0.5f ? Formation.Umbrella : Formation.OneThreeOne;
        board.InZone = false;
        board.Tally[(int)pp].PowerPlays++;
    }

    public void DrawScene(World world, RenderContext render)
    {
        if (!world.Switches.SpecialTeamsSpots && !world.Settings.MatchDebug) return;
        var board = SpecialTeams.Board(world);
        foreach (var side in new[] { Side.Home, Side.Away })
        {
            var colour = side == Side.Home ? new Color(0xef, 0xe0, 0xc2, 255) : new Color(0xc8, 0x57, 0x4a, 255);
            foreach (var spot in board.Spots[(int)side].Values)
                DrawCube(new Vector3(spot.X, 0.05f, spot.Y), 0.45f, 0.08f, 0.45f, colour);
        }
    }

    public void DrawHud(World world, RenderContext render)
    {
        if (!world.Switches.SpecialTeamsSpots && !world.Settings.MatchDebug) return;
        var board = SpecialTeams.Board(world);
        if (!board.PowerPlay.TryGetSide(out var pp)) return;
        var kill = pp == Side.Home ? Side.Away : Side.Home;
        string text = $"PP {world.Setup.Club(pp).ShortName} {Name(board.PpShape)}   KILL {Name(board.KillShape)}   CLEARS {board.Tally[(int)kill].Clears}";
        DrawText(text, 6, 252, 10, new Color(0xef, 0xe0, 0xc2, 255));
    }

    static string Name(Formation f)
    {
        return f switch
        {
            Formation.Umbrella => "UMBRELLA",
            Formation.OneThreeOne => "1-3-1",
            Formation.Box => "BOX",
            Formation.Diamond => "DIAMOND",
            _ => "-",
        };
    }

    static List<Player> Skaters(World world, Side side)
    {
        return [.. world.OnIce(side).Where(p => !p.IsGoalie && p.Action is not (PlayerAction.Fallen or PlayerAction.Fight)).OrderBy(p => p.Id)];
    }

    static Vector2 Flat(Vector3 v)
    {
        return new Vector2(v.X, v.Z);
    }
}
