using System.Collections.Immutable;
using Raylib_cs;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Screens
{
    /// <summary>Menu presses from the keyboard and both gamepads, found once per fixed step by comparing held
    /// buttons with the previous step, with key repeat on the arrows. Arrows or WASD move, enter or
    /// space picks, escape or backspace goes back, escape or P pauses; on a pad the d-pad or left
    /// stick moves, A picks, B goes back and start pauses.
    /// A <see cref="MenuScript"/> presses scripted buttons instead.</summary>
    internal sealed class MenuInput
    {
        private const float RepeatDelay = 0.35f,
            RepeatEvery = 0.1f,
            StickDeadZone = 0.5f;

        private static readonly ImmutableArray<MenuButton> Repeats =
        [
            MenuButton.Up,
            MenuButton.Down,
            MenuButton.Left,
            MenuButton.Right,
        ];

        private readonly HashSet<MenuButton> last = [];
        private readonly HashSet<MenuButton> pressed = [];
        private float heldFor;

        /// <summary>Finds this step's presses.</summary>
        /// <param name="script">The scripted presses, when SAP_SCREEN_KEYS set them.</param>
        /// <param name="settings">The settings that bind the pause button.</param>
        /// <param name="menuUp">Whether a menu is showing; while a match runs only a scripted "pause" is taken.</param>
        /// <param name="dt">The step length.</param>
        /// <returns>The buttons pressed this step (escape is both back and pause), usually none.</returns>
        public IReadOnlySet<MenuButton> Next(
            MenuScript script,
            Settings settings,
            bool menuUp,
            float dt
        )
        {
            pressed.Clear();
            if (script.IsScripted)
            {
                if (script.Next(menuUp) is var b and not MenuButton.None)
                {
                    _ = pressed.Add(b);
                    DeviceInput.Scripted(b.ToString()); // the replay presses it on the same step
                }
                return pressed;
            }
            if (
                DeviceInput.Current.MenuPress.Length > 0
                && Enum.TryParse(DeviceInput.Current.MenuPress, out MenuButton played)
            )
            {
                _ = pressed.Add(played); // a scripted press, played back from a replay
                return pressed;
            }

            HashSet<MenuButton> held = Held(settings);
            foreach (MenuButton b in held.Where(b => !last.Contains(b)))
            {
                _ = pressed.Add(b);
            }

            // the arrows repeat while held
            MenuButton arrow = Repeats.FirstOrDefault(held.Contains);
            if (arrow == MenuButton.None || pressed.Contains(arrow))
            {
                heldFor = 0;
            }
            else
            {
                float before = heldFor;
                heldFor += dt;
                if (
                    heldFor >= RepeatDelay
                    && (
                        before < RepeatDelay
                        || (int)((heldFor - RepeatDelay) / RepeatEvery)
                            != (int)((before - RepeatDelay) / RepeatEvery)
                    )
                )
                {
                    _ = pressed.Add(arrow);
                }
            }
            last.Clear();
            last.UnionWith(held);
            return pressed;
        }

        /// <summary>Forgets held buttons, so a button still down from the last screen does not press again.</summary>
        public void Swallow(MenuScript script, Settings settings)
        {
            if (script.IsScripted)
            {
                return;
            }

            last.Clear();
            last.UnionWith(Held(settings));
            heldFor = 0;
        }

        private static HashSet<MenuButton> Held(Settings settings)
        {
            HashSet<MenuButton> held = [];
            AddKeyboard(held, settings);
            AddPads(held, settings);
            return held;
        }

        private static void AddKeyboard(HashSet<MenuButton> held, Settings settings)
        {
            Add(
                held,
                MenuButton.Up,
                DeviceInput.KeyDown(KeyboardKey.Up) || DeviceInput.KeyDown(KeyboardKey.W)
            );
            Add(
                held,
                MenuButton.Down,
                DeviceInput.KeyDown(KeyboardKey.Down) || DeviceInput.KeyDown(KeyboardKey.S)
            );
            Add(
                held,
                MenuButton.Left,
                DeviceInput.KeyDown(KeyboardKey.Left) || DeviceInput.KeyDown(KeyboardKey.A)
            );
            Add(
                held,
                MenuButton.Right,
                DeviceInput.KeyDown(KeyboardKey.Right) || DeviceInput.KeyDown(KeyboardKey.D)
            );
            Add(
                held,
                MenuButton.Accept,
                DeviceInput.KeyDown(KeyboardKey.Enter)
                    || DeviceInput.KeyDown(KeyboardKey.KpEnter)
                    || DeviceInput.KeyDown(KeyboardKey.Space)
            );
            Add(
                held,
                MenuButton.Back,
                DeviceInput.KeyDown(KeyboardKey.Escape)
                    || DeviceInput.KeyDown(KeyboardKey.Backspace)
            );
            Add(
                held,
                MenuButton.Pause,
                DeviceInput.KeyDown(KeyboardKey.Escape)
                    || Players.Input.KeyInput.KeyDown(settings, GameAction.Pause)
            );
        }

        private static void AddPads(HashSet<MenuButton> held, Settings settings)
        {
            for (int pad = 0; pad < 2; pad++)
            {
                if (!DeviceInput.PadAvailable(pad))
                {
                    continue;
                }

                float x = DeviceInput.PadAxis(pad, GamepadAxis.LeftX),
                    y = DeviceInput.PadAxis(pad, GamepadAxis.LeftY);
                Add(
                    held,
                    MenuButton.Up,
                    DeviceInput.PadDown(pad, GamepadButton.LeftFaceUp) || y < -StickDeadZone
                );
                Add(
                    held,
                    MenuButton.Down,
                    DeviceInput.PadDown(pad, GamepadButton.LeftFaceDown) || y > StickDeadZone
                );
                Add(
                    held,
                    MenuButton.Left,
                    DeviceInput.PadDown(pad, GamepadButton.LeftFaceLeft) || x < -StickDeadZone
                );
                Add(
                    held,
                    MenuButton.Right,
                    DeviceInput.PadDown(pad, GamepadButton.LeftFaceRight) || x > StickDeadZone
                );
                Add(held, MenuButton.Accept, DeviceInput.PadDown(pad, GamepadButton.RightFaceDown));
                Add(held, MenuButton.Back, DeviceInput.PadDown(pad, GamepadButton.RightFaceRight));
                Add(
                    held,
                    MenuButton.Pause,
                    Players.Input.KeyInput.PadDown(settings, GameAction.Pause, pad)
                );
            }
        }

        private static void Add(HashSet<MenuButton> held, MenuButton button, bool down)
        {
            if (down)
            {
                _ = held.Add(button);
            }
        }
    }
}
