using System.Numerics;
using StickAndPuck.App.AI;
using StickAndPuck.App.Core;
using StickAndPuck.App.Goalie;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players;
using StickAndPuck.App.Rules;
using StickAndPuck.App.Screens;
using StickAndPuck.Tests.Support;
using StickAndPuck.App.Versus;

namespace StickAndPuck.Tests;

/// <summary>
/// Player vs player (W17). Two devices cannot be pressed without hands, so the two human slots are
/// driven with scripted commands: a short match with player one skating the home centre and player
/// two the away centre, the rest AI, checked for both humans moving their own skater, control never
/// crossing clubs, and the match reaching its final. Formerly SAP_SCENE=versustest.
/// </summary>
public sealed class VersusTests : CheckedTest
{
    [Test]
    public void ModeOnList()
    {
        var modes = GameModes.All();
        int versus = modes.ToList().FindIndex(m => m is VersusMode);
        Check(versus > 0, $"player vs player is on the new game list, after exhibition (place {versus + 1} of {modes.Count})");
        var mode = new VersusMode();
        Check(mode.ClubPicks == 2 && mode.PickHeading(0).Contains("PLAYER 1", StringComparison.Ordinal) && mode.PickHeading(1).Contains("PLAYER 2", StringComparison.Ordinal),
            "each player picks a club under their own heading");
    }

    [Test]
    public void Devices()
    {
        Env.Set("SAP_VERSUS_P2", "");
        var inputs = VersusMode.Inputs(InputDevice.KeyboardMouse);
        Check(inputs.Count == 2 && inputs[0] == new InputSlot(InputDevice.KeyboardMouse, Side.Home) && inputs[1] == new InputSlot(InputDevice.Gamepad1, Side.Away),
            "by default player one is keyboard and mouse on the home club, player two the first gamepad on the visitors");
        Check(VersusMode.SecondDevice(InputDevice.Gamepad1) == InputDevice.KeyboardMouse, "when player one holds the first gamepad, player two gets the keyboard");
        Check(VersusMode.SecondDevice(InputDevice.Gamepad2) == InputDevice.Gamepad1, "when player one holds the second gamepad, player two gets the first");
        Env.Set("SAP_VERSUS_P2", "pad2");
        Check(VersusMode.SecondDevice(InputDevice.KeyboardMouse) == InputDevice.Gamepad2, "SAP_VERSUS_P2=pad2 puts player two on the second gamepad");
        Env.Set("SAP_VERSUS_P2", "keyboard");
        Check(VersusMode.SecondDevice(InputDevice.KeyboardMouse) == InputDevice.Gamepad1, "SAP_VERSUS_P2 never gives both players the same device");
        Env.Set("SAP_VERSUS_P2", "");
        var ai = VersusMode.Inputs(InputDevice.None);
        Check(ai.All(s => s.Device == InputDevice.None), "with no human (SAP_HUMAN=0) both clubs go to the AI");
    }

    [Test]
    public void SetupFromPicks()
    {
        var clubs = WorldBuilder.Clubs;
        var picks = clubs.Count > 1 ? new[] { clubs[1], clubs[0] } : new[] { clubs[0], clubs[0] };
        var setup = new VersusMode().Setup(new GameModeStart(clubs, picks, InputDevice.KeyboardMouse));
        Check(setup.Is(x => x.Home == picks[0] && x.Away == picks[1]), "player one's pick is the home club, player two's the visitors");
        Check(setup.Is(x => x.Inputs.Count(s => s.Device != InputDevice.None) == 2 && x.Inputs[0].Side != x.Inputs[1].Side),
            "the setup has two human slots on opposite clubs");
        Check(setup.Is(x => x.Overtime == OvertimeRule.Shl), "a tie goes to 3 on 3 overtime, then a shootout");
    }

    /// <summary>A one-period match with both humans scripted: each skates for the puck and shoots when carrying it.</summary>
    [Test]
    public void ScriptedMatch()
    {
        var setup = WorldBuilder.DefaultSetup with { PeriodSeconds = 60, Periods = 1, Overtime = OvertimeRule.None, Inputs = VersusMode.Inputs(InputDevice.KeyboardMouse) };
        var world = new World(setup, new Settings());
        Scenes.AddStarters(world);
        VersusMode.AssignSlots(world);
        var first = (Held(world, 0), Held(world, 1));
        Check(first.Item1.Is(p => p.Side == Side.Home && !p.IsGoalie) && first.Item2.Is(p => p.Side == Side.Away && !p.IsGoalie),
            "at the faceoff player one controls a home skater and player two an away skater");
        Check(first.Item1.Is(p => p.Data.Position == PlayingPosition.C) && first.Item2.Is(p => p.Data.Position == PlayingPosition.C), "both start on their centre");
        MatchFlow.Begin(world, log: false);

#pragma warning disable S2245 // fixed seeds so the checks repeat
        ISystem[] systems =
        [
            new PositioningSystem(new Random(17)), new DecisionSystem(new Random(17)), new SwitchingSystem(), new SkatingSystem(), new CheckingSystem(new Random(17)),
            new PuckHandlingSystem(new Random(17)), new GoalieSystem { Dice = new Random(17) }, new PuckPhysics(), new MatchRules(), new OvertimeRules(),
        ];
#pragma warning restore S2245
        foreach (var s in systems) if (s is not (PuckPhysics or GoalieSystem)) s.Start(world);

        var run = new Run(setup);
        int steps = (int)(300 / Game.Step);    // a 60 s period plus the intro, faceoffs and goal stoppages
        for (int i = 0; i < steps && world.Match.Phase != MatchPhase.Final; i++)
        {
            world.Events.Clear();
            run.Before(world);     // the humans' commands land first, as InputSystem's do; the AI must leave them alone
            foreach (var s in systems) s.Update(world, Game.Step);
            run.After(world);
            world.Time += Game.Step; world.Tick++;
        }

        var (crossed, overwritten, moved, carried, shots) = (run.Crossed, run.Overwritten, run.Moved, run.Carried, run.Shots);
        Console.WriteLine($"      match: {world.Match.Score[0]}-{world.Match.Score[1]}, shots on goal {world.Match.Shots[0]}-{world.Match.Shots[1]}, player one carried {carried[0]} steps and shot {shots[0]}, player two carried {carried[1]} steps and shot {shots[1]}, moved {moved[0]:F1} m and {moved[1]:F1} m");
        Check(world.Match.Phase == MatchPhase.Final, "a one-period versus match plays out to the final");
        Check(moved[0] > 5f && moved[1] > 5f, "both humans skate their own player where their stick points");
        Check(!overwritten, "the AI never overwrites a human's command");
        Check(!crossed, "control switching keeps each player on their own club");
        Check(carried[0] > 0 && carried[1] > 0, "both humans get the puck during the period");
        Check(world.Controlled(0).HasValue && world.Controlled(1).HasValue, "both slots still control a skater at the final");
    }

    /// <summary>What the scripted humans did over the match.</summary>
    sealed class Run(MatchSetup setup)
    {
        public bool Crossed, Overwritten;
        public float[] Moved = new float[2];
        public int[] Carried = new int[2], Shots = new int[2];
        readonly Maybe<Vector3>[] starts = new Maybe<Vector3>[2];

        public void Before(World world)
        {
            for (int slot = 0; slot < 2; slot++)
            {
                if (!world.Controlled(slot).TryGet(out var me)) continue;
                if (me.Side != setup.Inputs[slot].Side) Crossed = true;
                var (carrying, shot) = Drive(world, me);
                if (carrying) Carried[slot]++;
                if (shot) Shots[slot]++;
            }
        }

        public void After(World world)
        {
            for (int slot = 0; slot < 2; slot++)
            {
                if (!world.Controlled(slot).TryGet(out var me) || world.Match.Phase != MatchPhase.Play) continue;
                if (me.Command.Move == Vector2.Zero && me.Action == PlayerAction.None && Vector3.Distance(me.Position, world.Puck.Position) > 1f) Overwritten = true;
                var start = starts[slot].Or(me.Position);
                starts[slot] = start;
                Moved[slot] = Math.Max(Moved[slot], Vector3.Distance(start, me.Position));
            }
        }

        /// <summary>Scripts one human: skate for the puck, carry it at the other net and shoot from inside 14 m.</summary>
        static (bool Carrying, bool Shot) Drive(World world, Player me)
        {
            bool carrying = world.Puck.Carrier == me.Id;
            var goal = new Vector3(me.Side == Side.Home ? RinkSpec.GoalLineX : -RinkSpec.GoalLineX, 0, 0);
            var to = (carrying ? goal : world.Puck.Position) - me.Position;
            var dir = new Vector2(to.X, to.Z);
            if (dir.LengthSquared() > 0.01f) dir = Vector2.Normalize(dir);
            bool shoot = carrying && Vector3.Distance(me.Position, goal) < 14f && world.Match.Phase == MatchPhase.Play;
            me.Command = new PlayerCommand { Move = dir, Sprint = true, Shoot = shoot };
            return (carrying, shoot);
        }
    }

    /// <summary>The real input system on two devices with nothing pressed: both slots read, nobody gets a stale press.</summary>
    [Test]
    public void InputSlots()
    {
        var world = new WorldBuilder().Setup(s => s with { Inputs = VersusMode.Inputs(InputDevice.KeyboardMouse) }).NotBegun().Build();
        VersusMode.AssignSlots(world);
        var input = new InputSystem();
        var one = Must(Held(world, 0));
        var two = Must(Held(world, 1));
        one.Command = new PlayerCommand { Move = Vector2.UnitX, Shoot = true };
        two.Command = new PlayerCommand { Move = Vector2.UnitX, Pass = true };
        input.Update(world, Game.Step);
        Check(one.Command.Move == Vector2.Zero && !one.Command.Shoot && two.Command.Move == Vector2.Zero && !two.Command.Pass,
            "the input system writes both slots' devices to their players every step");
    }

    /// <summary>The player a slot controls, if any.</summary>
    static Maybe<Player> Held(World world, int slot) => world.Controlled(slot);

    /// <summary>Gets a value the test needs, failing loudly when it is absent.</summary>
    static T Must<T>(Maybe<T> maybe)
        where T : notnull => maybe.TryGet(out var value) ? value : throw new InvalidOperationException("expected a value");
}
