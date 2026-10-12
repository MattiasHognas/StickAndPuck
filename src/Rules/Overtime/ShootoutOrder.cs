using StickAndPuck.Core;
using static Raylib_cs.Raylib;

namespace StickAndPuck.Rules;

/// <summary>
/// W45: how many shooters each club sends and who shoots first, from the match's rules
/// (<see cref="RuleSet.ShootoutShooters"/>, <see cref="RuleSet.ShootoutFirst"/>). SHL: three each,
/// away first, and away also starts every sudden death round. NHL: three each, the home club chooses;
/// an AI home club shoots first, a human one picks with shoot (first) or pass (second).
/// </summary>
public static class ShootoutOrder
{
    /// <summary>A human home club that has not picked by then shoots first.</summary>
    public const float ChooseSeconds = 10f;

    /// <summary>Gets the shooters per club before sudden death.</summary>
    /// <param name="setup">The match setup.</param>
    /// <returns>Shooters, at least one.</returns>
    public static int Shooters(MatchSetup setup)
    {
        return Math.Max(1, setup.Rules.ShootoutShooters);
    }

    /// <summary>Gets the club that shoots first, or null when a human home club is to choose.</summary>
    /// <param name="setup">The match setup.</param>
    /// <returns>The first club, or null while home chooses.</returns>
    public static Side? First(MatchSetup setup)
    {
        return setup.Rules.ShootoutFirst switch
        {
            ShootoutFirst.Home => Side.Home,
            ShootoutFirst.HomeChooses => HomeHuman(setup) is null ? Side.Home : null,
            _ => Side.Away,
        };
    }

    /// <summary>Gets the club to shoot next: the first club whenever both have taken as many, so it also opens every sudden death round.</summary>
    /// <param name="so">The shootout so far.</param>
    /// <param name="first">The club that shoots first.</param>
    /// <returns>The club.</returns>
    public static Side Next(ShootoutState so, Side first)
    {
        var second = first == Side.Home ? Side.Away : Side.Home;
        return so.Taken(Side.Home) == so.Taken(Side.Away) ? first : second;
    }

    /// <summary>Gets the home club's human device, or null when the AI plays home.</summary>
    /// <param name="setup">The match setup.</param>
    /// <returns>The device.</returns>
    public static InputDevice? HomeHuman(MatchSetup setup)
    {
        foreach (var input in setup.Inputs)
            if (input.Device != InputDevice.None && input.Side == Side.Home) return input.Device;
        return null;
    }

    /// <summary>Reads the home human's pick this step: shoot for first, pass for second.</summary>
    /// <param name="world">The match.</param>
    /// <returns>The club the home human sends first, or null with no pick yet.</returns>
    internal static Side? Pick(World world)
    {
        if (!IsWindowReady() || HomeHuman(world.Setup) is not { } device) return null;
        if (Pressed(world.Settings, device, GameAction.Shoot)) return Side.Home;
        if (Pressed(world.Settings, device, GameAction.Pass)) return Side.Away;
        return null;
    }

    static bool Pressed(Settings settings, InputDevice device, GameAction action)
    {
        var b = KeyBindings.Of(settings, action);
        if (device == InputDevice.KeyboardMouse)
            return (b.Key != Raylib_cs.KeyboardKey.Null && IsKeyPressed(b.Key)) || (b.Alt != Raylib_cs.KeyboardKey.Null && IsKeyPressed(b.Alt));
        int pad = device == InputDevice.Gamepad2 ? 1 : 0;
        return b.Button != Raylib_cs.GamepadButton.Unknown && IsGamepadAvailable(pad) && IsGamepadButtonPressed(pad, b.Button);
    }
}
