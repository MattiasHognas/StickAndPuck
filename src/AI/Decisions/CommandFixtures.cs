using System.Numerics;
using StickAndPuck.Core;
using StickAndPuck.Players;
using StickAndPuck.Rules;

namespace StickAndPuck.AI;

/// <summary>
/// W68: checks who writes an AI skater's command and which write wins (<see cref="AiCommand"/>),
/// run by aimatchtest. Each fixture plays real AI systems and looks at every AI write between
/// decisions and skating:
/// <list type="bullet">
/// <item>Weighing a pass touches nobody, and the pure receiver pick matches the old one.</item>
/// <item>A tactical dump beats an ordinary shot: while tactics has the carrier, decisions only hold its shot.</item>
/// <item>A kill clear beats decisions' own clear and pass the same way.</item>
/// <item>A defender the defence has containing never lines up a hit.</item>
/// <item>An AI skater meeting a pass never presses shoot or pass, so never arms a one-timer.</item>
/// <item>A human's command is never written by the AI, the stand-in faceoff included.</item>
/// </list>
/// </summary>
internal static class CommandFixtures
{
    /// <summary>Runs every fixture.</summary>
    /// <param name="clubs">The clubs; the first two are used.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="check">Prints and counts one check.</param>
    public static void Run(IReadOnlyList<ClubData> clubs, Settings settings, Action<bool, string> check)
    {
        PureReceiver(clubs, settings, check);
        DumpAndShot(clubs, settings, check);
        KillClear(clubs, settings, check);
        HumanUntouched(clubs, settings, check);
    }

    /// <summary>Every AI write of one step, per skater, in the order they were made.</summary>
    sealed class StepWrites
    {
        readonly Dictionary<int, List<AiIntent>> writes = [];

        public IEnumerable<(int Id, List<AiIntent> Writes)> All => writes.Select(kv => (kv.Key, kv.Value));

        public void Add(Player p, AiIntent intent)
        {
            if (!writes.TryGetValue(p.Id, out var list)) writes[p.Id] = list = [];
            list.Add(intent);
        }

        public void Clear()
        {
            foreach (var list in writes.Values) list.Clear();
        }
    }

    /// <summary>Plays steps, calling back with the step's writes once decisions have run and before anything acts on the commands.</summary>
    static void Play(World world, ISystem[] systems, float seconds, Action<StepWrites> afterDecisions, Action? beforeStep = null)
    {
        var step = new StepWrites();
        AiCommand.Watch = step.Add;
        try
        {
            int steps = (int)(seconds / Game.Step);
            for (int i = 0; i < steps && world.Match.Phase != MatchPhase.Final && world.Match.Period < 2; i++)
            {
                world.Events.Clear();
                step.Clear();
                beforeStep?.Invoke();
                foreach (var s in systems)
                {
                    s.Update(world, Game.Step);
                    if (s is DecisionSystem) afterDecisions(step);
                }
                world.Time += Game.Step;
                world.Tick++;
            }
        }
        finally
        {
            AiCommand.Watch = null;
        }
    }

    static World Match(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var setup = Scenes.DefaultSetup(clubs) with { PeriodSeconds = 20 * 60, Inputs = [new InputSlot(InputDevice.None, Side.Home), new InputSlot(InputDevice.None, Side.Away)] };
        var world = new World(setup, settings);
        Scenes.AddStarters(world);
        MatchFlow.Begin(world, log: false);
        return world;
    }

    /// <summary>Whether any write after the given layer's write with this reason came from anyone but decisions holding the shot.</summary>
    static bool OnlyHeldAfter(List<AiIntent> writes, AiLayer layer, string reason)
    {
        int at = writes.FindIndex(w => w.Owner == layer && string.Equals(w.Reason, reason, StringComparison.Ordinal));
        return at < 0 || writes.Skip(at + 1).All(w => w.Owner == AiLayer.Decisions && string.Equals(w.Reason, "shot held", StringComparison.Ordinal));
    }

    static bool Wrote(List<AiIntent> writes, AiLayer layer, string reason)
    {
        return writes.Exists(w => w.Owner == layer && string.Equals(w.Reason, reason, StringComparison.Ordinal));
    }

    /// <summary>
    /// Weighing passes mid-match leaves every player as it was, and the receiver picked for a
    /// stick direction is the one puck handling picks with the stick held that way (same scoring,
    /// same ties, a short stick reading as the way the body faces).
    /// </summary>
    static void PureReceiver(IReadOnlyList<ClubData> clubs, Settings settings, Action<bool, string> check)
    {
        var world = Match(clubs, settings);
        var systems = TacticsSelfTest.Systems(world, 41);
        int weighed = 0, touched = 0, picks = 0, differ = 0;
        long n = 0;
        Play(world, systems, 4 * 60, _ =>
        {
            if (n++ % 7 != 0) return;
            foreach (var p in world.Players.Where(o => o.OnIce && !o.IsGoalie))
            {
                for (int k = 0; k < 16; k++)
                {
                    float a = k * MathF.Tau / 16f;
                    var stick = new Vector2(MathF.Cos(a), MathF.Sin(a)) * (k % 4 == 3 ? 0.2f : 1f);
                    var keep = p.Command;
                    p.Command = keep with { Move = stick };
                    var old = PuckHandling.PickReceiver(world, p);
                    p.Command = keep;
                    picks++;
                    if (PuckHandling.PickReceiver(world, p, stick) != old) differ++;
                }
                if (world.Puck.Carrier != p.Id) continue;
                var before = Snapshot(world);
                var at = new Vector2(world.Puck.Position.X, world.Puck.Position.Z);
                var seen = world.Players.Where(o => o.OnIce && o.Side != p.Side).Select(o => (new Vector2(o.Position.X, o.Position.Z), 7f)).ToList();
                foreach (int smart in new[] { 20, 60, 95 })
                    DecisionSystem.BestPass(world, p, PuckHandling.AttackEnd(world, p.Side), at, seen, smart, cycling: false);
                weighed++;
                if (!before.SequenceEqual(Snapshot(world))) touched++;
            }
        });
        check(weighed >= 50 && touched == 0, $"weighing every pass leaves all player state untouched ({weighed} carriers weighed, {touched} changed)");
        check(picks >= 1000 && differ == 0, $"the pure receiver pick matches puck handling's for the same stick ({picks} picks, {differ} different)");
    }

    static List<(string, Vector3, Vector3, float, PlayerAction)> Snapshot(World world)
    {
        return [.. world.Players.Select(p => (Fields(p.Command), p.Position, p.Velocity, p.Heading, p.Action))];
    }

    /// <summary>Every field of a command, exactly, as one string to compare.</summary>
    static string Fields(PlayerCommand c)
    {
        return string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"{BitConverter.SingleToInt32Bits(c.Move.X)} {BitConverter.SingleToInt32Bits(c.Move.Y)} {c.Sprint} {c.Shoot} {c.Pass} {c.Check} {c.Deke} {c.SwitchPlayer} {c.ChangeLine} {c.Punch} {c.Block} {c.AcceptFight} {c.Pause}");
    }

    /// <summary>
    /// A 2-1-2 dump-and-chase club against a 1-3-1 trap club: while tactics takes the carrier wide or
    /// dumps the puck, decisions take no shot or pass of their own and only hold the dump; a
    /// defender the defence has containing never lines up a hit (one it lets engage may); and an
    /// AI skater meeting a pass never presses shoot or pass.
    /// </summary>
    static void DumpAndShot(IReadOnlyList<ClubData> clubs, Settings settings, Action<bool, string> check)
    {
        var press = new ClubTactics { Forecheck = Forecheck.Aggressive, Entry = ZoneEntry.DumpAndChase };
        var trap = new ClubTactics { Forecheck = Forecheck.Trap, Entry = ZoneEntry.Carry };
        var world = TacticsSelfTest.Make(clubs, settings, press, trap);
        var systems = TacticsSelfTest.Systems(world, 43);
        int dumps = 0, wide = 0, overDump = 0, contains = 0, containHits = 0, hits = 0, meets = 0, armed = 0;
        Play(world, systems, 20 * 60, step =>
        {
            foreach (var (id, writes) in step.All)
            {
                var p = world.Players[id];
                if (Wrote(writes, AiLayer.Tactics, "dump")) dumps++;
                if (Wrote(writes, AiLayer.Tactics, "dump: take it wide")) wide++;
                if (!OnlyHeldAfter(writes, AiLayer.Tactics, "dump") || !OnlyHeldAfter(writes, AiLayer.Tactics, "dump: take it wide")) overDump++;
                bool contain = Wrote(writes, AiLayer.Defence, "contain");
                bool hit = Wrote(writes, AiLayer.Decisions, "line up a hit");
                if (contain) contains++;
                if (hit) hits++;
                if (contain && hit) containHits++;
                if (!Wrote(writes, AiLayer.Decisions, "meet the pass")) continue;
                meets++;
                if (p.Command.Shoot || p.Command.Pass) armed++;
            }
        });
        check(dumps >= 5 && wide >= 20 && overDump == 0, $"a tactical dump beats an ordinary shot: decisions only hold it ({dumps} dumps, {wide} steps taken wide, {overDump} overwritten)");
        check(contains >= 200 && hits >= 1 && containHits == 0, $"a defender containing the carrier never lines up a hit, one engaging may ({contains} contain steps, {hits} hit steps, {containHits} both)");
        check(meets >= 50 && armed == 0, $"an AI skater meeting a pass never presses shoot or pass, so never arms a one-timer ({meets} steps, {armed} armed)");
    }

    /// <summary>A man short, a killer's clear is held by decisions and never replaced by decisions' own clear, shot or pass.</summary>
    static void KillClear(IReadOnlyList<ClubData> clubs, Settings settings, Action<bool, string> check)
    {
        int clears = 0, over = 0, ownClears = 0;
        foreach (var kill in new[] { Side.Away, Side.Home })
        {
            var world = SpecialTeamsSelfTest.ShortHanded(clubs, settings, kill);
            var systems = SpecialTeamsSelfTest.Systems(world, 45 + (int)kill, special: true, rules: true);
            Play(world, systems, 4 * 60, step =>
            {
                foreach (var (id, writes) in step.All)
                {
                    if (world.Players[id].Side != kill) continue;
                    if (Wrote(writes, AiLayer.SpecialTeams, "kill clear")) clears++;
                    if (!OnlyHeldAfter(writes, AiLayer.SpecialTeams, "kill clear")) over++;
                    if (Wrote(writes, AiLayer.Decisions, "clear")) ownClears++;
                }
            });
        }
        check(clears >= 4 && over == 0 && ownClears == 0, $"a kill clear is the special teams' own: decisions only hold it and never clear for the kill ({clears} clears, {over} overwritten, {ownClears} of decisions')");
    }

    /// <summary>A human skater in an all-AI match keeps exactly the command his input gave, every step, and through the stand-in faceoff.</summary>
    static void HumanUntouched(IReadOnlyList<ClubData> clubs, Settings settings, Action<bool, string> check)
    {
        var world = Match(clubs, settings);
        var human = world.OnIce(Side.Home).First(p => !p.IsGoalie);
        human.Slot = 0;
        var input = new PlayerCommand { Move = new Vector2(0.3f, -0.2f), Sprint = true };
        string given = Fields(input);
        var systems = TacticsSelfTest.Systems(world, 47);
        int steps = 0, overwritten = 0, written = 0;
        Play(world, systems, 3 * 60, step =>
        {
            steps++;
            if (!string.Equals(Fields(human.Command), given, StringComparison.Ordinal)) overwritten++;
            if (step.All.Any(w => w.Id == human.Id && w.Writes.Count > 0)) written++;
        }, () => human.Command = input);
        human.Command = input;
        StandInPlay.Faceoff(world, Vector3.Zero);
        bool faceoff = string.Equals(Fields(human.Command), given, StringComparison.Ordinal);
        check(steps > 1000 && overwritten == 0 && written == 0 && faceoff, $"the AI never writes a human's command, the stand-in faceoff included ({steps} steps, {overwritten} overwritten, {written} written)");
    }
}
