using System.Globalization;
using System.Numerics;
using StickAndPuck.App.AI;
using StickAndPuck.App.AI.Decisions;
using StickAndPuck.App.AI.Positioning.Dev;
using StickAndPuck.App.AI.Tactics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players.Puck;
using StickAndPuck.Tests.AI.Tactics;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.AI.Decisions
{
    /// <summary>
    /// Checks who writes an AI skater's command and which write wins (<see cref="AiCommand"/>),
    /// run by <see cref="DecisionsTests"/>. Each fixture plays real AI systems and looks at every AI write between
    /// decisions and skating:
    /// <list type="bullet">
    /// <item>Weighing a pass touches nobody, and the pure receiver pick matches the old one.</item>
    /// <item>A tactical dump beats an ordinary shot: while tactics has the carrier, decisions only hold its shot.</item>
    /// <item>A kill clear beats decisions' own clear and pass the same way.</item>
    /// <item>A defender the defence has containing never lines up a hit.</item>
    /// <item>An AI skater meeting a pass never presses pass, and presses shoot only to one-time it.</item>
    /// <item>A human's command is never written by the AI, the stand-in faceoff included.</item>
    /// </list>
    /// </summary>
    internal static class CommandFixtures
    {
        private static readonly int[] PassingIqLevels = [20, 60, 95];

        /// <summary>Every AI write of one step, per skater, in the order they were made.</summary>
        private sealed class StepWrites
        {
            private readonly Dictionary<int, List<AiIntent>> writes = [];

            public IEnumerable<(int Id, List<AiIntent> Writes)> All =>
                writes.Select(kv => (kv.Key, kv.Value));

            public void Add(Player p, AiIntent intent)
            {
                if (!writes.TryGetValue(p.Id, out List<AiIntent> list))
                {
                    writes[p.Id] = list = [];
                }

                list.Add(intent);
            }

            public void Clear()
            {
                foreach (List<AiIntent> list in writes.Values)
                {
                    list.Clear();
                }
            }
        }

        /// <summary>Plays steps, calling back with the step's writes once decisions have run and before anything acts on the commands.</summary>
        private static void Play(
            World world,
            ISystem[] systems,
            float seconds,
            Action<StepWrites> afterDecisions,
            Maybe<Action> beforeStep = default
        )
        {
            StepWrites step = new();
            world.Switches.AiWatch = step.Add;
            try
            {
                int steps = (int)(seconds / Game.Step);
                for (
                    int i = 0;
                    i < steps && world.Match.Phase != MatchPhase.Final && world.Match.Period < 2;
                    i++
                )
                {
                    world.Events.Clear();
                    step.Clear();
                    if (beforeStep.TryGet(out Action before))
                    {
                        before();
                    }

                    foreach (ISystem s in systems)
                    {
                        s.Update(world, Game.Step);
                        if (s is DecisionSystem)
                        {
                            afterDecisions(step);
                        }
                    }
                    world.Time += Game.Step;
                    world.Tick++;
                }
            }
            finally
            {
                world.Switches.AiWatch = new WorldSwitches().AiWatch;
            }
        }

        private static World Match(IReadOnlyList<ClubData> clubs, Settings settings)
        {
            MatchSetup setup = Scenes.DefaultSetup(clubs) with
            {
                PeriodSeconds = 20 * 60,
                Inputs =
                [
                    new InputSlot(InputDevice.None, Side.Home),
                    new InputSlot(InputDevice.None, Side.Away),
                ],
            };
            return new WorldBuilder().Setup(setup).Settings(settings).Build();
        }

        /// <summary>Whether any write after the given layer's write with this reason came from anyone but decisions holding the shot.</summary>
        private static bool OnlyHeldAfter(List<AiIntent> writes, AiLayer layer, string reason)
        {
            int at = writes.FindIndex(w =>
                w.Owner == layer && string.Equals(w.Reason, reason, StringComparison.Ordinal)
            );
            return at < 0
                || writes
                    .Skip(at + 1)
                    .All(w =>
                        w.Owner == AiLayer.Decisions
                        && string.Equals(w.Reason, "shot held", StringComparison.Ordinal)
                    );
        }

        private static bool Wrote(List<AiIntent> writes, AiLayer layer, string reason)
        {
            return writes.Exists(w =>
                w.Owner == layer && string.Equals(w.Reason, reason, StringComparison.Ordinal)
            );
        }

        /// <summary>
        /// Weighing passes mid-match leaves every player as it was, and the receiver picked for a
        /// stick direction is the one puck handling picks with the stick held that way (same scoring,
        /// same ties, a short stick reading as the way the body faces).
        /// </summary>
        internal static void PureReceiver(
            IReadOnlyList<ClubData> clubs,
            Settings settings,
            Action<bool, string> check
        )
        {
            World world = Match(clubs, settings);
            ISystem[] systems = TacticsTests.Systems(world, 41);
            int weighed = 0,
                touched = 0,
                picks = 0,
                differ = 0;
            long n = 0;
            Play(
                world,
                systems,
                4 * 60,
                writes =>
                {
                    if (n++ % 7 != 0)
                    {
                        return;
                    }

                    foreach (Player p in world.Players.Where(o => o.OnIce && !o.IsGoalie))
                    {
                        for (int k = 0; k < 16; k++)
                        {
                            float a = k * MathF.Tau / 16f;
                            Vector2 stick =
                                new Vector2(SimMath.Cos(a), SimMath.Sin(a))
                                * (k % 4 == 3 ? 0.2f : 1f);
                            PlayerCommand keep = p.Command;
                            p.Command = keep with { Move = stick };
                            Maybe<Player> old = PuckHandling.PickReceiver(world, p);
                            p.Command = keep;
                            picks++;
                            if (PuckHandling.PickReceiver(world, p, stick) != old)
                            {
                                differ++;
                            }
                        }
                        (weighed, touched) = VerifyReceiverObservations(world, weighed, touched, p);
                    }
                }
            );
            check(
                weighed >= 50 && touched == 0,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"weighing every pass leaves all player state untouched ({weighed} carriers weighed, {touched} changed)"
                )
            );
            check(
                picks >= 1000 && differ == 0,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the pure receiver pick matches puck handling's for the same stick ({picks} picks, {differ} different)"
                )
            );
        }

        private static (int weighed, int touched) VerifyReceiverObservations(
            World world,
            int weighed,
            int touched,
            Player p
        )
        {
            if (world.Puck.Carrier != p.Id)
            {
                return (weighed, touched);
            }

            List<(string, Vector3, Vector3, float, PlayerAction)> before = Snapshot(world);
            Vector2 at = new(world.Puck.Position.X, world.Puck.Position.Z);
            List<(Vector2, float)> seen =
            [
                .. world
                    .Players.Where(o => o.OnIce && o.Side != p.Side)
                    .Select(o => (new Vector2(o.Position.X, o.Position.Z), 7f)),
            ];
            foreach (int smart in PassingIqLevels)
            {
                _ = DecisionSystem.BestPass(
                    world,
                    p,
                    PuckHandling.AttackEnd(world, p.Side),
                    at,
                    seen,
                    smart,
                    cycling: false
                );
            }

            weighed++;
            if (!before.SequenceEqual(Snapshot(world)))
            {
                touched++;
            }

            return (weighed, touched);
        }

        private static List<(string, Vector3, Vector3, float, PlayerAction)> Snapshot(World world)
        {
            return
            [
                .. world.Players.Select(p =>
                    (Fields(p.Command), p.Position, p.Velocity, p.Heading, p.Action)
                ),
            ];
        }

        /// <summary>Every field of a command, exactly, as one string to compare.</summary>
        private static string Fields(PlayerCommand c)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{BitConverter.SingleToInt32Bits(c.Move.X)} {BitConverter.SingleToInt32Bits(c.Move.Y)} {c.Sprint} {c.Shoot} {c.Pass} {c.Check} {c.Deke} {c.SwitchPlayer} {c.ChangeLine} {c.Punch} {c.Block} {c.AcceptFight} {c.Pause}"
            );
        }

        /// <summary>
        /// A 2-1-2 dump-and-chase club against a 1-3-1 trap club: while tactics takes the carrier wide or
        /// dumps the puck, decisions take no shot or pass of their own and only hold the dump; a
        /// defender the defence has containing never lines up a hit (one it lets engage may); and an
        /// AI skater meeting a pass never presses pass, and presses shoot only to one-time it.
        /// </summary>
        internal static void DumpAndShot(
            IReadOnlyList<ClubData> clubs,
            Settings settings,
            Action<bool, string> check
        )
        {
            ClubTactics press = new()
            {
                Forecheck = Forecheck.Aggressive,
                Entry = ZoneEntry.DumpAndChase,
            };
            ClubTactics trap = new() { Forecheck = Forecheck.Trap, Entry = ZoneEntry.Carry };
            World world = TacticsTests.Make(clubs, settings, press, trap);
            ISystem[] systems = TacticsTests.Systems(world, 43);
            int dumps = 0,
                wide = 0,
                overDump = 0,
                contains = 0,
                containHits = 0,
                hits = 0,
                meets = 0,
                armed = 0,
                oneTimers = 0;
            Play(
                world,
                systems,
                20 * 60,
                step =>
                {
                    foreach ((int id, List<AiIntent> writes) in step.All)
                    {
                        Player p = world.Players[id];
                        dumps += Wrote(writes, AiLayer.Tactics, "dump") ? 1 : 0;

                        if (Wrote(writes, AiLayer.Tactics, "dump: take it wide"))
                        {
                            wide++;
                        }

                        if (!DumpCommandsStayedHeld(writes))
                        {
                            overDump++;
                        }

                        bool contain = Wrote(writes, AiLayer.Defence, "contain");
                        bool hit = Wrote(writes, AiLayer.Decisions, "line up a hit");
                        if (contain)
                        {
                            contains++;
                        }

                        if (hit)
                        {
                            hits++;
                        }

                        if (contain && hit)
                        {
                            containHits++;
                        }

                        if (!Wrote(writes, AiLayer.Decisions, "meet the pass"))
                        {
                            continue;
                        }

                        meets++;
                        bool oneTiming = Wrote(writes, AiLayer.Decisions, "one-timer");
                        if (oneTiming)
                        {
                            oneTimers++;
                        }

                        if (p.Command.Pass || (p.Command.Shoot && !oneTiming))
                        {
                            armed++;
                        }
                    }
                }
            );
            VerifyDumpShotCommands(
                check,
                dumps,
                wide,
                overDump,
                contains,
                containHits,
                hits,
                meets,
                armed,
                oneTimers
            );
        }

        private static bool DumpCommandsStayedHeld(List<AiIntent> writes)
        {
            return OnlyHeldAfter(writes, AiLayer.Tactics, "dump")
                && OnlyHeldAfter(writes, AiLayer.Tactics, "dump: take it wide");
        }

        private static void VerifyDumpShotCommands(
            Action<bool, string> check,
            int dumps,
            int wide,
            int overDump,
            int contains,
            int containHits,
            int hits,
            int meets,
            int armed,
            int oneTimers
        )
        {
            check(
                dumps >= 5 && wide >= 20 && overDump == 0,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"a tactical dump beats an ordinary shot: decisions only hold it ({dumps} dumps, {wide} steps taken wide, {overDump} overwritten)"
                )
            );
            check(
                contains >= 200 && hits >= 1 && containHits == 0,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"a defender containing the carrier never lines up a hit, one engaging may ({contains} contain steps, {hits} hit steps, {containHits} both)"
                )
            );
            check(
                meets >= 50 && armed == 0,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"an AI skater meeting a pass never presses pass, and shoot only to one-time it ({meets} steps, {oneTimers} one-timing, {armed} otherwise)"
                )
            );
        }

        /// <summary>A man short, a killer's clear is held by decisions and never replaced by decisions' own clear, shot or pass.</summary>
        internal static void KillClear(
            IReadOnlyList<ClubData> clubs,
            Settings settings,
            Action<bool, string> check
        )
        {
            int clears = 0,
                over = 0,
                ownClears = 0;
            foreach (Side kill in new[] { Side.Away, Side.Home })
            {
                World world = App.AI.SpecialTeams.SpecialTeamsSetup.ShortHanded(
                    clubs,
                    settings,
                    kill
                );
                ISystem[] systems = App.AI.SpecialTeams.SpecialTeamsSetup.Systems(
                    world,
                    45 + (int)kill,
                    special: true,
                    rules: true
                );
                Play(
                    world,
                    systems,
                    4 * 60,
                    step =>
                    {
                        foreach ((int id, List<AiIntent> writes) in step.All)
                        {
                            if (world.Players[id].Side != kill)
                            {
                                continue;
                            }

                            if (Wrote(writes, AiLayer.SpecialTeams, "kill clear"))
                            {
                                clears++;
                            }

                            if (!OnlyHeldAfter(writes, AiLayer.SpecialTeams, "kill clear"))
                            {
                                over++;
                            }

                            if (Wrote(writes, AiLayer.Decisions, "clear"))
                            {
                                ownClears++;
                            }
                        }
                    }
                );
            }
            check(
                clears >= 4 && over == 0 && ownClears == 0,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"a kill clear is the special teams' own: decisions only hold it and never clear for the kill ({clears} clears, {over} overwritten, {ownClears} of decisions')"
                )
            );
        }

        /// <summary>A human skater in an all-AI match keeps exactly the command his input gave, every step, and through the stand-in faceoff.</summary>
        internal static void HumanUntouched(
            IReadOnlyList<ClubData> clubs,
            Settings settings,
            Action<bool, string> check
        )
        {
            World world = Match(clubs, settings);
            Player human = world.OnIce(Side.Home).First(p => !p.IsGoalie);
            human.Slot = 0;
            PlayerCommand input = new() { Move = new Vector2(0.3f, -0.2f), Sprint = true };
            string given = Fields(input);
            ISystem[] systems = TacticsTests.Systems(world, 47);
            int steps = 0,
                overwritten = 0,
                written = 0;
            Play(
                world,
                systems,
                3 * 60,
                step =>
                {
                    steps++;
                    if (!string.Equals(Fields(human.Command), given, StringComparison.Ordinal))
                    {
                        overwritten++;
                    }

                    if (step.All.Any(w => w.Id == human.Id && w.Writes.Count > 0))
                    {
                        written++;
                    }
                },
                Maybe.Some<Action>(() => human.Command = input)
            );
            human.Command = input;
            StandInPlay.Faceoff(world, Vector3.Zero);
            bool faceoff = string.Equals(Fields(human.Command), given, StringComparison.Ordinal);
            check(
                steps > 1000 && overwritten == 0 && written == 0 && faceoff,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the AI never writes a human's command, the stand-in faceoff included ({steps} steps, {overwritten} overwritten, {written} written)"
                )
            );
        }
    }
}
