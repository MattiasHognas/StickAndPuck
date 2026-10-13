using StickAndPuck.App.Core;
using StickAndPuck.App.Season;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens;

/// <summary>
/// The rules page (W44), opened from RULES on the settings screen: PRESET first (SHL, ARCADE, NHL,
/// or CUSTOM once a rule differs), then every rule the game plays in its group, then RESET TO
/// PRESET. Left and right change a value; picking a preset clears the changed rules. Changes are
/// saved with the settings on leaving and play from the next match: a running match keeps the rules
/// it started with, and a season keeps the rules in its save.
/// </summary>
internal sealed class RulesScreen : MenuScreen
{
    const int PanelW = 300, PanelTop = 8, Visible = 14;

    static readonly RulePreset[] Presets = Enum.GetValues<RulePreset>();

    int first;

    static RuleChoice Choice => ScreenFlow.Prefs.Rules;

    /// <inheritdoc/>
    public override IReadOnlyList<MenuItem> Items()
    {
        var rules = Choice.Resolve();
        var preset = RuleSet.Of(Choice.Preset);
        var items = new List<MenuItem> { new(Text.Get(K.Preset), value: rules.Name, change: d => Pick(Cycle(Choice.Preset, d))) };
        var group = Maybe<string>.None;
        foreach (var row in RuleRows.Shown)
        {
            if (!group.Is(g => string.Equals(row.Group, g, StringComparison.Ordinal)))
            {
                group = row.Group;
                items.Add(new MenuItem(Text.Format(K.RuleGroupHeading, Text.Get(row.Group)), enabled: false));
            }
            string value = row.Get(rules);
            bool changed = !string.Equals(value, row.Get(preset), StringComparison.Ordinal);
            items.Add(new MenuItem(Text.Get(row.Label), value: RuleRow.Show(value) + (changed ? " *" : ""), change: d => Change(row, d)));
        }
        items.Add(new MenuItem(Text.Get(K.ResetToPreset), () => Pick(Choice.Preset), enabled: rules.Custom));
        items.Add(new MenuItem(Text.Get(K.Back), Back));
        return items;
    }

    /// <inheritdoc/>
    public override void Update(World world, IReadOnlySet<MenuButton> press)
    {
        world.Match.Paused = true;
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

        var (x, top) = art.Panel(Text.Get(K.Rules), PanelW, ((shown + 1) * ScreenArt.Row) + 30, PanelTop);
        if (first > 0) art.Centred(Text.Get(K.More), Game.Width / 2, top - 3, ScreenArt.Muted);
        top += ScreenArt.Row / 2;
        art.Menu(items.Skip(first).Take(shown).ToList(), Selected - first, x, PanelW, top);
        if (first + shown < items.Count) art.Centred(Text.Get(K.More), Game.Width / 2, top + (shown * ScreenArt.Row) + 1, ScreenArt.Muted);
        art.Hint(Text.Get(ScreenFlow.Playing.HasValue ? K.HintRulesNextMatch : K.HintRules));
    }

    /// <inheritdoc/>
    protected override void Back()
    {
        ScreenPrefsFile.Write(ScreenFlow.Prefs);
        Console.WriteLine($"SAP: screens: rules saved, {Choice.Resolve()}");
        ScreenFlow.Pop();
    }

    /// <summary>The preset one step along, wrapping.</summary>
    /// <param name="preset">The current preset.</param>
    /// <param name="dir">-1 or 1.</param>
    /// <returns>The preset.</returns>
    public static RulePreset Cycle(RulePreset preset, int dir)
    {
        int i = Array.IndexOf(Presets, preset);
        return Presets[(((i + dir) % Presets.Length) + Presets.Length) % Presets.Length];
    }

    /// <summary>Chooses a preset as it stands, clearing every changed rule.</summary>
    /// <param name="preset">The preset.</param>
    public static void Pick(RulePreset preset)
    {
        ScreenFlow.Prefs = ScreenFlow.Prefs with { Rules = RuleChoice.Of(preset) };
    }

    static void Change(RuleRow row, int dir)
    {
        ScreenFlow.Prefs = ScreenFlow.Prefs with { Rules = Choice.With(row, row.Next(Choice.Resolve(), dir)) };
    }
}

/// <summary>
/// Which rules a match starts with (W44): the arcade entries always play <see cref="RuleSet.Arcade"/>;
/// a season or playoff game plays the rules stored in the season save, which its first game stores
/// from the player's choice; every other game plays the player's choice from settings.json.
/// SAP_RULES=shl|arcade|nhl picks a preset as it stands for headless runs and is never saved.
/// </summary>
internal static class RulePicks
{
    /// <summary>Gets the player's rule choice, under SAP_RULES.</summary>
    public static RuleChoice Choice =>
        Environment.GetEnvironmentVariable("SAP_RULES") is { Length: > 0 } name
            ? RuleChoice.Of(Enum.TryParse<RulePreset>(name.Trim(), ignoreCase: true, out var p) ? p : throw new InvalidOperationException($"SAP_RULES={name}: use shl, arcade or nhl"))
            : ScreenFlow.Prefs.Rules;

    /// <summary>Gets the rules a match of this plan plays.</summary>
    /// <param name="plan">What the match is.</param>
    /// <returns>The resolved rules.</returns>
    public static RuleSet For(MatchPlan plan)
    {
        if (plan.Mode.TryGet(out var mode) && Fixed(mode).TryGet(out var fixedRules)) return fixedRules;
        if (!plan.Season || !SeasonSession.Current.TryGet(out var season)) return Choice.Resolve();
        if (season.Rules.TryGet(out var stored)) return stored;
        var rules = Choice.Resolve();
        season.Rules = rules;
        if (SeasonSession.SavePath.Length > 0) SeasonSave.Write(season, SeasonSession.SavePath);
        Console.WriteLine($"SAP: screens: season stores {rules.Name} rules");
        return rules;
    }

    /// <summary>Gets the rules a mode is fixed to, or None when the player picks: the arcade entries play Arcade.</summary>
    /// <param name="mode">The mode.</param>
    /// <returns>The fixed rules, or None.</returns>
    public static Maybe<RuleSet> Fixed(IGameMode mode)
    {
        return mode is ArcadeMode ? RuleSet.Arcade : Maybe<RuleSet>.None;
    }
}
