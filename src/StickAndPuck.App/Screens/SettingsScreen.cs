using System.Globalization;
using StickAndPuck.App.Core;
using System.Collections.Immutable;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens;

/// <summary>Settings: every switch in one place. The rules page first (the preset and every
/// rule, penalties and fights among them). Game: control switching, line changes, difficulty,
/// period length, sim game, auto deke, one-timers. Controls: player one's and
/// player two's devices, the key mapping page, the control marker. Display and sound: camera zoom, lighting mood, swap
/// kits, volume, language, the graphics card (leaving starts the game again on a newly picked card, which
/// opens here asking to keep it: Enter keeps it, Esc or 10 seconds go back to the card before). Debug: the fps line, the match line, the lines panel. Left and right change a
/// value; it applies to the world on show at once (so a paused match plays on with it) and every
/// later match, and is saved on leaving. The list scrolls when it is taller than the frame. A
/// running match stays paused while this screen is up (<see cref="ScreenSystem"/> holds every
/// match under a screen).</summary>
internal sealed class SettingsScreen(AppState app) : MenuScreen(app)
{
    const int PanelW = 300, PanelTop = 8;

    /// <summary>Rows shown at once; the rest scroll into view.</summary>
    const int Visible = 14;

    /// <summary>Steps a newly picked graphics card waits to be kept before the game goes back to the card before.</summary>
    const int KeepSteps = 600;

    static readonly ImmutableArray<InputDevice> Devices = [InputDevice.KeyboardMouse, InputDevice.Gamepad1];
    static readonly ImmutableArray<InputDevice> Player2Devices = [InputDevice.None, InputDevice.KeyboardMouse, InputDevice.Gamepad1, InputDevice.Gamepad2];

    Maybe<World> world;
    Maybe<string> gpu;    // the card picked on this visit
    bool reverting;
    int keepWait;
    int first;

    ScreenPrefs Prefs => Flow.Prefs;

    /// <inheritdoc/>
    public override IReadOnlyList<MenuItem> Items()
    {
        var s = Prefs.Settings;
        return
        [
            new MenuItem(Text.Get(K.Rules), () => Flow.Push(new RulesScreen(App)), value: Prefs.Rules.Resolve().Name),
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
            new MenuItem(Text.Get(K.KeyMapping), () => Flow.Push(new KeyMappingScreen(App))),
            new MenuItem(Text.Get(K.ControlMarker), value: OnOff(s.ControlMarker), change: _ => Set(s with { ControlMarker = !s.ControlMarker })),
            new MenuItem(Text.Get(K.CameraZoom), value: OnOff(s.CameraZoom), change: _ => Set(s with { CameraZoom = !s.CameraZoom })),
            new MenuItem(Text.Get(K.Lighting), value: Text.Get(s.Lighting switch { LightingMood.Early => K.LightingEarly, LightingMood.Afternoon => K.LightingAfternoon, LightingMood.Late => K.LightingLate, _ => K.LightingRandom }), change: d => Set(s with { Lighting = Cycle(s.Lighting, d) })),
            new MenuItem(Text.Get(K.SwapKits), value: OnOff(s.SwapKits), change: _ => Set(s with { SwapKits = !s.SwapKits })),
            new MenuItem(Text.Get(K.Benches), value: OnOff(s.Benches), change: _ => Set(s with { Benches = !s.Benches })),
            new MenuItem(Text.Get(K.Volume), value: ((int)MathF.Round(s.Volume * 10)).ToString(CultureInfo.InvariantCulture), change: Volume),
            new MenuItem(Text.Get(K.Language), value: Text.NameOf(s.Language), change: d => Set(s with { Language = Cycle(s.Language, d) })),
            new MenuItem(Text.Get(K.GraphicsCard), value: Card().Name, change: Gpu),
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
        if (GpuLaunch.Unconfirmed.TryGet(out var before))
        {
            if (press.Contains(MenuButton.Accept))
            {
                GpuLaunch.Keep();
                Console.WriteLine($"SAP: gpu: kept {GpuLaunch.Running.Name}");
                return;
            }

            if (press.Contains(MenuButton.Back) || ++keepWait >= KeepSteps)
            {
                Console.WriteLine($"SAP: gpu: not kept, back to {before.Name}");
                GpuLaunch.Keep();
                reverting = true;
                gpu = before.IsDefault ? "" : before.Id;
                Set(Prefs.Settings with { Gpu = gpu.Or("") });
                Back();
                return;
            }
        }

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
        bool onGpu = Selected < items.Count && string.Equals(items[Selected].Label, Text.Get(K.GraphicsCard), StringComparison.Ordinal);
        art.Hint(GpuLaunch.Unconfirmed.HasValue ? Text.Format(K.GpuKeep, (KeepSteps - keepWait + 59) / 60)
            : !onGpu ? Text.Get(K.HintSettings)
            : gpu.HasValue && GpuLaunch.CanSwitch && !string.Equals(Card().Id, GpuLaunch.Running.Id, StringComparison.Ordinal) ? Text.Format(K.GpuRestart, Card().Name)
            : Text.Format(K.GpuRunning, GpuCards.ShortRenderer(GpuCards.Renderer)));
    }

    /// <inheritdoc/>
    protected override void Back()
    {
        if (world.TryGet(out var onShow)) Flow.ApplySettings(onShow);
        Console.WriteLine($"SAP: screens: settings saved, {Prefs}");
        Flow.Pop();
        if (gpu.TryGet(out string picked) && GpuLaunch.Request(picked, confirm: !reverting)) Flow.Quit();    // GpuLaunch starts the game again on it
    }

    static string OnOff(bool on) => Text.Get(on ? K.On : K.Off);

    string Device(InputDevice device) => App.Pads.Label(device);    // the pad by name

    static T Cycle<T>(T value, int dir)
        where T : struct, Enum
    {
        return Cycle([.. Enum.GetValues<T>()], value, dir);
    }

    static T Cycle<T>(ImmutableArray<T> all, T value, int dir)
    {
        int i = all.IndexOf(value);
        return all[((Math.Max(i, 0) + dir) % all.Length + all.Length) % all.Length];
    }

    void Set(Settings settings)
    {
        Flow.Prefs = Prefs with { Settings = settings };
        if (world.TryGet(out var onShow)) ScreenFlow.Apply(onShow, Flow.Settings);
    }

    /// <summary>The card the row shows: the one picked here, else the one the settings (and SAP_GPU) ask for.</summary>
    GpuCard Card() => GpuCards.Resolve(GpuCards.All, gpu.Or(Flow.Settings.Gpu));

    void Gpu(int dir)
    {
        if (GpuCards.All.Count == 0) return;
        var card = Cycle([.. GpuCards.All], Card(), dir);
        gpu = card.IsDefault ? "" : card.Id;
        Set(Prefs.Settings with { Gpu = gpu.Or("") });
    }

    void Period(int dir)
    {
        var choices = ScreenPrefs.PeriodChoices;
        int i = choices.IndexOf(Prefs.PeriodMinutes);
        i = i < 0 ? 2 : Math.Clamp(i + dir, 0, choices.Length - 1);
        Flow.Prefs = Prefs with { PeriodMinutes = choices[i] };
    }

    void Controls(int dir)
    {
        Flow.Prefs = Prefs with { Controls = Cycle(Devices, Prefs.Controls, dir) };
    }

    void Player2(int dir)
    {
        Flow.Prefs = Prefs with { Player2 = Cycle(Player2Devices, Prefs.Player2, dir) };
    }

    void Volume(int dir)
    {
        var s = Prefs.Settings;
        float v = Math.Clamp(MathF.Round((s.Volume * 10) + dir) / 10f, 0, 1);
        Set(s with { Volume = v });
    }
}
