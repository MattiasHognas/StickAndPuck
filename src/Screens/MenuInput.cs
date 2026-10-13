using Raylib_cs;
using StickAndPuck.Core;

namespace StickAndPuck.Screens;

/// <summary>A menu button.</summary>
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

/// <summary>
/// Menu presses from the keyboard and both gamepads, found once per fixed step by comparing held
/// buttons with the previous step, with key repeat on the arrows. Arrows or WASD move, enter or
/// space picks, escape or backspace goes back, escape or P pauses; on a pad the d-pad or left
/// stick moves, A picks, B goes back and start pauses.
/// SAP_SCREEN_KEYS="down enter ..." presses scripted buttons instead (one every
/// <see cref="ScriptGap"/> steps while a menu is up), for headless runs through the screens;
/// "shot:name" saves name.png of the last frame and "wait" does nothing for one gap.
/// </summary>
internal sealed class MenuInput
{
    const float RepeatDelay = 0.35f, RepeatEvery = 0.1f, StickDeadZone = 0.5f;

    /// <summary>Steps between scripted presses.</summary>
    public const int ScriptGap = 20;

    static Queue<string>? script;
    static int scriptWait;

    static readonly MenuButton[] Repeats = [MenuButton.Up, MenuButton.Down, MenuButton.Left, MenuButton.Right];

    readonly HashSet<MenuButton> last = [];
    readonly HashSet<MenuButton> pressed = [];
    float heldFor;

    /// <summary>Gets a value indicating whether a script drives the menus.</summary>
    public static bool IsScripted => script != null;

    /// <summary>Reads SAP_SCREEN_KEYS once.</summary>
    public static void LoadScript()
    {
        if (script != null || Environment.GetEnvironmentVariable("SAP_SCREEN_KEYS") is not { Length: > 0 } keys) return;
        script = new Queue<string>(keys.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries));
        Console.WriteLine($"SAP: screens: {script.Count} scripted presses");
    }

    /// <summary>Finds this step's presses.</summary>
    /// <param name="menuUp">Whether a menu is showing; while a match runs only a scripted "pause" is taken.</param>
    /// <param name="dt">The step length.</param>
    /// <returns>The buttons pressed this step (escape is both back and pause), usually none.</returns>
    public IReadOnlySet<MenuButton> Next(bool menuUp, float dt)
    {
        pressed.Clear();
        if (script != null)
        {
            if (Scripted(menuUp) is var b and not MenuButton.None)
            {
                pressed.Add(b);
                DeviceInput.Scripted(b.ToString());    // W67: the replay presses it on the same step
            }
            return pressed;
        }
        if (DeviceInput.Current.MenuPress.Length > 0 && Enum.TryParse<MenuButton>(DeviceInput.Current.MenuPress, out var played))
        {
            pressed.Add(played);    // a scripted press, played back from a replay
            return pressed;
        }

        var held = Held();
        foreach (var b in held)
            if (!last.Contains(b)) pressed.Add(b);

        // the arrows repeat while held
        var arrow = Array.Find(Repeats, held.Contains);
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
    public void Swallow()
    {
        if (script != null) return;
        last.Clear();
        last.UnionWith(Held());
        heldFor = 0;
    }

    static MenuButton Scripted(bool menuUp)
    {
        if (script!.Count == 0) return MenuButton.None;
        string token = script.Peek();
        if (!menuUp && !string.Equals(token, "pause", StringComparison.OrdinalIgnoreCase))
        {
            scriptWait = 0;    // the gap counts from when a menu shows, so it has been drawn before a shot
            return MenuButton.None;
        }
        if (++scriptWait < ScriptGap) return MenuButton.None;
        script.Dequeue();
        scriptWait = 0;
        Console.WriteLine($"SAP: screens: scripted {token}");
        if (token.StartsWith("shot:", StringComparison.OrdinalIgnoreCase))
        {
            AssetRoot.SaveScreen(token[5..] + ".png");
            return MenuButton.None;
        }
        return token.ToLowerInvariant() switch
        {
            "up" => MenuButton.Up,
            "down" => MenuButton.Down,
            "left" => MenuButton.Left,
            "right" => MenuButton.Right,
            "enter" => MenuButton.Accept,
            "back" => MenuButton.Back,
            "pause" => MenuButton.Pause,
            _ => MenuButton.None,
        };
    }

    static HashSet<MenuButton> Held()
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
        Add(MenuButton.Pause, DeviceInput.KeyDown(KeyboardKey.Escape) || Players.KeyInput.KeyDown(ScreenFlow.Settings, GameAction.Pause));
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
            Add(MenuButton.Pause, Players.KeyInput.PadDown(ScreenFlow.Settings, GameAction.Pause, pad));
        }
        return held;
    }
}
