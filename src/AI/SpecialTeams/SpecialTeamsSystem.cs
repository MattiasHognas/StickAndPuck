using System.Numerics;
using Raylib_cs;
using StickAndPuck.Core;
using StickAndPuck.Players;
using StickAndPuck.Rules;
using static Raylib_cs.Raylib;

namespace StickAndPuck.AI;

/// <summary>
/// W22c: the AI's special teams. Runs after the defence (W22b) and before decisions (W6b), and
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
/// Icing is waved off for the short club (W7b).</item>
/// <item>Hockey IQ sets the odds: the kill reads the power play's shape at each zone entry, a smart
/// one picking the right answer, a fumbling one the wrong one; a smart skater finds his spot
/// exactly and clears at once, a poor one stands a little off and hesitates with the puck.</item>
/// </list>
/// SAP_ST_SPOTS=1 or the match debug setting draws the spots on the ice.
/// </summary>
internal sealed class SpecialTeamsSystem : ISystem
{
    const float PressFrom = 6.5f;       // a killer presses a carrier this close to his spot; further out he holds the shape
    const float Switch = 2.5f;          // a new pairing of skaters and spots has to save this many metres to replace the old
    const float OnSpot = 2.5f;          // within this of his spot a skater counts as holding the shape
    const float OpenRadius = 1.4f;      // how far from his spot a power play skater goes to get open
    const float ZoneMargin = 0.5f;      // how far past the blue line the puck has to be for the shapes
    const float BackFrom = 2f;          // a killer staying back stands at least this far into his own half
    const float SlotShot = 0.4f;        // a power play carrier with a lane shoots from a spot this good
    const float PerimeterShot = 0.12f;  // and from the point or the half wall on this share of his looks

    readonly Random rng;
    readonly bool on;

    public SpecialTeamsSystem()
#pragma warning disable S2245 // gameplay variation, not security
        : this(new Random())
#pragma warning restore S2245
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

    /// <summary>Gets or sets whether the spots are drawn on the ice (SAP_ST_SPOTS=1 sets it).</summary>
    public static bool ShowSpots { get; set; } = string.Equals(Environment.GetEnvironmentVariable("SAP_ST_SPOTS"), "1", StringComparison.Ordinal);

    public void Update(World world, float dt)
    {
        var board = SpecialTeams.Board(world);
        board.Spots[0].Clear();
        board.Spots[1].Clear();
        board.Shape = [Formation.None, Formation.None];
        if (!on || !Positioning.Drives(world) || world.Match.Phase is not (MatchPhase.Play or MatchPhase.Overtime)) return;
        var pk = world.Puck;
        TrackClear(world, board);
        if (Penalties.PowerPlay(world) is not { } pp)
        {
            board.PowerPlay = null;
            return;
        }
        if (!pk.Live) return;

        var kill = pp == Side.Home ? Side.Away : Side.Home;
        var ppTeam = Skaters(world, pp);
        var killTeam = Skaters(world, kill);
        if (ppTeam.Count <= killTeam.Count) return;
        if (board.PowerPlay != pp) Begin(board, pp);

        int end = PuckHandling.AttackEnd(world, pp);
        var puck = Flat(pk.Position);
        var carrier = pk.Carrier >= 0 && pk.Carrier < world.Players.Count ? world.Players[pk.Carrier] : null;
        KillPuck(world, board, carrier, killTeam, ppTeam);

        bool ppPuck = carrier != null ? carrier.Side == pp : pk.LastTouch >= 0 && pk.LastTouch < world.Players.Count && world.Players[pk.LastTouch].Side == pp;
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
    void KillPuck(World world, SpecialTeamsBoard board, Player? carrier, List<Player> killTeam, List<Player> ppTeam)
    {
        if (carrier is { IsGoalie: false } && killTeam.Count > 0 && carrier.Side == killTeam[0].Side)
        {
            Clear(world, board, carrier);
            StayBack(world, killTeam, carrier);
            return;
        }
        board.ClearCarrier = -1;
        if (carrier == null) NoChase(world, killTeam, ppTeam);
    }

    /// <summary>
    /// A man short, the kill does not join a rush: out of its own zone with the puck, one killer
    /// (the nearest) supports the carrier and the rest skate back to their defensive spots.
    /// </summary>
    static void StayBack(World world, List<Player> team, Player carrier)
    {
        int end = PuckHandling.AttackEnd(world, carrier.Side);
        var puck = Flat(world.Puck.Position);
        if (end * puck.X < -RinkSpec.BlueLineX) return;
        var support = team.Where(p => p != carrier).MinBy(p => Vector3.Distance(p.Position, carrier.Position));
        foreach (var p in team)
        {
            if (p == carrier || p == support || p.Slot >= 0 || Positioning.JobOf(world, p) == Job.Chase) continue;
            Take(world, p, Back(world, p, end, puck));
        }
    }

    /// <summary>
    /// A man short, the kill does not chase a cleared puck into the other end: its chaser goes only
    /// when he is clearly first to it, and otherwise skates back to his defensive spot.
    /// </summary>
    static void NoChase(World world, List<Player> killers, List<Player> powerPlay)
    {
        var puck = world.Puck.Position;
        int end = PuckHandling.AttackEnd(world, killers.Count > 0 ? killers[0].Side : Side.Home);
        if (end * puck.X <= 0) return;
        float theirs = powerPlay.Select(p => Vector3.Distance(p.Position, puck)).DefaultIfEmpty(99f).Min();
        foreach (var p in killers)
        {
            if (p.Slot >= 0 || Positioning.JobOf(world, p) != Job.Chase || Vector3.Distance(p.Position, puck) < theirs - 2f) continue;
            Take(world, p, Back(world, p, end, Flat(puck)));
        }
    }

    /// <summary>A killer's spot when he stays back: his defensive spot, but in his own half.</summary>
    static Vector2 Back(World world, Player p, int end, Vector2 puck)
    {
        var spot = TeamShape.Spot(Positioning.RoleOf(world, p), TeamPhase.Defend, end, puck);
        return end * spot.X > -BackFrom ? spot with { X = -end * BackFrom } : spot;
    }

    /// <summary>A new power play: it picks its shape (W22d's club tactics may pick it later).</summary>
    void Begin(SpecialTeamsBoard board, Side pp)
    {
        board.PowerPlay = pp;
        board.PpShape = rng.NextSingle() < 0.5f ? Formation.Umbrella : Formation.OneThreeOne;
        board.InZone = false;
        board.Tally[(int)pp].PowerPlays++;
    }

    public void DrawScene(World world, RenderContext render)
    {
        if (!ShowSpots && !world.Settings.MatchDebug) return;
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
        if (!ShowSpots && !world.Settings.MatchDebug) return;
        var board = SpecialTeams.Board(world);
        if (board.PowerPlay is not { } pp) return;
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

    /// <summary>The kill reads the power play's shape as the puck comes in: the right answer by the odds of its hockey IQ, the wrong one on a fumble.</summary>
    Formation ReadShape(Formation pp, List<Player> killers)
    {
        int iq = killers.Count == 0 ? 50 : (int)killers.Average(p => p.Data.Traits.HockeyIq);
        var (outcome, read) = Jitter.Roll(rng, iq);
        var right = Formations.Answer(pp);
        var wrong = right == Formation.Box ? Formation.Diamond : Formation.Box;
        if (outcome == ReadOutcome.Fumble) return wrong;
        return rng.NextSingle() < 0.35f + 0.6f * Traits.Unit(read) ? right : Formation.Box;
    }

    /// <summary>The power play's AI skaters take their spots; the carrier drifts toward his, leaving the play with the puck to decisions.</summary>
    void PowerPlay(World world, SpecialTeamsBoard board, Side side, List<Player> team, int end, Vector2 puck, Player? carrier, float dt)
    {
        var spots = Formations.Spots(board.PpShape, team.Count, end, puck);
        var pairs = Pair(world, board, side, team, spots);
        var tally = board.Tally[(int)side];
        tally.Seconds += dt;
        if (board.PpShape == Formation.Umbrella) tally.Umbrella += dt; else tally.OneThreeOne += dt;
        int held = 0;
        foreach (var (p, spot) in pairs)
        {
            if (Vector2.Distance(Flat(p.Position), spot) < OnSpot) held++;
            if (p.Slot >= 0 || Positioning.JobOf(world, p) == Job.Chase) continue;
            board.Spots[(int)side][p.Id] = spot;
            if (p == carrier)
            {
                // the carrier is positioning's and decisions': he skates and passes as usual, and shoots more readily
                Shoot(world, board, p, end);
                continue;
            }
            Take(world, p, carrier != null ? Open(world, board, p, carrier, spot) : Read(world, board, p, spot, 1.2f));
        }
        tally.OnSpot += dt * held / Math.Max(1, pairs.Count);
    }

    /// <summary>The kill holds its box or diamond; the killer on the carrier presses him while he is near his spot.</summary>
    void Kill(World world, SpecialTeamsBoard board, Side side, List<Player> team, int end, Vector2 puck, Player? carrier, float dt)
    {
        var spots = Formations.Spots(board.KillShape, team.Count, end, puck);
        var pairs = Pair(world, board, side, team, spots);
        var tally = board.Tally[(int)side];
        tally.KillSeconds += dt;
        if (board.KillShape == Formation.Box) tally.Box += dt; else tally.Diamond += dt;
        int held = 0;
        foreach (var (p, spot) in pairs)
        {
            board.Spots[(int)side][p.Id] = spot;
            bool presses = carrier != null && DefenceSystem.DutyOf(world, p) is Duty.Contain or Duty.Engage
                && Vector2.Distance(Flat(carrier.Position), spot) < PressFrom;
            if (presses || Vector2.Distance(Flat(p.Position), spot) < OnSpot) held++;
            if (presses || p.Slot >= 0 || Positioning.JobOf(world, p) == Job.Chase) continue;
            Take(world, p, Read(world, board, p, spot, 1.6f));
        }
        tally.KillOnSpot += dt * held / Math.Max(1, pairs.Count);
    }

    /// <summary>Pairs a club's skaters with the spots, keeping the old pairing unless a new one saves real distance.</summary>
    static List<(Player P, Vector2 Spot)> Pair(World world, SpecialTeamsBoard board, Side side, List<Player> team, IReadOnlyList<Vector2> spots)
    {
        var who = team.Select(p => (Flat(p.Position), Roles.IsDefence(Positioning.RoleOf(world, p)))).ToList();
        var (fresh, freshCost) = Formations.Assign(who, spots);
        var old = board.Pairing[(int)side];
        float oldCost = 0;
        bool valid = old.Count == Math.Min(team.Count, spots.Count) && team.Take(spots.Count).All(p => old.ContainsKey(p.Id))
            && old.Values.Distinct().Count() == old.Count && old.Values.All(s => s < spots.Count);
        if (valid)
            for (int i = 0; i < team.Count; i++)
                if (old.TryGetValue(team[i].Id, out int s)) oldCost += Formations.Cost(who[i], spots[s], s);
        if (!valid || freshCost < oldCost - Switch)
        {
            old.Clear();
            for (int i = 0; i < team.Count; i++)
                if (fresh[i] >= 0) old[team[i].Id] = fresh[i];
        }
        return [.. team.Where(p => old.ContainsKey(p.Id)).Select(p => (p, spots[old[p.Id]]))];
    }

    /// <summary>The spot as this skater reads it: a smart one finds it, a poor one stands a little off, re-read every few seconds.</summary>
    Vector2 Read(World world, SpecialTeamsBoard board, Player p, Vector2 spot, float off)
    {
        var plan = board.For(p.Id);
        if (world.Time >= plan.ErrorUntil)
        {
            float angle = rng.NextSingle() * MathF.Tau, size = (1f - p.Data.Traits.Iq) * off * MathF.Sqrt(rng.NextSingle());
            plan.Error = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * size;
            plan.ErrorUntil = world.Time + 3f + rng.NextSingle() * 3f;
        }
        return TeamShape.Inside(spot + plan.Error);
    }

    /// <summary>
    /// A power play shoots more than at even strength: the carrier looks every moment, and with a
    /// clear lane he lets a shot go from the slot, and now and then from the point or the half wall,
    /// a slap shot when he has time, for a tip or a rebound. Decisions hold the shot (W6b).
    /// </summary>
    void Shoot(World world, SpecialTeamsBoard board, Player p, int end)
    {
        var plan = board.For(p.Id);
        var brain = Decisions.Board(world).For(p.Id);
        if (p.Slot >= 0 || p.Action != PlayerAction.None || brain.ShootSteps > 0 || brain.LatePass != null || world.Time < plan.ShotLook) return;
        plan.ShotLook = world.Time + Choices.ThinkEvery(p.Data.Traits.HockeyIq) * (0.8f + 0.4f * rng.NextSingle());
        if (world.Time - brain.GotAt < 0.25f) return;
        var at = Flat(world.Puck.Position);
        var blockers = world.OnIce(p.Side == Side.Home ? Side.Away : Side.Home).Where(o => !o.IsGoalie).Select(o => Flat(o.Position)).ToList();
        float lane = Choices.ShotLane(end, at, blockers);
        float quality = Choices.ShotQuality(end, at);
        bool pressed = blockers.Any(b => Vector2.Distance(b, at) < 2.6f);
        var (outcome, _) = Jitter.Roll(world, p, rng);
        if (outcome == ReadOutcome.Fumble || lane < 0.85f) return;
        if (quality < SlotShot && !(quality > 0.05f && rng.NextSingle() < PerimeterShot)) return;

        var net = new Vector2(end * RinkSpec.GoalLineX, 0);
        float distance = Vector2.Distance(net, at);
        int side = at.Y > 0 ? -1 : 1;
        var aim = Vector2.Normalize(net - at + new Vector2(0, side * 0.6f));
        brain.ShootAim = aim;
        brain.ShootSteps = distance > 13f && !pressed ? 30 + rng.Next(20) : 2;
        brain.NextThink = Math.Max(brain.NextThink, world.Time + 0.2f);
        p.Command = p.Command with { Move = aim, Shoot = true, Sprint = false, Pass = false };
        board.Tally[(int)p.Side].Shots++;
    }

    /// <summary>
    /// With a teammate on the puck, an off-puck skater looks for the open ice around his spot, where
    /// a pass gets through (W22a's <see cref="GetOpen"/>), read again every moment, quicker for a smart one.
    /// </summary>
    Vector2 Open(World world, SpecialTeamsBoard board, Player p, Player carrier, Vector2 spot)
    {
        var plan = board.For(p.Id);
        bool stale = plan.Open is not { } open || Vector2.Distance(open, spot) > OpenRadius * 1.6f;
        if (!stale && world.Time < plan.OpenNext) return plan.Open!.Value;
        int iq = Jitter.Roll(world, p, rng).Iq;
        float minA = RinkSpec.BlueLineX + 0.8f, maxA = RinkSpec.HalfLength;
        plan.Open = GetOpen.Best(world, p, carrier, spot, OpenRadius, minA, maxA, iq, rng, plan.Open);
        plan.OpenNext = world.Time + Traits.Scale(p.Data.Traits.HockeyIq, 0.7f, 0.3f) * (0.8f + 0.4f * rng.NextSingle());
        return plan.Open.Value;
    }

    /// <summary>Takes a skater over from positioning and the defence, as the defence does, and skates him to his spot.</summary>
    static void Take(World world, Player p, Vector2 target)
    {
        var job = Positioning.Board(world).For(p.Id);
        job.Job = Job.Requested;
        job.Target = target;
        float far = Vector2.Distance(target, Flat(p.Position));
        p.Command = Steering.Toward(p, target, 1.6f, sprint: far > 6f);
    }

    /// <summary>
    /// A killer with the puck in his own half clears it: after a beat (shorter for a smart one,
    /// longer on a fumbled read) he fires it down the ice through the least crowded lane, and flips
    /// it high when every lane has a stick in it. Decisions hold the shot (W6b's slap shot hold).
    /// </summary>
    void Clear(World world, SpecialTeamsBoard board, Player p)
    {
        if (p.Slot >= 0) return;
        int end = PuckHandling.AttackEnd(world, p.Side);
        var at = Flat(world.Puck.Position);
        if (end * at.X > 0f) { board.ClearCarrier = -1; return; }   // over the red line: a short-handed rush is decisions' play
        var brain = Decisions.Board(world).For(p.Id);
        if (board.ClearCarrier != p.Id)
        {
            board.ClearCarrier = p.Id;
            var (outcome, iq) = Jitter.Roll(world, p, rng);
            float beat = Traits.Scale(iq, 0.5f, 0.12f);
            if (outcome == ReadOutcome.Fumble) beat += 0.5f + 0.5f * rng.NextSingle();
            board.ClearAt = world.Time + beat;
        }
        if (brain.ShootSteps > 0 || p.Action != PlayerAction.None) return;
        brain.NextThink = Math.Max(brain.NextThink, world.Time + 0.2f);   // no carry, pass or shot of decisions' own in the meantime
        brain.LatePass = null;
        if (world.Time < board.ClearAt) return;

        float side = at.Y >= 0 ? 1f : -1f;
        // up the boards on either side, never straight at the far net
        Vector2[] lanes = [new(end, 0.45f * side), new(end, 0.2f * side), new(end, -0.3f * side)];
        var opponents = world.OnIce(p.Side == Side.Home ? Side.Away : Side.Home).Where(o => !o.IsGoalie).Select(o => Flat(o.Position)).ToList();
        var best = lanes.Select(Vector2.Normalize).MinBy(d => Crowd(at, d, opponents) + MathF.Abs(MathF.Abs(d.Y) - 0.41f));
        bool flip = Crowd(at, best, opponents) > 0;
        brain.ShootAim = best;
        brain.ShootSteps = flip ? 20 : 4;   // a flip is a short slap wind-up, lifted (W35's held shot), a clear along the ice a tap
        p.Command = p.Command with { Move = best, Shoot = true, Sprint = false };
        board.Clearing = new ClearWatch(p.Side, p.Id, flip, world.Time);
        board.ClearCarrier = -1;
    }

    /// <summary>
    /// Counts a clear once the puck leaves the killer's stick (a wind-up broken by a check is no
    /// clear), and as out once it crosses the killers' blue line, going away, within three seconds.
    /// </summary>
    static void TrackClear(World world, SpecialTeamsBoard board)
    {
        if (board.Clearing is not { } c) return;
        var pk = world.Puck;
        var tally = board.Tally[(int)c.Side];
        if (c.Released < 0)
        {
            if (pk.Carrier == c.Player && world.Time - c.At < 1.5f) return;
            if (pk.Carrier >= 0 || pk.LastTouch != c.Player || !pk.Live) { board.Clearing = null; return; }
            tally.Clears++;
            if (c.Flip) tally.Flips++;
            board.Clearing = c with { Released = world.Time };
            return;
        }
        if (world.Time - c.Released > 3f) { board.Clearing = null; return; }
        if (PuckHandling.AttackEnd(world, c.Side) * pk.Position.X > -RinkSpec.BlueLineX)
        {
            tally.ClearedOut++;
            board.Clearing = null;
        }
    }

    /// <summary>How many opponents stand near a lane in its first 14 metres.</summary>
    static int Crowd(Vector2 from, Vector2 dir, List<Vector2> opponents)
    {
        int n = 0;
        foreach (var o in opponents)
        {
            float along = Vector2.Dot(o - from, dir);
            if (along is < 0.5f or > 14f) continue;
            if (Vector2.Distance(from + dir * along, o) < 1.8f) n++;
        }
        return n;
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
