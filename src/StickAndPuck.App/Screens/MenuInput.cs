using Raylib_cs;
using StickAndPuck.App.Core;
using System.Collections.Immutable;

namespace StickAndPuck.App.Screens;

internal enum MenuButton
{
    None,
    Up,
    Down,
    Left,
    Right,
    Accept,
    Back,
    Pause,
}

/// <summary>Menu presses from the keyboard and both gamepads, found once per fixed step by comparing held
/// buttons with the previous step, with key repeat on the arrows. Arrows or WASD move, enter or
/// space picks, escape or backspace goes back, escape or P pauses; on a pad the d-pad or left
/// stick moves, A picks, B goes back and start pauses.
/// A <see cref="MenuScript"/> presses scripted buttons instead.</summary>
internal sealed class MenuInput
{
    const float RepeatDelay = 0.35f, RepeatEvery = 0.1f, StickDeadZone = 0.5f;

    static readonly ImmutableArray<MenuButton> Repeats = [MenuButton.Up, MenuButton.Down, MenuButton.Left, MenuButton.Right];

    readonly HashSet<MenuButton> last = [];
    readonly HashSet<MenuButton> pressed = [];
    float heldFor;

    /// <summary>Finds this step's presses.</summary>
    /// <param name="script">The scripted presses, when SAP_SCREEN_KEYS set them.</param>
    /// <param name="settings">The settings that bind the pause button.</param>
    /// <param name="menuUp">Whether a menu is showing; while a match runs only a scripted "pause" is taken.</param>
    /// <param name="dt">The step length.</param>
    /// <returns>The buttons pressed this step (escape is both back and pause), usually none.</returns>
    public IReadOnlySet<MenuButton> Next(MenuScript script, Settings settings, bool menuUp, float dt)
    {
        pressed.Clear();
        if (script.IsScripted)
        {
            if (script.Next(menuUp) is var b and not MenuButton.None)
            {
                pressed.Add(b);
                DeviceInput.Scripted(b.ToString());    // the replay presses it on the same step
            }
            return pressed;
        }
        if (DeviceInput.Current.MenuPress.Length > 0 && Enum.TryParse<MenuButton>(DeviceInput.Current.MenuPress, out var played))
        {
            pressed.Add(played);    // a scripted press, played back from a replay
            return pressed;
        }

        var held = Held(settings);
        foreach (var b in held)
            if (!last.Contains(b)) pressed.Add(b);

        // the arrows repeat while held
        var arrow = Repeats.FirstOrDefault(held.Contains);
        if (arrow == MenuButton.None || pressed.Contains(arrow))
        {
            heldFor = 0;
        }
        else
        {
            float before = heldFor;
            heldFor += dt;
            if (heldFor >= RepeatDelay && (before < RepeatDelay || (int)((heldFor - RepeatDelay) / RepeatEvery) != (int)((before - RepeatDelay) / RepeatEvery)))
                pressed.Add(arrow);
        }
        last.Clear();
        last.UnionWith(held);
        return pressed;
    }

    /// <summary>Forgets held buttons, so a button still down from the last screen does not press again.</summary>
    public void Swallow(MenuScript script, Settings settings)
    {
        if (script.IsScripted) return;
        last.Clear();
        last.UnionWith(Held(settings));
        heldFor = 0;
    }

    static HashSet<MenuButton> Held(Settings settings)
    {
        var held = new HashSet<MenuButton>();
        void Add(MenuButton b, bool down)
        {
            if (down) held.Add(b);
        }

        Add(MenuButton.Up, DeviceInput.KeyDown(KeyboardKey.Up) || DeviceInput.KeyDown(KeyboardKey.W));
        Add(MenuButton.Down, DeviceInput.KeyDown(KeyboardKey.Down) || DeviceInput.KeyDown(KeyboardKey.S));
        Add(MenuButton.Left, DeviceInput.KeyDown(KeyboardKey.Left) || DeviceInput.KeyDown(KeyboardKey.A));
        Add(MenuButton.Right, DeviceInput.KeyDown(KeyboardKey.Right) || DeviceInput.KeyDown(KeyboardKey.D));
        Add(MenuButton.Accept, DeviceInput.KeyDown(KeyboardKey.Enter) || DeviceInput.KeyDown(KeyboardKey.KpEnter) || DeviceInput.KeyDown(KeyboardKey.Space));
        Add(MenuButton.Back, DeviceInput.KeyDown(KeyboardKey.Escape) || DeviceInput.KeyDown(KeyboardKey.Backspace));
        Add(MenuButton.Pause, DeviceInput.KeyDown(KeyboardKey.Escape) || Players.KeyInput.KeyDown(settings, GameAction.Pause));
        for (int pad = 0; pad < 2; pad++)
        {
            if (!DeviceInput.PadAvailable(pad)) continue;
            float x = DeviceInput.PadAxis(pad, GamepadAxis.LeftX), y = DeviceInput.PadAxis(pad, GamepadAxis.LeftY);
            Add(MenuButton.Up, DeviceInput.PadDown(pad, GamepadButton.LeftFaceUp) || y < -StickDeadZone);
            Add(MenuButton.Down, DeviceInput.PadDown(pad, GamepadButton.LeftFaceDown) || y > StickDeadZone);
            Add(MenuButton.Left, DeviceInput.PadDown(pad, GamepadButton.LeftFaceLeft) || x < -StickDeadZone);
            Add(MenuButton.Right, DeviceInput.PadDown(pad, GamepadButton.LeftFaceRight) || x > StickDeadZone);
            Add(MenuButton.Accept, DeviceInput.PadDown(pad, GamepadButton.RightFaceDown));
            Add(MenuButton.Back, DeviceInput.PadDown(pad, GamepadButton.RightFaceRight));
            Add(MenuButton.Pause, Players.KeyInput.PadDown(settings, GameAction.Pause, pad));
        }
        return held;
    }
}
