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
