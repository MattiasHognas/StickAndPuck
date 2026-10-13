using System.Numerics;
using Raylib_cs;
using StickAndPuck.Core;
using StickAndPuck.Rules;
using static Raylib_cs.Raylib;

namespace StickAndPuck.Lines;

/// <summary>
/// W23: pulling the goalie for an extra attacker. A club pulls its goalie when a penalty call is
/// delayed against the other club (the whistle goes as soon as they touch the puck, so the net is
/// safe), and, trailing by a goal or two late in the last period of regulation, to press for the
/// equaliser. AI clubs do both by themselves; a club with a human on it pulls for a delayed call
/// by itself and otherwise on the button (G on the keyboard, back on a gamepad), which also
/// brings the goalie back. The goalie comes back at the next whistle, which covers a goal at
/// either end. <see cref="LinesSystem"/> runs <see cref="Update"/> each step; the penalties
/// (W14) count an extra attacker when they balance the skaters at a faceoff.
/// </summary>
public static class GoaliePull
{
    static readonly Side[] BothSides = [Side.Home, Side.Away];

    /// <summary>Gets whether a club has pulled its goalie.</summary>
    /// <param name="world">The match.</param>
    /// <param name="side">The club.</param>
    /// <returns>True while the net is empty.</returns>
    public static bool IsPulled(World world, Side side)
    {
        return Bench.Board(world).PulledGoalie[(int)side] != null;
    }

    /// <summary>Gets the extra attacker who came on for the goalie, if he is still on the ice.</summary>
    /// <param name="world">The match.</param>
    /// <param name="side">The club.</param>
    /// <returns>The skater, or null.</returns>
    public static Player? ExtraAttacker(World world, Side side)
    {
        var b = Bench.Board(world);
        int id = b.ExtraAttacker[(int)side];
        return id >= 0 && id < world.Players.Count && world.Players[id].OnIce ? world.Players[id] : null;
    }

    /// <summary>Gets how long before the end of the last period the AI pulls its goalie when trailing: two minutes, shortened with the period.</summary>
    /// <param name="world">The match.</param>
    /// <returns>Game seconds.</returns>
    public static float PullSeconds(World world)
    {
        return LinesTuning.PullSeconds * world.Setup.PeriodSeconds / PenaltyTuning.FullPeriodSeconds;
    }

    /// <summary>
    /// Pulls a club's goalie: he leaves for the bench and the freshest forward on the bench comes
    /// on through the bench door as the extra attacker. Nothing happens unless play is running
    /// with the goalie on the ice.
    /// </summary>
    /// <param name="world">The match.</param>
    /// <param name="side">The club.</param>
    /// <returns>True if the goalie was pulled.</returns>
    public static bool Pull(World world, Side side)
    {
        if (!world.Setup.Rules.GoaliePull || !MatchFlow.IsRunning(world) || world.Match.Phase != MatchPhase.Play || IsPulled(world, side)) return false;
        var goalie = world.OnIce(side).FirstOrDefault(p => p.IsGoalie);
        if (goalie == null) return false;
        var b = Bench.Board(world);
        Bench.SendOff(world, goalie);
        var extra = Bench.SendOn(world, side, Group.Forwards, onTheFly: true);
        b.PulledGoalie[(int)side] = goalie;
        b.ExtraAttacker[(int)side] = extra?.Id ?? -1;
        b.Last[(int)side] = ("EXTRA", world.Time);
        Log(world, $"{world.Setup.Club(side).ShortName} pulls {goalie.Data.LastName}" + (extra != null ? $", {extra.Data.LastName} comes on" : string.Empty));
        return true;
    }

    /// <summary>Brings a pulled goalie back into his crease and sends the extra attacker off, if he is still out there.</summary>
    /// <param name="world">The match.</param>
    /// <param name="side">The club.</param>
    /// <returns>True if the goalie came back.</returns>
    public static bool Return(World world, Side side)
    {
        var b = Bench.Board(world);
        var goalie = b.PulledGoalie[(int)side];
        if (goalie == null) return false;
        b.PulledGoalie[(int)side] = null;
        // the extra attacker goes off, or whoever took his place in a line change since
        int skaters = world.OnIce(side).Count(p => !p.IsGoalie);
        if (Bench.Surplus(world, side, skaters - 1) is { } extra) Bench.SendOff(world, extra);
        b.ExtraAttacker[(int)side] = -1;
        int end = FaceoffFormation.DefendedEnd(world.Match, side);
        goalie.OnIce = true;
        goalie.Position = new Vector3(end * (RinkSpec.GoalLineX - 1f), 0, 0);
        goalie.Velocity = Vector3.Zero;
        goalie.Heading = end < 0 ? MathF.PI / 2 : -MathF.PI / 2;
        goalie.Command = default;
        goalie.Begin(PlayerAction.None);
        Log(world, $"{world.Setup.Club(side).ShortName} goalie {goalie.Data.LastName} back in");
        return true;
    }

    /// <summary>One step: the goalie comes back at any stoppage; in play the AI, a delayed call or the button pulls him.</summary>
    /// <param name="world">The match.</param>
    internal static void Update(World world)
    {
        var m = world.Match;
        var b = Bench.Board(world);
        bool play = m.Phase == MatchPhase.Play;
        foreach (var side in BothSides)
        {
            if (!play)
            {
                Return(world, side);
                continue;
            }

            var other = side == Side.Home ? Side.Away : Side.Home;
            bool delayedAgainstOther = Penalties.Delayed(world).Any(d => d.Side == other);
            bool human = world.Players.Exists(p => p.Side == side && p.Slot >= 0);
            if (!IsPulled(world, side) && (delayedAgainstOther || (!human && WantsExtraAttacker(world, side)))) Pull(world, side);
        }
        if (play) Buttons(world, b);
    }

    /// <summary>Whether an AI club trailing late wants the extra attacker: last period of regulation, inside the window, down one or two, and the puck out of its own zone or on its stick.</summary>
    static bool WantsExtraAttacker(World world, Side side)
    {
        var m = world.Match;
        if (m.Period != world.Setup.Periods || m.Clock > PullSeconds(world)) return false;
        int deficit = m.Score[(int)Other(side)] - m.Score[(int)side];
        if (deficit < 1 || deficit > LinesTuning.PullMaxDeficit) return false;
        var pk = world.Puck;
        if (pk.Carrier >= 0 && pk.Carrier < world.Players.Count && world.Players[pk.Carrier].Side == side) return true;
        int end = FaceoffFormation.DefendedEnd(m, side);
        return pk.Position.X * end < RinkSpec.BlueLineX;
    }

    /// <summary>The pull button of each human slot toggles the goalie of that slot's club.</summary>
    static void Buttons(World world, LinesBoard b)
    {
        if (!IsWindowReady()) return;
        var inputs = world.Setup.Inputs;
        bool padFree = !inputs.Any(i => i.Device == InputDevice.Gamepad1);
        for (int slot = 0; slot < inputs.Count && slot < b.PullHeld.Length; slot++)
        {
            bool held = inputs[slot].Device switch
            {
                InputDevice.KeyboardMouse => DeviceInput.KeyDown(KeyboardKey.G) || (padFree && PadBack(0)),
                InputDevice.Gamepad1 => PadBack(0),
                InputDevice.Gamepad2 => PadBack(1),
                _ => false,
            };
            bool pressed = held && !b.PullHeld[slot];
            b.PullHeld[slot] = held;
            if (!pressed) continue;
            var side = inputs[slot].Side;
            if (IsPulled(world, side)) Return(world, side);
            else Pull(world, side);
        }
    }

    static bool PadBack(int pad)
    {
        return DeviceInput.PadAvailable(pad) && DeviceInput.PadDown(pad, GamepadButton.MiddleLeft);
    }

    static Side Other(Side side)
    {
        return side == Side.Home ? Side.Away : Side.Home;
    }

    static void Log(World world, string text)
    {
        if (MatchFlow.State(world).Log) Console.WriteLine($"LINES: {text}");
    }
}
