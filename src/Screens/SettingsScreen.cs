using System.Globalization;
using StickAndPuck.Core;

namespace StickAndPuck.Screens;

/// <summary>
/// Settings: control switching, line changes, period length, controls, camera zoom, penalties,
/// fights and volume. Left and right change a value; it applies to the world on show at once
/// (so a paused match plays on with it) and every later match, and is saved on leaving.
/// </summary>
internal sealed class SettingsScreen : MenuScreen
{
    const int PanelW = 300;

    static readonly InputDevice[] Devices = [InputDevice.KeyboardMouse, InputDevice.Gamepad1];

    World? world;

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
            new MenuItem("PERIOD LENGTH", Value: $"{Prefs.PeriodMinutes.ToString(CultureInfo.InvariantCulture)} MIN", Change: Period),
            new MenuItem("CONTROLS", Value: Prefs.Controls == InputDevice.Gamepad1 ? "GAMEPAD" : "KEYBOARD", Change: Controls),
            new MenuItem("CAMERA ZOOM", Value: OnOff(s.CameraZoom), Change: _ => Set(s with { CameraZoom = !s.CameraZoom })),
            new MenuItem("PENALTIES", Value: OnOff(s.Penalties), Change: _ => Set(s with { Penalties = !s.Penalties })),
            new MenuItem("FIGHTS", Value: OnOff(s.Fights), Change: _ => Set(s with { Fights = !s.Fights })),
            new MenuItem("VOLUME", Value: ((int)MathF.Round(s.Volume * 10)).ToString(CultureInfo.InvariantCulture), Change: Volume),
            new MenuItem("BACK", Back),
        ];
    }

    /// <inheritdoc/>
    public override void Update(World world, IReadOnlySet<MenuButton> press)
    {
        this.world = world;
        base.Update(world, press);
    }

    /// <inheritdoc/>
    public override void Draw(ScreenArt art, World world)
    {
        var items = Items();
        var (x, top) = art.Panel("SETTINGS", PanelW, (items.Count * ScreenArt.Row) + 30, 40);
        art.Menu(items, Selected, x, PanelW, top);
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

    static T Cycle<T>(T value, int dir)
        where T : struct, Enum
    {
        var all = Enum.GetValues<T>();
        int i = Array.IndexOf(all, value);
        return all[((i + dir) % all.Length + all.Length) % all.Length];
    }

    void Set(Settings settings)
    {
        ScreenFlow.Prefs = Prefs with { Settings = settings };
        if (world != null) world.Settings = settings;
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
        int i = Array.IndexOf(Devices, Prefs.Controls);
        ScreenFlow.Prefs = Prefs with { Controls = Devices[((Math.Max(i, 0) + dir) % Devices.Length + Devices.Length) % Devices.Length] };
    }

    void Volume(int dir)
    {
        var s = Prefs.Settings;
        float v = Math.Clamp(MathF.Round((s.Volume * 10) + dir) / 10f, 0, 1);
        Set(s with { Volume = v });
    }
}
