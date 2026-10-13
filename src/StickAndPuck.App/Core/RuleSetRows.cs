using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Core;

/// <summary>One rule on the rules page: its key in settings.json, its label and group, the values it takes
/// (as stored, enum names or On and Off) and how to read and change it on a <see cref="RuleSet"/>.</summary>
/// <param name="Key">The key in settings.json, the <see cref="RuleSet"/> property's name.</param>
/// <param name="Label">The string table key of its label on the rules page.</param>
/// <param name="Group">The string table key of the group heading it sits under.</param>
/// <param name="Plays">True when the game plays the rule; a rule a later task wires stays off the page until then.</param>
/// <param name="Values">The values, as stored.</param>
/// <param name="Get">Reads the value.</param>
/// <param name="Set">Changes the value.</param>
public sealed record RuleRow(string Key, string Label, string Group, bool Plays, IReadOnlyList<string> Values, Func<RuleSet, string> Get, Func<RuleSet, string, RuleSet> Set)
{
    /// <summary>Gets a value as the rules page shows it.</summary>
    public static string Show(string value) => value switch
    {
        "On" => Text.Get(K.RuleValueOn),
        "Off" => Text.Get(K.RuleValueOff),
        "Hybrid" => Text.Get(K.RuleValueHybrid),
        "Touch" => Text.Get(K.RuleValueTouch),
        "ThreeOnThree" => Text.Get(K.RuleValueThreeOnThree),
        "SuddenDeath" => Text.Get(K.RuleValueSuddenDeath),
        "ShootoutOnly" => Text.Get(K.RuleValueShootoutOnly),
        "Tie" => Text.Get(K.RuleValueTie),
        "Lenient" => Text.Get(K.RuleValueLenient),
        "Normal" => Text.Get(K.RuleValueNormal),
        "Strict" => Text.Get(K.RuleValueStrict),
        "Away" => Text.Get(K.RuleValueAway),
        "Home" => Text.Get(K.RuleValueHome),
        "HomeChooses" => Text.Get(K.RuleValueHomeChooses),
        "MajorAndEjection" => Text.Get(K.RuleValueMajorAndEjection),
        "LoserMinor" => Text.Get(K.RuleValueLoserMinor),
        "Major" => Text.Get(K.RuleValueMajor),
        "Full" => Text.Get(K.RuleValueFull),
        "MinorsOnly" => Text.Get(K.RuleValueMinorsOnly),
        "Substitute" => Text.Get(K.RuleValueSubstitute),
        "FourOnFour" => Text.Get(K.RuleValueFourOnFour),
        _ => value.ToUpperInvariant(),
    };

    /// <summary>The value one step along from the current one, wrapping.</summary>
    public string Next(RuleSet rules, int dir)
    {
        int i = Math.Max(0, Values.ToList().IndexOf(Get(rules)));
        return Values[(((i + dir) % Values.Count) + Values.Count) % Values.Count];
    }
}

/// <summary>Every rule switch, in page order: Game (offside, icing, goalie pull), Overtime, Penalties,
/// Infractions and Fights. <see cref="RuleRow.Plays"/> says whether the game plays a row's rule.</summary>
public static class RuleRows
{
    const string On = "On", Off = "Off";

    /// <summary>Gets every rule, shown or not.</summary>
    public static IReadOnlyList<RuleRow> All { get; } =
    [
        Flag(nameof(RuleSet.Offside), K.RuleOffside, K.RuleGroupGame, true, r => r.Offside, (r, v) => r with { Offside = v }),
        Choice(nameof(RuleSet.Icing), K.RuleIcing, K.RuleGroupGame, true, Enum.GetValues<IcingCall>(), r => r.Icing, (r, v) => r with { Icing = v }),
        Flag(nameof(RuleSet.GoaliePull), K.RuleGoaliePull, K.RuleGroupGame, true, r => r.GoaliePull, (r, v) => r with { GoaliePull = v }),
        Choice(nameof(RuleSet.Overtime), K.RuleOvertime, K.RuleGroupOvertime, true, Enum.GetValues<OvertimeStyle>(), r => r.Overtime, (r, v) => r with { Overtime = v }),
        Choice(nameof(RuleSet.PlayoffOvertime), K.RulePlayoffOvertime, K.RuleGroupOvertime, true, [OvertimeStyle.SuddenDeath, OvertimeStyle.ThreeOnThree, OvertimeStyle.ShootoutOnly], r => r.PlayoffOvertime, (r, v) => r with { PlayoffOvertime = v }),
        new(nameof(RuleSet.ShootoutShooters), K.RuleShootoutShooters, K.RuleGroupOvertime, true, ["3", "5"], r => r.ShootoutShooters.ToString(System.Globalization.CultureInfo.InvariantCulture), (r, v) => r with { ShootoutShooters = int.Parse(v, System.Globalization.CultureInfo.InvariantCulture) }),
        Choice(nameof(RuleSet.ShootoutFirst), K.RuleShootoutFirst, K.RuleGroupOvertime, true, Enum.GetValues<ShootoutFirst>(), r => r.ShootoutFirst, (r, v) => r with { ShootoutFirst = v }),
        Flag(nameof(RuleSet.Penalties), K.RulePenalties, K.RuleGroupPenalties, true, r => r.Penalties, (r, v) => r with { Penalties = v }),
        Choice(nameof(RuleSet.Strictness), K.RuleStrictness, K.RuleGroupPenalties, true, Enum.GetValues<RefereeStrictness>(), r => r.Strictness, (r, v) => r with { Strictness = v }),
        Flag(nameof(RuleSet.PenaltiesCarryIntoOvertime), K.RulePenaltiesCarryIntoOvertime, K.RuleGroupPenalties, true, r => r.PenaltiesCarryIntoOvertime, (r, v) => r with { PenaltiesCarryIntoOvertime = v }),
        Flag(nameof(RuleSet.StackPenalties), K.RuleStackPenalties, K.RuleGroupPenalties, true, r => r.StackPenalties, (r, v) => r with { StackPenalties = v }),
        Choice(nameof(RuleSet.CoincidentalMinors), K.RuleCoincidentalMinors, K.RuleGroupPenalties, true, Enum.GetValues<CoincidentalMinors>(), r => r.CoincidentalMinors, (r, v) => r with { CoincidentalMinors = v }),
        Choice(nameof(RuleSet.PenaltySeverity), K.RulePenaltySeverity, K.RuleGroupPenalties, true, Enum.GetValues<PenaltySeverity>(), r => r.PenaltySeverity, (r, v) => r with { PenaltySeverity = v }),
        Flag(nameof(RuleSet.Interference), K.RuleInterference, K.RuleGroupInfractions, true, r => r.Interference, (r, v) => r with { Interference = v }),
        Flag(nameof(RuleSet.Holding), K.RuleHolding, K.RuleGroupInfractions, true, r => r.Holding, (r, v) => r with { Holding = v }),
        Flag(nameof(RuleSet.HighSticking), K.RuleHighSticking, K.RuleGroupInfractions, true, r => r.HighSticking, (r, v) => r with { HighSticking = v }),
        Flag(nameof(RuleSet.CrossChecking), K.RuleCrossChecking, K.RuleGroupInfractions, true, r => r.CrossChecking, (r, v) => r with { CrossChecking = v }),
        Flag(nameof(RuleSet.Boarding), K.RuleBoarding, K.RuleGroupInfractions, true, r => r.Boarding, (r, v) => r with { Boarding = v }),
        Flag(nameof(RuleSet.Roughing), K.RuleRoughing, K.RuleGroupInfractions, true, r => r.Roughing, (r, v) => r with { Roughing = v }),
        Flag(nameof(RuleSet.IcingNoChange), K.RuleIcingNoChange, K.RuleGroupGame, true, r => r.IcingNoChange, (r, v) => r with { IcingNoChange = v }),
        Flag(nameof(RuleSet.Fights), K.RuleFights, K.RuleGroupFights, true, r => r.Fights, (r, v) => r with { Fights = v }),
        Choice(nameof(RuleSet.FightPenalty), K.RuleFightPenalty, K.RuleGroupFights, true, Enum.GetValues<FightPenalty>(), r => r.FightPenalty, (r, v) => r with { FightPenalty = v }),
    ];

    /// <summary>Gets the rules the game plays, grouped as the rules page shows them.</summary>
    public static IReadOnlyList<RuleRow> Shown => [.. All.Where(r => r.Plays).GroupBy(r => r.Group).SelectMany(g => g)];

    static RuleRow Flag(string key, string label, string group, bool plays, Func<RuleSet, bool> get, Func<RuleSet, bool, RuleSet> set)
    {
        return new RuleRow(key, label, group, plays, [On, Off], r => get(r) ? On : Off, (r, v) => set(r, string.Equals(v, On, StringComparison.Ordinal)));
    }

    static RuleRow Choice<T>(string key, string label, string group, bool plays, IReadOnlyList<T> values, Func<RuleSet, T> get, Func<RuleSet, T, RuleSet> set)
        where T : struct, Enum
    {
        return new RuleRow(key, label, group, plays, [.. values.Select(v => v.ToString())], r => get(r).ToString(), (r, v) => set(r, Enum.Parse<T>(v)));
    }
}
