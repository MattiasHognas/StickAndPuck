using System.Globalization;
using StickAndPuck.App.Core;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens;

/// <summary>Settings: every switch in one place. The rules page first (the preset and every
/// rule, penalties and fights among them). Game: control switching, line changes, difficulty,
/// period length, sim game, auto deke, one-timers. Controls: player one's and
/// player two's devices, the key mapping page, the control marker. Display and sound: camera zoom, lighting mood, swap
/// kits, volume, language. Debug: the fps line, the match line, the lines panel. Left and right change a
/// value; it applies to the world on show at once (so a paused match plays on with it) and every
/// later match, and is saved on leaving. The list scrolls when it is taller than the frame. A
/// running match stays paused while this screen is up (<see cref="ScreenSystem"/> holds every
/// match under a screen).</summary>
internal sealed class SettingsScreen : MenuScreen
{
    const int PanelW = 300, PanelTop = 8;

    /// <summary>Rows shown at once; the rest scroll into view.</summary>
    const int Visible = 14;

    static readonly InputDevice[] Devices = [InputDevice.KeyboardMouse, InputDevice.Gamepad1];
    static readonly InputDevice[] Player2Devices = [InputDevice.None, InputDevice.KeyboardMouse, InputDevice.Gamepad1, InputDevice.Gamepad2];

    Maybe<World> world;
    int first;

    static ScreenPrefs Prefs => ScreenFlow.Prefs;

    /// <inheritdoc/>
    public override IReadOnlyList<MenuItem> Items()
    {
        var s = Prefs.Settings;
        return
        [
            new MenuItem(Text.Get(K.Rules), () => ScreenFlow.Push(new RulesScreen()), value: Prefs.Rules.Resolve().Name),
            new MenuItem(Text.Get(K.ControlSwitching), value: Text.Get(s.Switching switch
            {
                ControlSwitching.Auto => K.SwitchAuto,
                ControlSwitching.PassesOnly => K.SwitchPassesOnly,
                _ => K.SwitchManual,
            }), change: d => Set(s with { Switching = Cycle(s.Switching, d) })),
            new MenuItem(Text.Get(K.LineChanges), value: Text.Get(s.Lines == LineChanges.Automatic ? K.LinesAutomatic : K.LinesManual),
                change: d => Set(s with { Lines = Cycle(s.Lines, d) })),
            new MenuItem(Text.Get(K.Difficulty), value: Text.Get(s.Difficulty switch { Difficulty.Easy => K.DifficultyEasy, Difficulty.Hard => K.DifficultyHard, _ => K.DifficultyNormal }), change: d => Set(s with { Difficulty = Cycle(s.Difficulty, d) })),
            new MenuItem(Text.Get(K.PeriodLength), value: Text.Format(K.PeriodMinutes, Prefs.PeriodMinutes), change: Period),
            new MenuItem(Text.Get(K.SimThisGame), value: OnOff(s.SimGame), change: _ => Set(s with { SimGame = !s.SimGame })),
            new MenuItem(Text.Get(K.AutoDeke), value: OnOff(s.AutoDeke), change: _ => Set(s with { AutoDeke = !s.AutoDeke })),
            new MenuItem(Text.Get(K.OneTimers), value: OnOff(s.OneTimers), change: _ => Set(s with { OneTimers = !s.OneTimers })),
            new MenuItem(Text.Get(K.Controls), value: Device(Prefs.Controls), change: Controls),
            new MenuItem(Text.Get(K.Player2Controls), value: Device(Prefs.Player2), change: Player2),
            new MenuItem(Text.Get(K.KeyMapping), () => ScreenFlow.Push(new KeyMappingScreen())),
            new MenuItem(Text.Get(K.ControlMarker), value: OnOff(s.ControlMarker), change: _ => Set(s with { ControlMarker = !s.ControlMarker })),
            new MenuItem(Text.Get(K.CameraZoom), value: OnOff(s.CameraZoom), change: _ => Set(s with { CameraZoom = !s.CameraZoom })),
            new MenuItem(Text.Get(K.Lighting), value: Text.Get(s.Lighting switch { LightingMood.Early => K.LightingEarly, LightingMood.Afternoon => K.LightingAfternoon, LightingMood.Late => K.LightingLate, _ => K.LightingRandom }), change: d => Set(s with { Lighting = Cycle(s.Lighting, d) })),
            new MenuItem(Text.Get(K.SwapKits), value: OnOff(s.SwapKits), change: _ => Set(s with { SwapKits = !s.SwapKits })),
            new MenuItem(Text.Get(K.Volume), value: ((int)MathF.Round(s.Volume * 10)).ToString(CultureInfo.InvariantCulture), change: Volume),
            new MenuItem(Text.Get(K.Language), value: Text.NameOf(s.Language), change: d => Set(s with { Language = Cycle(s.Language, d) })),
            new MenuItem(Text.Get(K.FpsLine), value: OnOff(s.FpsLine), change: _ => Set(s with { FpsLine = !s.FpsLine })),
            new MenuItem(Text.Get(K.MatchDebugLine), value: OnOff(s.MatchDebug), change: _ => Set(s with { MatchDebug = !s.MatchDebug })),
            new MenuItem(Text.Get(K.LinesPanel), value: OnOff(s.LinesDebug), change: _ => Set(s with { LinesDebug = !s.LinesDebug })),
            new MenuItem(Text.Get(K.Back), Back),
        ];
    }

    /// <inheritdoc/>
    public override void Update(World world, IReadOnlySet<MenuButton> press)
    {
        this.world = world;
        world.Match.Paused = true;    // a running match waits while the settings are open
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
        var (x, top) = art.Panel(Text.Get(K.Settings), PanelW, ((shown + 1) * ScreenArt.Row) + 30, PanelTop);
        if (first > 0) art.Centred(Text.Get(K.More), Game.Width / 2, top - 3, ScreenArt.Muted);
        top += ScreenArt.Row / 2;
        art.Menu(items.Skip(first).Take(shown).ToList(), Selected - first, x, PanelW, top);
        if (first + shown < items.Count) art.Centred(Text.Get(K.More), Game.Width / 2, top + (shown * ScreenArt.Row) + 1, ScreenArt.Muted);
        art.Hint(Text.Get(K.HintSettings));
    }

    /// <inheritdoc/>
    protected override void Back()
    {
        if (world.TryGet(out var onShow)) ScreenFlow.ApplySettings(onShow);
        Console.WriteLine($"SAP: screens: settings saved, {Prefs}");
        ScreenFlow.Pop();
    }

    static string OnOff(bool on) => Text.Get(on ? K.On : K.Off);

    static string Device(InputDevice device) => Players.Gamepads.Label(device);    // the pad by name

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
        if (world.TryGet(out var onShow)) ScreenFlow.Apply(onShow, ScreenFlow.Settings);
    }

    static void Period(int dir)
    {
        int[] choices = ScreenPrefs.PeriodChoices;
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
