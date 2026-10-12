using System.Globalization;
using StickAndPuck.Core;

namespace StickAndPuck.Screens;

/// <summary>
/// Settings: every switch in one place (W20). Game: control switching, line changes, difficulty,
/// period length, sim game, auto deke, one-timers, penalties, fights. Controls: player one's and
/// player two's devices, the control marker. Display and sound: camera zoom, lighting mood, swap
/// kits, volume. Debug: the fps line, the match line, the lines panel. Left and right change a
/// value; it applies to the world on show at once (so a paused match plays on with it) and every
/// later match, and is saved on leaving. The list scrolls when it is taller than the frame. A
/// running match stays paused while this screen is up (<see cref="ScreenSystem"/> holds every
/// match under a screen).
/// </summary>
internal sealed class SettingsScreen : MenuScreen
{
    const int PanelW = 300, PanelTop = 8;

    /// <summary>Rows shown at once; the rest scroll into view.</summary>
    const int Visible = 14;

    static readonly InputDevice[] Devices = [InputDevice.KeyboardMouse, InputDevice.Gamepad1];
    static readonly InputDevice[] Player2Devices = [InputDevice.None, InputDevice.KeyboardMouse, InputDevice.Gamepad1, InputDevice.Gamepad2];

    World? world;
    int first;

    static ScreenPrefs Prefs => ScreenFlow.Prefs;

    /// <inheritdoc/>
    public override IReadOnlyList<MenuItem> Items()
    {
        var s = Prefs.Settings;
        return
        [
            new MenuItem("CONTROL SWITCHING", Value: s.Switching switch
            {
                ControlSwitching.Auto => "AUTO",
                ControlSwitching.PassesOnly => "PASSES ONLY",
                _ => "MANUAL",
            }, Change: d => Set(s with { Switching = Cycle(s.Switching, d) })),
            new MenuItem("LINE CHANGES", Value: s.Lines == LineChanges.Automatic ? "AUTOMATIC" : "MANUAL",
                Change: d => Set(s with { Lines = Cycle(s.Lines, d) })),
            new MenuItem("DIFFICULTY", Value: s.Difficulty.ToString().ToUpperInvariant(), Change: d => Set(s with { Difficulty = Cycle(s.Difficulty, d) })),
            new MenuItem("PERIOD LENGTH", Value: $"{Prefs.PeriodMinutes.ToString(CultureInfo.InvariantCulture)} MIN", Change: Period),
            new MenuItem("SIM THIS GAME", Value: OnOff(s.SimGame), Change: _ => Set(s with { SimGame = !s.SimGame })),
            new MenuItem("AUTO DEKE", Value: OnOff(s.AutoDeke), Change: _ => Set(s with { AutoDeke = !s.AutoDeke })),
            new MenuItem("ONE-TIMERS", Value: OnOff(s.OneTimers), Change: _ => Set(s with { OneTimers = !s.OneTimers })),
            new MenuItem("PENALTIES", Value: OnOff(s.Penalties), Change: _ => Set(s with { Penalties = !s.Penalties })),
            new MenuItem("FIGHTS", Value: OnOff(s.Fights), Change: _ => Set(s with { Fights = !s.Fights })),
            new MenuItem("CONTROLS", Value: Device(Prefs.Controls), Change: Controls),
            new MenuItem("PLAYER 2 CONTROLS", Value: Device(Prefs.Player2), Change: Player2),
            new MenuItem("CONTROL MARKER", Value: OnOff(s.ControlMarker), Change: _ => Set(s with { ControlMarker = !s.ControlMarker })),
            new MenuItem("CAMERA ZOOM", Value: OnOff(s.CameraZoom), Change: _ => Set(s with { CameraZoom = !s.CameraZoom })),
            new MenuItem("LIGHTING", Value: s.Lighting.ToString().ToUpperInvariant(), Change: d => Set(s with { Lighting = Cycle(s.Lighting, d) })),
            new MenuItem("SWAP KITS", Value: OnOff(s.SwapKits), Change: _ => Set(s with { SwapKits = !s.SwapKits })),
            new MenuItem("VOLUME", Value: ((int)MathF.Round(s.Volume * 10)).ToString(CultureInfo.InvariantCulture), Change: Volume),
            new MenuItem("FPS LINE", Value: OnOff(s.FpsLine), Change: _ => Set(s with { FpsLine = !s.FpsLine })),
            new MenuItem("MATCH DEBUG LINE", Value: OnOff(s.MatchDebug), Change: _ => Set(s with { MatchDebug = !s.MatchDebug })),
            new MenuItem("LINES PANEL", Value: OnOff(s.LinesDebug), Change: _ => Set(s with { LinesDebug = !s.LinesDebug })),
            new MenuItem("BACK", Back),
        ];
    }

    /// <inheritdoc/>
    public override void Update(World world, IReadOnlySet<MenuButton> press)
    {
        this.world = world;
        world.Match.Paused = true;    // W20: a running match waits while the settings are open
        base.Update(world, press);
    }

    /// <inheritdoc/>
    public override void Draw(ScreenArt art, World world)
    {
        var items = Items();
        int shown = Math.Min(Visible, items.Count);
        if (Selected < first) first = Selected;
        if (Selected >= first + shown) first = Selected - shown + 1;
        first = Math.Clamp(first, 0, Math.Max(0, items.Count - shown));

        // a marker row above and below the list says there is more to scroll to
        var (x, top) = art.Panel("SETTINGS", PanelW, ((shown + 1) * ScreenArt.Row) + 30, PanelTop);
        if (first > 0) art.Centred("- MORE -", Game.Width / 2, top - 3, ScreenArt.Muted);
        top += ScreenArt.Row / 2;
        art.Menu(items.Skip(first).Take(shown).ToList(), Selected - first, x, PanelW, top);
        if (first + shown < items.Count) art.Centred("- MORE -", Game.Width / 2, top + (shown * ScreenArt.Row) + 1, ScreenArt.Muted);
        art.Hint("LEFT RIGHT CHANGES   ESC BACK");
    }

    /// <inheritdoc/>
    protected override void Back()
    {
        if (world != null) ScreenFlow.ApplySettings(world);
        Console.WriteLine($"SAP: screens: settings saved, {Prefs}");
        ScreenFlow.Pop();
    }

    static string OnOff(bool on) => on ? "ON" : "OFF";

    static string Device(InputDevice device) => Players.Gamepads.Label(device);    // W38: the pad by name

    static T Cycle<T>(T value, int dir)
        where T : struct, Enum
    {
        var all = Enum.GetValues<T>();
        return Cycle(all, value, dir);
    }

    static T Cycle<T>(T[] all, T value, int dir)
    {
        int i = Array.IndexOf(all, value);
        return all[((Math.Max(i, 0) + dir) % all.Length + all.Length) % all.Length];
    }

    void Set(Settings settings)
    {
        ScreenFlow.Prefs = Prefs with { Settings = settings };
        if (world != null) ScreenFlow.Apply(world, ScreenFlow.Settings);
    }

    static void Period(int dir)
    {
        var choices = ScreenPrefs.PeriodChoices;
        int i = Array.IndexOf(choices, Prefs.PeriodMinutes);
        i = i < 0 ? 2 : Math.Clamp(i + dir, 0, choices.Length - 1);
        ScreenFlow.Prefs = Prefs with { PeriodMinutes = choices[i] };
    }

    static void Controls(int dir)
    {
        ScreenFlow.Prefs = Prefs with { Controls = Cycle(Devices, Prefs.Controls, dir) };
    }

    static void Player2(int dir)
    {
        ScreenFlow.Prefs = Prefs with { Player2 = Cycle(Player2Devices, Prefs.Player2, dir) };
    }

    void Volume(int dir)
    {
        var s = Prefs.Settings;
        float v = Math.Clamp(MathF.Round((s.Volume * 10) + dir) / 10f, 0, 1);
        Set(s with { Volume = v });
    }
}
