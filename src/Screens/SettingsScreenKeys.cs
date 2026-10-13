using Raylib_cs;
using StickAndPuck.Core;

namespace StickAndPuck.Screens;

/// <summary>
/// Key mapping (W39), opened from the settings screen: one row per <see cref="GameAction"/> with
/// its keyboard keys and gamepad button. Enter on a row waits for the next key or pad button; a key
/// replaces the row's keys, a button its pad button, and escape cancels. Reset to defaults clears
/// every rebinding. Changes apply at once and are saved with the settings on leaving.
/// </summary>
internal sealed class KeyMappingScreen : MenuScreen
{
    const int PanelW = 330, PanelTop = 8, Visible = 13;

    static readonly KeyboardKey[] AllKeys = [.. Enum.GetValues<KeyboardKey>().Where(k => k != KeyboardKey.Null)];
    static readonly GamepadButton[] AllButtons = [.. Enum.GetValues<GamepadButton>().Where(b => b != GamepadButton.Unknown)];

    readonly HashSet<KeyboardKey> heldKeys = [];
    readonly HashSet<(int Pad, GamepadButton Button)> heldButtons = [];
    World? world;
    GameAction? listening;
    int first;

    static Settings Saved => ScreenFlow.Prefs.Settings;

    /// <inheritdoc/>
    public override IReadOnlyList<MenuItem> Items()
    {
        var items = new List<MenuItem>();
        foreach (var action in KeyBindings.All)
            items.Add(new MenuItem(Label(action), () => Listen(action)));
        items.Add(new MenuItem("RESET TO DEFAULTS", Reset, Enabled: Saved.Keys.Count > 0));
        items.Add(new MenuItem("BACK", Back));
        return items;
    }

    /// <inheritdoc/>
    public override void Update(World world, IReadOnlySet<MenuButton> press)
    {
        this.world = world;
        world.Match.Paused = true;
        if (listening is { } action) Capture(action);
        else base.Update(world, press);
    }

    /// <inheritdoc/>
    public override void Draw(ScreenArt art, World world)
    {
        var items = Items();
        int shown = Math.Min(Visible, items.Count);
        if (Selected < first) first = Selected;
        if (Selected >= first + shown) first = Selected - shown + 1;
        first = Math.Clamp(first, 0, Math.Max(0, items.Count - shown));

        var (x, top) = art.Panel("KEY MAPPING", PanelW, ((shown + 2) * ScreenArt.Row) + 30, PanelTop);
        int keysX = x + 150, right = x + PanelW - 12;
        art.Left("KEYBOARD", keysX, top, ScreenArt.Muted);
        art.Right("GAMEPAD", right, top, ScreenArt.Muted);
        top += ScreenArt.Row;
        if (first > 0) art.Centred("- MORE -", Game.Width / 2, top - 3, ScreenArt.Muted);
        top += ScreenArt.Row / 2;
        var settings = Saved;
        for (int i = first; i < first + shown; i++)
        {
            var item = items[i];
            int y = top + ((i - first) * ScreenArt.Row);
            bool on = i == Selected;
            var ink = !item.Enabled ? ScreenArt.Muted : on ? art.Gold : art.Text;
            if (on) art.Hud.DrawIcon("arrow_right", x + 6, y + 2);
            art.Left(item.Label, x + 18, y, ink);
            if (i >= KeyBindings.All.Count) continue;
            var binding = KeyBindings.Of(settings, KeyBindings.All[i]);
            if (on && listening != null)
            {
                art.Left("PRESS A KEY", keysX, y, art.Gold);
                art.Right("OR BUTTON", right, y, art.Gold);
                continue;
            }
            art.Left(KeyBindings.KeysName(binding), keysX, y, ink);
            art.Right(KeyBindings.ButtonName(binding.Button), right, y, ink);
        }
        if (first + shown < items.Count) art.Centred("- MORE -", Game.Width / 2, top + (shown * ScreenArt.Row) + 1, ScreenArt.Muted);
        art.Hint(listening != null ? "PRESS A KEY OR PAD BUTTON   ESC CANCELS" : "ENTER REBINDS   ESC BACK");
    }

    /// <inheritdoc/>
    protected override void Back()
    {
        if (world != null) ScreenFlow.ApplySettings(world);
        Console.WriteLine($"SAP: screens: key mapping saved, {Saved.Keys.Count} rebound");
        ScreenFlow.Pop();
    }

    static string Label(GameAction action) => action switch
    {
        GameAction.SkateUp => "SKATE UP",
        GameAction.SkateDown => "SKATE DOWN",
        GameAction.SkateLeft => "SKATE LEFT",
        GameAction.SkateRight => "SKATE RIGHT",
        GameAction.LineChange => "LINE CHANGE",
        GameAction.AcceptFight => "ACCEPT FIGHT",
        GameAction.Kit => "SWAP KITS",
        GameAction.Zoom => "CAMERA ZOOM",
        _ => action.ToString().ToUpperInvariant(),
    };

    /// <summary>Starts waiting for a key, ignoring what is already held (the enter that picked the row).</summary>
    void Listen(GameAction action)
    {
        listening = action;
        heldKeys.Clear();
        heldKeys.UnionWith(AllKeys.Where(k => DeviceInput.KeyDown(k)));
        heldButtons.Clear();
        heldButtons.UnionWith(Buttons());
    }

    /// <summary>Binds the first key or pad button that goes down; escape cancels.</summary>
    void Capture(GameAction action)
    {
        if (MenuInput.IsScripted)
        {
            listening = null;    // a headless script cannot press a key, so the row only shows it waits
            return;
        }
        foreach (var key in AllKeys)
        {
            if (!DeviceInput.KeyDown(key))
            {
                heldKeys.Remove(key);
                continue;
            }
            if (!heldKeys.Add(key)) continue;
            if (key != KeyboardKey.Escape) Set(KeyBindings.WithKey(Saved, action, key));
            Done();
            return;
        }
        var now = Buttons();
        heldButtons.IntersectWith(now);
        foreach (var b in now)
        {
            if (!heldButtons.Add(b)) continue;
            Set(KeyBindings.WithButton(Saved, action, b.Button));
            Done();
            return;
        }
    }

    /// <summary>Stops waiting. The menu input saw the key go down on this same step, so it is not taken as a press later.</summary>
    void Done()
    {
        listening = null;
    }

    static List<(int Pad, GamepadButton Button)> Buttons()
    {
        var down = new List<(int, GamepadButton)>();
        for (int pad = 0; pad < 2; pad++)
        {
            if (!DeviceInput.PadAvailable(pad)) continue;
            foreach (var b in AllButtons)
                if (DeviceInput.PadDown(pad, b)) down.Add((pad, b));
        }
        return down;
    }

    void Reset()
    {
        Set(Saved with { Keys = new Dictionary<GameAction, KeyBinding>() });
        Console.WriteLine("SAP: screens: key mapping reset to defaults");
    }

    void Set(Settings settings)
    {
        ScreenFlow.Prefs = ScreenFlow.Prefs with { Settings = settings };
        if (world != null) ScreenFlow.Apply(world, ScreenFlow.Settings);
    }
}
