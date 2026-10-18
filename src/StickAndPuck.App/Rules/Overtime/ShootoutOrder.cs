using StickAndPuck.App.Core;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Rules.Overtime
{
    /// <summary>How many shooters each club sends and who shoots first, from the match's rules
    /// (<see cref="RuleSet.ShootoutShooters"/>, <see cref="RuleSet.ShootoutFirst"/>). SHL: three each,
    /// away first, and away also starts every sudden death round. NHL: three each, the home club chooses;
    /// an AI home club shoots first, a human one picks with shoot (first) or pass (second).</summary>
    public static class ShootoutOrder
    {
        /// <summary>A human home club that has not picked by then shoots first.</summary>
        public const float ChooseSeconds = 10f;

        /// <summary>Gets the shooters per club before sudden death.</summary>
        public static int Shooters(MatchSetup setup)
        {
            return Math.Max(1, setup.Rules.ShootoutShooters);
        }

        /// <summary>Gets the club that shoots first, or None when a human home club is to choose.</summary>
        public static SideOrNone First(MatchSetup setup)
        {
            return setup.Rules.ShootoutFirst switch
            {
                ShootoutFirst.Home => SideOrNone.Home,
                ShootoutFirst.HomeChooses => HomeHuman(setup) == InputDevice.None
                    ? SideOrNone.Home
                    : SideOrNone.None,
                ShootoutFirst.Away => SideOrNone.Away,
                _ => SideOrNone.Away,
            };
        }

        /// <summary>Gets the club to shoot next: the first club whenever both have taken as many, so it also opens every sudden death round.</summary>
        public static Side Next(ShootoutState so, Side first)
        {
            Side second = first == Side.Home ? Side.Away : Side.Home;
            return so.Taken(Side.Home) == so.Taken(Side.Away) ? first : second;
        }

        /// <summary>Gets the home club's human device, or <see cref="InputDevice.None"/> when the AI plays home.</summary>
        public static InputDevice HomeHuman(MatchSetup setup)
        {
            foreach (InputSlot input in setup.Inputs)
            {
                if (input.Device != InputDevice.None && input.Side == Side.Home)
                {
                    return input.Device;
                }
            }

            return InputDevice.None;
        }

        /// <summary>Reads the home human's pick this step: shoot for first, pass for second.</summary>
        internal static SideOrNone Pick(World world)
        {
            if (!IsWindowReady())
            {
                return SideOrNone.None;
            }

            InputDevice device = HomeHuman(world.Setup);
            return device == InputDevice.None ? SideOrNone.None
                : Pressed(world.Settings, device, GameAction.Shoot) ? SideOrNone.Home
                : (
                    Pressed(world.Settings, device, GameAction.Pass)
                        ? SideOrNone.Away
                        : SideOrNone.None
                );
        }

        private static bool Pressed(Settings settings, InputDevice device, GameAction action)
        {
            KeyBinding b = KeyBindings.Of(settings, action);
            if (device == InputDevice.KeyboardMouse)
            {
                return (b.Key != Raylib_cs.KeyboardKey.Null && DeviceInput.KeyPressed(b.Key))
                    || (b.Alt != Raylib_cs.KeyboardKey.Null && DeviceInput.KeyPressed(b.Alt));
            }

            int pad = device == InputDevice.Gamepad2 ? 1 : 0;
            return b.Button != Raylib_cs.GamepadButton.Unknown
                && DeviceInput.PadAvailable(pad)
                && DeviceInput.PadPressed(pad, b.Button);
        }
    }
}
