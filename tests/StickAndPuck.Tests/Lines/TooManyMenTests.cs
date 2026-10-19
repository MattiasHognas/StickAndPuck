using System.Globalization;
using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Lines;
using StickAndPuck.App.Players.Movement;
using StickAndPuck.App.Rules.Penalties;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Lines
{
    /// <summary>
    /// Too many men on the ice: a human's second change line press rushes his change on the fly, the fresh
    /// skaters stepping out at once while the tired ones still skate for the door; playing the puck while one
    /// going off is still out draws a bench minor under SHL and NHL rules (lenient: a few metres from the
    /// door; strict: five feet), never in Arcade and never for the AI.
    /// </summary>
    public sealed class TooManyMenTests : CheckedTest
    {
        private static World Make(RuleSet rules, bool human = true)
        {
            WorldBuilder builder = new WorldBuilder()
                .Rules(rules)
                .Then(w => new LinesSystem().Start(w));
            if (human)
            {
                _ = builder.Human();
            }

            World world = builder.Build();
            world.Match.Phase = MatchPhase.Play;
            world.Puck.Live = true;
            world.Puck.Carrier = -1;
            world.Puck.Position = Vector3.Zero;
            return world;
        }

        /// <summary>Runs the given systems for a number of steps, the line button released after each; returns the events.</summary>
        private static List<MatchEvent> Steps(World world, int steps, params ISystem[] systems)
        {
            ISystem[] run = systems.Length > 0 ? systems : [new LinesSystem()];
            List<MatchEvent> events = [];
            for (int i = 0; i < steps; i++)
            {
                world.Events.Clear();
                foreach (ISystem s in run)
                {
                    s.Update(world, Game.Step);
                }

                foreach (Player p in world.Players)
                {
                    p.Command = p.Command with { ChangeLine = false };
                }

                events.AddRange(world.Events.Current);
                world.Time += Game.Step;
                world.Tick++;
            }

            return events;
        }

        /// <summary>The home human presses change line for one step.</summary>
        private static void Press(World world)
        {
            if (world.Controlled(0).TryGet(out Player me))
            {
                me.Command = me.Command with { ChangeLine = true };
            }

            _ = Steps(world, 1);
        }

        /// <summary>The home forwards are tired, so the forwards change; returns the line going off.</summary>
        private static List<Player> TiredForwards(World world)
        {
            List<Player> line =
            [
                .. world
                    .OnIce(Side.Home)
                    .Where(p => !p.IsGoalie && Squad.GroupOf(p) == Group.Forwards),
            ];
            foreach (Player p in line)
            {
                p.Energy = 0.4f;
            }

            return line;
        }

        /// <summary>Puts the skaters going off this far from the bench door, out on the ice.</summary>
        private static void Out(IEnumerable<Player> going, float metres)
        {
            Vector2 door = Squad.Door(Side.Home);
            float x = 0;
            foreach (Player p in going)
            {
                p.Position = new Vector3(door.X + x, 0, door.Y + metres);
                p.Velocity = Vector3.Zero;
                x += 0.3f;
            }
        }

        private static ShiftChange Change(World world, Side side)
        {
            return Bench.Board(world).Changing[(int)side].TryGet(out ShiftChange change)
                ? change
                : throw new InvalidOperationException("expected a change under way");
        }

        private static int Skaters(World world, Side side)
        {
            return world.OnIce(side).Count(p => !p.IsGoalie);
        }

        private static bool CalledOn(World world, Side side)
        {
            return PenaltyCalls
                .Delayed(world)
                .Any(d =>
                    d.Side == side
                    && string.Equals(d.Infraction, Infraction.TooManyMen, StringComparison.Ordinal)
                );
        }

        /// <summary>Two presses rush the change: returns the first skater who stepped out early, the change under way.</summary>
        private static Player Rush(World world, List<Player> going, float metres)
        {
            Out(going, metres);
            Press(world);
            Out(going, metres);
            Press(world);
            Out(going, metres);
            return Change(world, Side.Home).Swaps.First(s => s.Early).In;
        }

        /// <summary>The rule each preset starts with, and its row on the rules page.</summary>
        [Test]
        public void Presets()
        {
            Check(RuleSet.Shl.TooManyMen == TooManyMenCall.Lenient, "SHL: too many men, lenient");
            Check(RuleSet.Nhl.TooManyMen == TooManyMenCall.Lenient, "NHL: too many men, lenient");
            Check(RuleSet.Arcade.TooManyMen == TooManyMenCall.Off, "Arcade: never called");
            RuleRow row = RuleRows.All.First(r =>
                string.Equals(r.Key, nameof(RuleSet.TooManyMen), StringComparison.Ordinal)
            );
            RuleSet custom = RuleChoice.Of(RulePreset.Arcade).With(row, "Strict").Resolve();
            Check(
                RuleRows.Shown.Contains(row)
                    && custom is { TooManyMen: TooManyMenCall.Strict, Custom: true },
                "too many men is a rule on the rules page that can be turned on for Arcade"
            );
            Check(
                !string.Equals(
                    Infraction.Label(Infraction.TooManyMen),
                    Infraction.Label(""),
                    StringComparison.Ordinal
                ),
                "the penalty banner names the infraction"
            );
        }

        /// <summary>The second press rushes the change: the fresh line is on at once, the tired one still skates for the door, and each goes off when he gets there.</summary>
        [Test]
        public void SecondPressRushesTheChange()
        {
            World w = Make(RuleSet.Arcade);
            List<Player> going = TiredForwards(w);
            Out(going, 10f);
            Press(w);
            Check(
                Bench.IsChanging(w, Side.Home) && Skaters(w, Side.Home) == 5,
                "the first press calls a change on the fly, nobody on yet"
            );
            Out(going, 10f);
            Press(w);
            ShiftChange change = Change(w, Side.Home);
            Check(
                change.Rushed
                    && change.Swaps.TrueForAll(s => s.Early && s.In.OnIce && s.Out.OnIce)
                    && Skaters(w, Side.Home) == 8,
                $"the second press puts the fresh line on at once: both lines on the ice ({Skaters(w, Side.Home).ToString(CultureInfo.InvariantCulture)} skaters)"
            );
            Check(w.Controlled(0).Is(p => p.OnIce), "the human still controls a skater on the ice");
            _ = Steps(w, (int)(8 / Game.Step), new LinesSystem(), new SkatingSystem());
            Check(
                going.TrueForAll(p => !p.OnIce)
                    && Skaters(w, Side.Home) == 5
                    && !Bench.IsChanging(w, Side.Home),
                $"the tired line goes off at the door: five skaters again ({Skaters(w, Side.Home).ToString(CultureInfo.InvariantCulture)})"
            );
        }

        /// <summary>Under SHL and NHL rules the early skater playing the puck with his man still out draws a bench minor; Arcade calls nothing.</summary>
        [Test]
        [Arguments(RulePreset.Shl)]
        [Arguments(RulePreset.Nhl)]
        [Arguments(RulePreset.Arcade)]
        public void RushedChangeDrawsBenchMinor(RulePreset preset)
        {
            bool called = preset != RulePreset.Arcade;
            World w = Make(RuleSet.Of(preset));
            List<Player> going = TiredForwards(w);
            Player early = Rush(w, going, 10f);
            Check(!CalledOn(w, Side.Home), $"{preset}: nothing called before the puck is played");
            w.Puck.Carrier = early.Id;
            w.Puck.LastTouch = early.Id;
            Out(going, 10f);
            _ = Steps(w, 1);
            Check(
                CalledOn(w, Side.Home) == called,
                called
                    ? $"{preset}: the early skater plays the puck with his man 10 m out: too many men"
                    : $"{preset}: no call in Arcade"
            );
            if (!called)
            {
                return;
            }

            Check(
                PenaltyCalls.Delayed(w).Where(d => d.Side == Side.Home).Take(2).Count() == 1
                    && PenaltyCalls.Delayed(w)[0].PlayerId == early.Id,
                $"{preset}: one bench minor, served by the skater who stepped out early"
            );
            _ = Steps(w, 30);
            Check(
                PenaltyCalls.Delayed(w).Where(d => d.Side == Side.Home).Take(2).Count() == 1,
                $"{preset}: the change draws no second call"
            );
            List<MatchEvent> events = Steps(w, 1, new LinesSystem(), new PenaltyRules());
            Check(
                events.OfType<PenaltyCalled>().Any(p => p.PlayerId == early.Id)
                    && PenaltyCalls.InBox(w, early.Id)
                    && w.Match.Phase != MatchPhase.Play,
                $"{preset}: his club has the puck, so the whistle goes and he is in the box"
            );
        }

        /// <summary>The skater going off playing the puck after his man is on is too many men too.</summary>
        [Test]
        public void OutgoingSkaterPlaysThePuck()
        {
            World w = Make(RuleSet.Shl);
            List<Player> going = TiredForwards(w);
            _ = Rush(w, going, 10f);
            w.Puck.Carrier = going[0].Id;
            Out(going, 10f);
            _ = Steps(w, 1);
            Check(CalledOn(w, Side.Home), "the man still going off carries the puck: too many men");
        }

        /// <summary>Lenient: no call within a few metres of the door, a call further out; strict calls it from five feet.</summary>
        [Test]
        [Arguments(TooManyMenCall.Lenient, 3f, false)]
        [Arguments(TooManyMenCall.Lenient, 7f, true)]
        [Arguments(TooManyMenCall.Strict, 3f, true)]
        [Arguments(TooManyMenCall.Strict, 2f, true)]
        [Arguments(TooManyMenCall.Off, 12f, false)]
        public void Threshold(TooManyMenCall rule, float metres, bool called)
        {
            World w = Make(RuleSet.Shl with { TooManyMen = rule });
            List<Player> going = TiredForwards(w);
            Player early = Rush(w, going, metres);
            w.Puck.Carrier = early.Id;
            Out(going, metres);
            _ = Steps(w, 1);
            Check(
                CalledOn(w, Side.Home) == called,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{rule}, the man going off {metres} m from the door: {(called ? "too many men" : "no call")}"
                )
            );
        }

        /// <summary>A change that was not rushed never draws it, even with the man going off carrying the puck far out.</summary>
        [Test]
        public void UnrushedChangeIsNeverCalled()
        {
            World w = Make(RuleSet.Shl with { TooManyMen = TooManyMenCall.Strict });
            List<Player> going = TiredForwards(w);
            Out(going, 10f);
            Press(w);
            w.Puck.Carrier = going[0].Id;
            Out(going, 10f);
            _ = Steps(w, 30);
            Check(
                !CalledOn(w, Side.Home)
                    && !Change(w, Side.Home).Rushed
                    && Skaters(w, Side.Home) == 5,
                "one press: the line waits for the door, nothing is called"
            );
        }

        /// <summary>The AI never rushes a change: neither the bench's own changes nor a change line command on an AI skater, with the puck played all over.</summary>
        [Test]
        [Arguments(RulePreset.Shl)]
        [Arguments(RulePreset.Nhl)]
        public void AiNeverCalled(RulePreset preset)
        {
            World w = Make(
                RuleSet.Of(preset) with
                {
                    TooManyMen = TooManyMenCall.Strict,
                },
                human: false
            );
            List<Player> going = TiredForwards(w);
            Out(going, 10f);
            Bench.RequestChange(w, Side.Home);
            _ = Steps(w, 1);
            Check(
                Bench.IsChanging(w, Side.Home),
                $"{preset}: the AI bench calls a change on the fly"
            );
            for (int i = 0; i < 20; i++)
            {
                Bench.RequestChange(w, Side.Home);
                foreach (Player p in w.OnIce(Side.Home))
                {
                    p.Command = p.Command with { ChangeLine = true };
                }

                w.Puck.Carrier = going[i % going.Count].Id;
                Out(going, 10f);
                _ = Steps(w, 1);
            }

            ShiftChange change = Change(w, Side.Home);
            Check(
                !change.Rushed && !change.Swaps.Exists(s => s.Early) && Skaters(w, Side.Home) == 5,
                $"{preset}: the AI's change is never rushed, five skaters on"
            );
            Check(!CalledOn(w, Side.Home), $"{preset}: and never draws too many men");
        }

        /// <summary>At a stoppage the rushed change finishes: the man still going off goes to the bench.</summary>
        [Test]
        public void StoppageFinishesRushedChange()
        {
            World w = Make(RuleSet.Arcade);
            List<Player> going = TiredForwards(w);
            _ = Rush(w, going, 10f);
            w.Match.Phase = MatchPhase.Stopped;
            w.Puck.Live = false;
            _ = Steps(w, 2);
            w.Match.Phase = MatchPhase.Faceoff;
            _ = Steps(w, 1);
            Check(
                going.TrueForAll(p => !p.OnIce)
                    && Skaters(w, Side.Home) == 5
                    && !Bench.IsChanging(w, Side.Home),
                $"the faceoff sends the tired line off: five skaters ({Skaters(w, Side.Home).ToString(CultureInfo.InvariantCulture)})"
            );
        }
    }
}
