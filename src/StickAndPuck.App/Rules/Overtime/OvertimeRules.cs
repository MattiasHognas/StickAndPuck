using System.Globalization;
using System.Numerics;
using StickAndPuck.App.Core;
using static Raylib_cs.Raylib;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Rules;

/// <summary>A tie after regulation goes to overtime and then a shootout as the mode's <see cref="OvertimeFormat"/>
/// says: SHL 3 on 3 then a shootout in the season, 5 on 5 until a goal in the playoffs.
/// <list type="bullet">
/// <item>Overtime: when the match flow hands over (<see cref="MatchPhase.Overtime"/>), each club keeps the
/// format's skaters (centre and two defence where it can, for 3 on 3), the rest go to the bench, and
/// after a short break <see cref="MatchFlow.StartPeriod"/> starts a sudden death period of
/// <see cref="PeriodLength"/>. A goal ends the match there; a scoreless period hands over to
/// the shootout, or to another overtime period when the format plays on until a goal.</item>
/// <item>Shootout: every skater leaves the ice and each goalie stays in their own net. Clubs take turns,
/// the rules' first club first (<see cref="ShootoutOrder"/>: SHL away, NHL home chooses), each shooter
/// alone from centre with the puck against the other club's goalie; the rules' shooters each, then sudden
/// death rounds opened by the same club. An attempt ends on a goal,
/// the goalie covering the puck, the puck going dead or behind the goal line, or time running out; no rebounds.
/// A human slot shoots for its club; otherwise the shooter skates in and shoots by itself.</item>
/// <item>The winner gets one goal added to the final score (as in the SHL) and MatchEnd is published.</item>
/// </list>
/// <see cref="Shootout"/> gives the tally for the HUD.</summary>
internal sealed partial class OvertimeRules : ISystem
{

    /// <summary>The dice seed without a match seed.</summary>
    internal const int Fallback = 9;

    readonly Random rng;

    public OvertimeRules()
        : this(Seeds.Dice(Seeds.Environment, "overtime", Fallback))
    {
    }

    /// <summary>Initializes the rules with their own randomness, so tests repeat.</summary>
    public OvertimeRules(Random rng)
    {
        this.rng = rng;
    }


    /// <summary>Gets the length of an overtime period in this match: the format's own, or <see cref="WorldSwitches.OvertimeSeconds"/>.</summary>
    public static float PeriodLength(World world)
    {
        return OvertimeFormat.Of(world.Setup).PeriodSeconds.Or(world.Switches.OvertimeSeconds);
    }

    /// <summary>Gets the shootout of a match, or None before it starts.</summary>
    public static Maybe<ShootoutState> Shootout(World world)
    {
        return world.Overtime.TryGet(out var s) ? s.Shootout : Maybe<ShootoutState>.None;
    }

    public void Update(World world, float dt)
    {
        if (!MatchFlow.IsRunning(world)) return;
        var s = world.Overtime.Get(() => new OvertimeState());
        switch (world.Match.Phase)
        {
            case MatchPhase.Overtime: Overtime(world, s, dt); break;
            case MatchPhase.Shootout: ShootoutStep(world, s, dt); break;
            default: break;
        }
    }

    public void DrawHud(World world, RenderContext render)
    {
        if (world.Overtime.TryGet(out var st) && st.Shootout.HasValue && !st.First.IsSet()) DrawChoice(world);

        // a plain shootout line under the match debug line
        if (!world.Switches.MatchLine || !Shootout(world).TryGet(out var so)) return;
        string Marks(Side side) => string.Concat(so.Shots.Where(x => x.Side == side).Select(x => x.Scored ? "o" : "x"));
        string text = $"SO  {world.Setup.Home.ShortName} {Marks(Side.Home)}   {world.Setup.Away.ShortName} {Marks(Side.Away)}";
        DrawRectangle(4, 236, MeasureText(text, 10) + 8, 14, new Raylib_cs.Color(24, 19, 31, 255));
        DrawText(text, 8, 238, 10, new Raylib_cs.Color(239, 224, 194, 255));
    }

    /// <summary>The home human's choice, blinking at the top until they pick.</summary>
    static void DrawChoice(World world)
    {
        string text = Text.Format(K.ShootoutChoice, world.Setup.Home.ShortName, Key(world, GameAction.Shoot), Key(world, GameAction.Pass));
        int w = MeasureText(text, 10);
        DrawRectangle((Game.Width - w) / 2 - 4, 48, w + 8, 14, new Raylib_cs.Color(24, 19, 31, 255));
        if ((int)(world.Time * 2) % 2 == 0) DrawText(text, (Game.Width - w) / 2, 50, 10, new Raylib_cs.Color(239, 224, 194, 255));
    }

    static string Key(World world, GameAction action)
    {
        if (ShootoutOrder.HomeHuman(world.Setup) is InputDevice.KeyboardMouse)
            return KeyBindings.Of(world.Settings, action).Key.ToString().ToUpperInvariant();

        string choice = action == GameAction.Shoot ? K.ChoiceShoot : K.ChoicePass;
        return Text.Get(choice);
    }

    /// <summary>The break after a tied period: the format's skaters each, then the overtime faceoff of the next period.</summary>
    static void Overtime(World world, OvertimeState s, float dt)
    {
        if (!s.InBreak)
        {
            s.InBreak = true;
            s.Timer = OvertimeTuning.BreakSeconds;
            world.Puck.Live = false;
            int skaters = OvertimeFormat.Of(world.Setup).Skaters;
            KeepSkaters(world, skaters);
            int number = world.Match.Period - world.Setup.Periods + 1;
            Log(world, $"overtime{(number > 1 ? string.Create(CultureInfo.InvariantCulture, $" {number}") : "")}, {skaters} on {skaters}");
        }

        s.Timer -= dt;
        if (s.Timer > 0) return;
        s.InBreak = false;
        MatchFlow.StartPeriod(world, world.Match.Period + 1, PeriodLength(world));
    }

    /// <summary>Sends all but <see cref="OvertimeTuning.OvertimeSkaters"/> skaters of each club to the bench, keeping a human's control on the ice.</summary>
    internal static void ThreeOnThree(World world)
    {
        KeepSkaters(world, OvertimeTuning.OvertimeSkaters);
    }

    /// <summary>Sends all but <paramref name="count"/> skaters of each club to the bench (centre and defence stay first), keeping a human's control on the ice.</summary>
    internal static void KeepSkaters(World world, int count)
    {
        foreach (var side in new[] { Side.Home, Side.Away })
        {
            var skaters = world.OnIce(side).Where(p => !p.IsGoalie).ToList();
            if (skaters.Count <= count) continue;
            var keep = new List<Player>();
            foreach (var pos in new[] { PlayingPosition.C, PlayingPosition.D, PlayingPosition.D })
                if (keep.Count < count && skaters.Find(p => p.Data.Position == pos && !keep.Contains(p)) is { } found) keep.Add(found);
            keep.AddRange(skaters.Where(p => !keep.Contains(p)).Take(count - keep.Count));

            foreach (var p in skaters.Where(p => !keep.Contains(p)))
            {
                if (p.Slot >= 0 && keep.Count > 0 && keep[0].Slot < 0) keep[0].Slot = p.Slot;
                Bench(p);
            }
        }
    }

    static void Bench(Player p)
    {
        p.OnIce = false;
        p.Slot = -1;
        p.Velocity = Vector3.Zero;
        p.Command = default;
        p.Begin(PlayerAction.None);
    }

    static bool Wait(OvertimeState s, float dt)
    {
        s.Timer -= dt;
        return s.Timer <= 0;
    }

    static void Log(World world, string what)
    {
        if (!MatchFlow.State(world).Log) return;
        var m = world.Match;
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"SAP: {what}: {world.Setup.Home.ShortName} {m.Score[0]}-{m.Score[1]} {world.Setup.Away.ShortName}"));
    }
}
