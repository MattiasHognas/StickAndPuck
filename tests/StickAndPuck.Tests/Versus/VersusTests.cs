using System.Globalization;
using System.Numerics;
using StickAndPuck.App.AI.Decisions;
using StickAndPuck.App.AI.Positioning;
using StickAndPuck.App.Core;
using StickAndPuck.App.Goalie;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players.Checks;
using StickAndPuck.App.Players.Input;
using StickAndPuck.App.Players.Movement;
using StickAndPuck.App.Players.Puck;
using StickAndPuck.App.Players.Switching;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Rules.Overtime;
using StickAndPuck.App.Screens;
using StickAndPuck.App.Versus;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Versus
{
    /// <summary>
    /// Player vs player. Two devices cannot be pressed without hands, so the two human slots are
    /// driven with scripted commands: a short match with player one skating the home centre and player
    /// two the away centre, the rest AI, checked for both humans moving their own skater, control never
    /// crossing clubs, and the match reaching its final. Formerly SAP_SCENE=versustest.
    /// </summary>
    public sealed class VersusTests : CheckedTest
    {
        /// <summary>The game state the test plays in, fresh for each test.</summary>
        private readonly AppState app = new();

        /// <summary>Gets the settings screen's PLAYER 2 CONTROLS pick, as saved by default.</summary>
        private static InputDevice Player2 => new ScreenPrefs().Player2;

        [Test]
        public void ModeOnList()
        {
            IReadOnlyList<IGameMode> modes = GameModes.All(app);
            int versus = modes.ToList().FindIndex(m => m is VersusMode);
            Check(
                versus > 0,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"player vs player is on the new game list, after exhibition (place {versus + 1} of {modes.Count})"
                )
            );
            VersusMode mode = new();
            Check(
                mode.ClubPicks == 2
                    && mode.PickHeading(app, 0).Contains("PLAYER 1", StringComparison.Ordinal)
                    && mode.PickHeading(app, 1).Contains("PLAYER 2", StringComparison.Ordinal),
                "each player picks a club under their own heading"
            );
        }

        [Test]
        public void Devices()
        {
            IReadOnlyList<InputSlot> inputs = VersusMode.Inputs(InputDevice.KeyboardMouse, Player2);
            Check(
                inputs.Count == 2
                    && inputs[0] == new InputSlot(InputDevice.KeyboardMouse, Side.Home)
                    && inputs[1] == new InputSlot(InputDevice.Gamepad1, Side.Away),
                "by default player one is keyboard and mouse on the home club, player two the first gamepad on the visitors"
            );
            Check(
                VersusMode.SecondDevice(InputDevice.Gamepad1, Player2, "")
                    == InputDevice.KeyboardMouse,
                "when player one holds the first gamepad, player two gets the keyboard"
            );
            Check(
                VersusMode.SecondDevice(InputDevice.Gamepad2, Player2, "") == InputDevice.Gamepad1,
                "when player one holds the second gamepad, player two gets the first"
            );
            Check(
                VersusMode.SecondDevice(InputDevice.KeyboardMouse, Player2, "pad2")
                    == InputDevice.Gamepad2,
                "SAP_VERSUS_P2=pad2 puts player two on the second gamepad"
            );
            Check(
                VersusMode.SecondDevice(InputDevice.KeyboardMouse, Player2, "keyboard")
                    == InputDevice.Gamepad1,
                "SAP_VERSUS_P2 never gives both players the same device"
            );
            IReadOnlyList<InputSlot> ai = VersusMode.Inputs(InputDevice.None, Player2);
            Check(
                ai.All(s => s.Device == InputDevice.None),
                "with no human (SAP_HUMAN=0) both clubs go to the AI"
            );
        }

        [Test]
        public void SetupFromPicks()
        {
            IReadOnlyList<ClubData> clubs = WorldBuilder.Clubs;
            ClubData[] picks = clubs.Count > 1 ? [clubs[1], clubs[0]] : [clubs[0], clubs[0]];
            Maybe<MatchSetup> setup = new VersusMode().Setup(
                new GameModeStart(clubs, picks, InputDevice.KeyboardMouse, app)
            );
            Check(
                setup.Is(x => x.Home == picks[0] && x.Away == picks[1]),
                "player one's pick is the home club, player two's the visitors"
            );
            Check(
                setup.Is(x =>
                    x.Inputs.Where(s => s.Device != InputDevice.None).Take(3).Count() == 2
                    && x.Inputs[0].Side != x.Inputs[1].Side
                ),
                "the setup has two human slots on opposite clubs"
            );
            Check(
                setup.Is(x => x.Overtime == OvertimeRule.Shl),
                "a tie goes to 3 on 3 overtime, then a shootout"
            );
        }

        /// <summary>A one-period match with both humans scripted: each skates for the puck and shoots when carrying it.</summary>
        [Test]
        public void ScriptedMatch()
        {
            MatchSetup setup = WorldBuilder.DefaultSetup with
            {
                PeriodSeconds = 60,
                Periods = 1,
                Overtime = OvertimeRule.None,
                Inputs = VersusMode.Inputs(InputDevice.KeyboardMouse, Player2),
            };
            World world = new(setup, new Settings()) { App = app };
            Scenes.AddStarters(world);
            VersusMode.AssignSlots(world);
            (Maybe<Player>, Maybe<Player>) first = (Held(world, 0), Held(world, 1));
            Check(
                first.Item1.Is(p => p.Side == Side.Home && !p.IsGoalie)
                    && first.Item2.Is(p => p.Side == Side.Away && !p.IsGoalie),
                "at the faceoff player one controls a home skater and player two an away skater"
            );
            Check(
                first.Item1.Is(p => p.Data.Position == PlayingPosition.C)
                    && first.Item2.Is(p => p.Data.Position == PlayingPosition.C),
                "both start on their centre"
            );
            MatchFlow.Begin(world, log: false);

#pragma warning disable S2245 // fixed seeds so the checks repeat
            ISystem[] systems =
            [
                new PositioningSystem(new Random(17)),
                new DecisionSystem(new Random(17)),
                new SwitchingSystem(),
                new SkatingSystem(),
                new CheckingSystem(new Random(17)),
                new PuckHandlingSystem(new Random(17)),
                new GoalieSystem { Dice = new Random(17) },
                new PuckPhysics(),
                new MatchRules(),
                new OvertimeRules(),
            ];
#pragma warning restore S2245
            foreach (ISystem s in systems.Where(s => s is not (PuckPhysics or GoalieSystem)))
            {
                s.Start(world);
            }

            Run run = new(setup);
            int steps = (int)(300 / Game.Step); // a 60 s period plus the intro, faceoffs and goal stoppages
            for (int i = 0; i < steps && world.Match.Phase != MatchPhase.Final; i++)
            {
                world.Events.Clear();
                run.Before(world); // the humans' commands land first, as InputSystem's do; the AI must leave them alone
                foreach (ISystem s in systems)
                {
                    s.Update(world, Game.Step);
                }

                run.After(world);
                world.Time += Game.Step;
                world.Tick++;
            }

            VerifyVersusMatch(world, run);
        }

        private void VerifyVersusMatch(World world, Run run)
        {
            (bool crossed, bool overwritten, float[] moved, int[] carried, int[] shots) = (
                run.Crossed,
                run.Overwritten,
                run.Moved,
                run.Carried,
                run.Shots
            );
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"      match: {world.Match.Score[0]}-{world.Match.Score[1]}, shots on goal {world.Match.Shots[0]}-{world.Match.Shots[1]}, player one carried {carried[0]} steps and shot {shots[0]}, player two carried {carried[1]} steps and shot {shots[1]}, moved {moved[0]:F1} m and {moved[1]:F1} m"
                )
            );
            Check(
                world.Match.Phase == MatchPhase.Final,
                "a one-period versus match plays out to the final"
            );
            Check(
                moved[0] > 5f && moved[1] > 5f,
                "both humans skate their own player where their stick points"
            );
            Check(!overwritten, "the AI never overwrites a human's command");
            Check(!crossed, "control switching keeps each player on their own club");
            Check(carried[0] > 0 && carried[1] > 0, "both humans get the puck during the period");
            Check(
                world.Controlled(0).HasValue && world.Controlled(1).HasValue,
                "both slots still control a skater at the final"
            );
        }

        /// <summary>What the scripted humans did over the match.</summary>
        private sealed class Run(MatchSetup setup)
        {
            public bool Crossed,
                Overwritten;
            public float[] Moved = new float[2];
            public int[] Carried = new int[2],
                Shots = new int[2];
            private readonly Maybe<Vector3>[] starts = new Maybe<Vector3>[2];

            public void Before(World world)
            {
                for (int slot = 0; slot < 2; slot++)
                {
                    if (!world.Controlled(slot).TryGet(out Player me))
                    {
                        continue;
                    }

                    if (me.Side != setup.Inputs[slot].Side)
                    {
                        Crossed = true;
                    }

                    (bool carrying, bool shot) = Drive(world, me);
                    if (carrying)
                    {
                        Carried[slot]++;
                    }

                    if (shot)
                    {
                        Shots[slot]++;
                    }
                }
            }

            public void After(World world)
            {
                for (int slot = 0; slot < 2; slot++)
                {
                    if (
                        !world.Controlled(slot).TryGet(out Player me)
                        || world.Match.Phase != MatchPhase.Play
                    )
                    {
                        continue;
                    }

                    if (
                        me.Command.Move == Vector2.Zero
                        && me.Action == PlayerAction.None
                        && Vector3.Distance(me.Position, world.Puck.Position) > 1f
                    )
                    {
                        Overwritten = true;
                    }

                    Vector3 start = starts[slot].Or(me.Position);
                    starts[slot] = start;
                    Moved[slot] = Math.Max(Moved[slot], Vector3.Distance(start, me.Position));
                }
            }

            /// <summary>Scripts one human: skate for the puck, carry it at the other net and shoot from inside 14 m.</summary>
            private static (bool Carrying, bool Shot) Drive(World world, Player me)
            {
                bool carrying = world.Puck.Carrier == me.Id;
                Vector3 goal = new(
                    me.Side == Side.Home ? RinkSpec.GoalLineX : -RinkSpec.GoalLineX,
                    0,
                    0
                );
                Vector3 to = (carrying ? goal : world.Puck.Position) - me.Position;
                Vector2 dir = new(to.X, to.Z);
                if (dir.LengthSquared() > 0.01f)
                {
                    dir = Vector2.Normalize(dir);
                }

                bool shoot =
                    carrying
                    && Vector3.Distance(me.Position, goal) < 14f
                    && world.Match.Phase == MatchPhase.Play;
                me.Command = new PlayerCommand
                {
                    Move = dir,
                    Sprint = true,
                    Shoot = shoot,
                };
                return (carrying, shoot);
            }
        }

        /// <summary>The real input system on two devices with nothing pressed: both slots read, nobody gets a stale press.</summary>
        [Test]
        public void InputSlots()
        {
            World world = new WorldBuilder()
                .Setup(s =>
                    s with
                    {
                        Inputs = VersusMode.Inputs(InputDevice.KeyboardMouse, Player2),
                    }
                )
                .NotBegun()
                .Build();
            VersusMode.AssignSlots(world);
            InputSystem input = new();
            Player one = Must(Held(world, 0));
            Player two = Must(Held(world, 1));
            one.Command = new PlayerCommand { Move = Vector2.UnitX, Shoot = true };
            two.Command = new PlayerCommand { Move = Vector2.UnitX, Pass = true };
            input.Update(world, Game.Step);
            Check(
                one.Command.Move == Vector2.Zero
                    && !one.Command.Shoot
                    && two.Command.Move == Vector2.Zero
                    && !two.Command.Pass,
                "the input system writes both slots' devices to their players every step"
            );
        }

        /// <summary>The player a slot controls, if any.</summary>
        private static Maybe<Player> Held(World world, int slot)
        {
            return world.Controlled(slot);
        }

        /// <summary>Gets a value the test needs, failing loudly when it is absent.</summary>
        private static T Must<T>(Maybe<T> maybe)
            where T : notnull
        {
            return maybe.TryGet(out T value)
                ? value
                : throw new InvalidOperationException("expected a value");
        }
    }
}
