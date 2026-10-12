using System.Text.Json.Serialization;

namespace StickAndPuck.Core;

/// <summary>A league's rule book: the built-in presets a <see cref="RuleSet"/> starts from.</summary>
public enum RulePreset
{
    /// <summary>Default: the SHL as the game plays it.</summary>
    Shl,

    /// <summary>Loose rules for the arcade runs: no offside, no icing, a lenient referee.</summary>
    Arcade,

    /// <summary>The NHL: SHL rules with the NHL's shootout order, fight major and 4 on 4 coincidentals.</summary>
    Nhl,
}

/// <summary>How icing is called.</summary>
public enum IcingCall
{
    /// <summary>Hybrid (SHL, NHL): the race to the end-zone dot decides; the defence wins a tie.</summary>
    Hybrid,

    /// <summary>Touch: icing stands only when a defender touches the puck first; an attacker getting there first waves it off.</summary>
    Touch,

    /// <summary>No icing.</summary>
    Off,
}

/// <summary>How a tie after regulation is settled, as a rule switch (mapped onto an overtime format by <see cref="MatchSetup.WithRules"/>).</summary>
public enum OvertimeStyle
{
    /// <summary>One 3 on 3 sudden death period, then a shootout (SHL and NHL regular season).</summary>
    ThreeOnThree,

    /// <summary>5 on 5 sudden death in full periods until a goal, no shootout (SHL and NHL playoffs).</summary>
    SuddenDeath,

    /// <summary>Straight to a shootout.</summary>
    ShootoutOnly,

    /// <summary>The tie stands (regular season only).</summary>
    Tie,
}

/// <summary>How readily the referee calls a foul.</summary>
public enum RefereeStrictness
{
    /// <summary>About half as many calls.</summary>
    Lenient,

    /// <summary>Default: the calls W14 tuned, about eight per 60 minutes.</summary>
    Normal,

    /// <summary>About half again as many calls.</summary>
    Strict,
}

/// <summary>Who shoots first in a shootout (W45 plays it).</summary>
public enum ShootoutFirst
{
    /// <summary>The away club shoots first and starts sudden death (SHL).</summary>
    Away,

    /// <summary>The home club shoots first.</summary>
    Home,

    /// <summary>The home club chooses (NHL).</summary>
    HomeChooses,
}

/// <summary>What a fight costs (W48 plays it).</summary>
public enum FightPenalty
{
    /// <summary>Both fighters get a 5 minute major and a game misconduct (SHL).</summary>
    MajorAndEjection,

    /// <summary>The loser serves 2 minutes, the winner nothing (the game's original rule, Arcade).</summary>
    LoserMinor,

    /// <summary>Both fighters get a 5 minute major and stay in the game (NHL).</summary>
    Major,
}

/// <summary>Which penalty lengths the referee hands out (W47 plays it).</summary>
public enum PenaltySeverity
{
    /// <summary>Minors, double minors, majors with a game misconduct and misconducts (SHL, NHL).</summary>
    Full,

    /// <summary>Every call is a 2 minute minor (Arcade).</summary>
    MinorsOnly,
}

/// <summary>What equal minors on both clubs at once do (W46 plays it).</summary>
public enum CoincidentalMinors
{
    /// <summary>Immediate substitution, play stays 5 on 5 (SHL).</summary>
    Substitute,

    /// <summary>One minor each plays 4 on 4 (NHL).</summary>
    FourOnFour,
}

/// <summary>
/// W44: every game rule as a switch, so a league is a preset and a player can turn any rule off.
/// A match carries its resolved set on <see cref="MatchSetup.Rules"/>; systems read
/// <c>world.Setup.Rules.X</c> and never a preset name. Presets: <see cref="Shl"/> (the default),
/// <see cref="Arcade"/> and <see cref="Nhl"/>. A player's changes are a <see cref="RuleChoice"/>
/// saved in settings.json and resolved once when a match starts; the arcade entries always play
/// <see cref="Arcade"/> as it stands, and a season keeps the set it was started with in its save.
/// The rows on the rules page, with their keys, are <see cref="RuleRows.All"/>.
/// </summary>
public sealed record RuleSet
{
    /// <summary>The SHL: the rules the game was built on.</summary>
    public static readonly RuleSet Shl = new();

    /// <summary>Arcade: no offside, no icing, a lenient referee and the game's original penalty and fight rules.</summary>
    public static readonly RuleSet Arcade = new()
    {
        Preset = RulePreset.Arcade,
        Offside = false,
        Icing = IcingCall.Off,
        Strictness = RefereeStrictness.Lenient,
        PenaltiesCarryIntoOvertime = false,
        StackPenalties = false,
        PenaltySeverity = PenaltySeverity.MinorsOnly,
        Interference = false,
        Holding = false,
        HighSticking = false,
        CrossChecking = false,
        Boarding = false,
        Roughing = false,
        FightPenalty = FightPenalty.LoserMinor,
        IcingNoChange = false,
    };

    /// <summary>The NHL: SHL rules with home choosing the shootout order, a fight major without ejection and 4 on 4 coincidental minors.</summary>
    public static readonly RuleSet Nhl = new()
    {
        Preset = RulePreset.Nhl,
        ShootoutFirst = ShootoutFirst.HomeChooses,
        FightPenalty = FightPenalty.Major,
        CoincidentalMinors = CoincidentalMinors.FourOnFour,
    };

    /// <summary>Gets the preset this set started from.</summary>
    public RulePreset Preset { get; init; } = RulePreset.Shl;

    /// <summary>Gets a value indicating whether the player changed a rule of the preset (shown as CUSTOM).</summary>
    public bool Custom { get; init; }

    /// <summary>Gets a value indicating whether offside is called (W7b).</summary>
    public bool Offside { get; init; } = true;

    /// <summary>Gets how icing is called (W7b).</summary>
    public IcingCall Icing { get; init; } = IcingCall.Hybrid;

    /// <summary>Gets a value indicating whether a club may pull its goalie for an extra attacker (W23).</summary>
    public bool GoaliePull { get; init; } = true;

    /// <summary>Gets how a tie is settled in a season, exhibition, versus or arcade game (W7c).</summary>
    public OvertimeStyle Overtime { get; init; } = OvertimeStyle.ThreeOnThree;

    /// <summary>Gets how a tie is settled in a playoff game (W19).</summary>
    public OvertimeStyle PlayoffOvertime { get; init; } = OvertimeStyle.SuddenDeath;

    /// <summary>Gets a value indicating whether the referee calls penalties (W14).</summary>
    public bool Penalties { get; init; } = true;

    /// <summary>Gets how readily the referee calls a foul (W14's strictness).</summary>
    public RefereeStrictness Strictness { get; init; } = RefereeStrictness.Normal;

    /// <summary>Gets a value indicating whether fights can start (W15).</summary>
    public bool Fights { get; init; } = true;

    /// <summary>Gets the shooters per club before the shootout goes to sudden death (W45).</summary>
    public int ShootoutShooters { get; init; } = 3;

    /// <summary>Gets who shoots first in the shootout (W45).</summary>
    public ShootoutFirst ShootoutFirst { get; init; } = ShootoutFirst.Away;

    /// <summary>Gets a value indicating whether penalties still running at the end of regulation carry into overtime (W46).</summary>
    public bool PenaltiesCarryIntoOvertime { get; init; } = true;

    /// <summary>Gets a value indicating whether a third penalty waits in the box instead of being waved off (W46).</summary>
    public bool StackPenalties { get; init; } = true;

    /// <summary>Gets what equal minors on both clubs do (W46).</summary>
    public CoincidentalMinors CoincidentalMinors { get; init; } = CoincidentalMinors.Substitute;

    /// <summary>Gets which penalty lengths are handed out (W47).</summary>
    public PenaltySeverity PenaltySeverity { get; init; } = PenaltySeverity.Full;

    /// <summary>Gets a value indicating whether the referee calls interference: a hit on a skater without the puck (W47).</summary>
    public bool Interference { get; init; } = true;

    /// <summary>Gets a value indicating whether the referee calls holding: close trailing contact on a slowed carrier (W47).</summary>
    public bool Holding { get; init; } = true;

    /// <summary>Gets a value indicating whether the referee calls high-sticking: the stick up on a face-to-face check (W47).</summary>
    public bool HighSticking { get; init; } = true;

    /// <summary>Gets a value indicating whether the referee calls cross-checking: a hard hit from the front on the carrier (W47).</summary>
    public bool CrossChecking { get; init; } = true;

    /// <summary>Gets a value indicating whether the referee calls boarding: a hard hit from behind near the boards (W47).</summary>
    public bool Boarding { get; init; } = true;

    /// <summary>Gets a value indicating whether the referee calls roughing: a hit after the whistle, or the same skater hit again at once (W47).</summary>
    public bool Roughing { get; init; } = true;

    /// <summary>Gets what a fight costs (W48).</summary>
    public FightPenalty FightPenalty { get; init; } = FightPenalty.MajorAndEjection;

    /// <summary>Gets a value indicating whether the icing club keeps its line until the faceoff (W49).</summary>
    public bool IcingNoChange { get; init; } = true;

    /// <summary>Gets the factor on W14's call chance for <see cref="Strictness"/>.</summary>
    [JsonIgnore]
    public float StrictnessScale => Strictness switch
    {
        RefereeStrictness.Lenient => 0.5f,
        RefereeStrictness.Strict => 1.5f,
        _ => 1f,
    };

    /// <summary>Gets the name shown for this set: the preset's, or CUSTOM once a rule was changed.</summary>
    [JsonIgnore]
    public string Name => Custom ? "CUSTOM" : NameOf(Preset);

    /// <summary>Gets a preset's rules.</summary>
    /// <param name="preset">The preset.</param>
    /// <returns>The rules.</returns>
    public static RuleSet Of(RulePreset preset) => preset switch
    {
        RulePreset.Arcade => Arcade,
        RulePreset.Nhl => Nhl,
        _ => Shl,
    };

    /// <summary>Gets a preset's name, in capitals.</summary>
    /// <param name="preset">The preset.</param>
    /// <returns>SHL, ARCADE or NHL.</returns>
    public static string NameOf(RulePreset preset) => preset.ToString().ToUpperInvariant();

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"{Name} rules ({string.Join(", ", RuleRows.All.Select(r => $"{r.Key} {r.Get(this)}"))})";
    }
}

/// <summary>
/// The player's rule choice as settings.json stores it: a preset and the rules changed from it,
/// by <see cref="RuleRow.Key"/> (for example <c>"Offside": "Off"</c>). A rule set back to the
/// preset's value drops out of <see cref="Overrides"/>, so CUSTOM shows only while something differs.
/// </summary>
public sealed record RuleChoice
{
    /// <summary>Gets the preset.</summary>
    public RulePreset Preset { get; init; } = RulePreset.Shl;

    /// <summary>Gets the changed rules, key to value; unknown keys and values are ignored.</summary>
    public IReadOnlyDictionary<string, string> Overrides { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets a value indicating whether a rule differs from the preset.</summary>
    [JsonIgnore]
    public bool Custom => Resolve().Custom;

    /// <summary>The rules a match is played by: the preset with the changed rules laid over it.</summary>
    /// <returns>The resolved set; <see cref="RuleSet.Custom"/> is true when a change took.</returns>
    public RuleSet Resolve()
    {
        var preset = RuleSet.Of(Preset);
        var rules = preset;
        foreach (var row in RuleRows.All)
        {
            if (Overrides.TryGetValue(row.Key, out string? value) && row.Values.Contains(value, StringComparer.OrdinalIgnoreCase))
                rules = row.Set(rules, row.Values.First(v => string.Equals(v, value, StringComparison.OrdinalIgnoreCase)));
        }
        return rules == preset ? preset : rules with { Custom = true };
    }

    /// <summary>A choice with one rule changed; setting it back to the preset's value drops the override.</summary>
    /// <param name="row">The rule.</param>
    /// <param name="value">One of the row's values.</param>
    /// <returns>The new choice.</returns>
    public RuleChoice With(RuleRow row, string value)
    {
        var overrides = new Dictionary<string, string>(Overrides, StringComparer.Ordinal);
        if (string.Equals(row.Get(RuleSet.Of(Preset)), value, StringComparison.Ordinal)) overrides.Remove(row.Key);
        else overrides[row.Key] = value;
        return this with { Overrides = overrides };
    }

    /// <summary>A choice of a preset as it stands, with every change cleared.</summary>
    /// <param name="preset">The preset.</param>
    /// <returns>The new choice.</returns>
    public static RuleChoice Of(RulePreset preset) => new() { Preset = preset };
}
