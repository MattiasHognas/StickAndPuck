using System.Text.Json.Serialization;

namespace StickAndPuck.App.Core
{
    /// <summary>The player's rule choice as settings.json stores it: a preset and the rules changed from it,
    /// by <see cref="RuleRow.Key"/> (for example <c language="json">"Offside": "Off"</c>). A rule set back to the
    /// preset's value drops out of <see cref="Overrides"/>, so CUSTOM shows only while something differs.</summary>
    public sealed record RuleChoice
    {
        /// <summary>Gets the preset.</summary>
        public RulePreset Preset { get; init; } = RulePreset.Shl;

        /// <summary>Gets the changed rules, key to value; unknown keys and values are ignored.</summary>
        public IReadOnlyDictionary<string, string> Overrides { get; init; } =
            new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Gets a value indicating whether a rule differs from the preset.</summary>
        [JsonIgnore]
        public bool Custom => Resolve().Custom;

        /// <summary>The rules a match is played by: the preset with the changed rules laid over it.</summary>
        public RuleSet Resolve()
        {
            RuleSet preset = RuleSet.Of(Preset);
            RuleSet rules = preset;
            foreach (RuleRow row in RuleRows.All)
            {
                if (
                    Overrides.GetValueOrDefault(row.Key) is { } value
                    && row.Values.Contains(value, StringComparer.OrdinalIgnoreCase)
                )
                {
                    rules = row.Set(
                        rules,
                        row.Values.First(v =>
                            string.Equals(v, value, StringComparison.OrdinalIgnoreCase)
                        )
                    );
                }
            }
            return rules == preset ? preset : rules with { Custom = true };
        }

        /// <summary>A choice with one rule changed; setting it back to the preset's value drops the override.</summary>
        public RuleChoice With(RuleRow row, string value)
        {
            Dictionary<string, string> overrides = new(Overrides, StringComparer.Ordinal);
            if (string.Equals(row.Get(RuleSet.Of(Preset)), value, StringComparison.Ordinal))
            {
                _ = overrides.Remove(row.Key);
            }
            else
            {
                overrides[row.Key] = value;
            }

            return this with
            {
                Overrides = overrides,
            };
        }

        /// <summary>A choice of a preset as it stands, with every change cleared.</summary>
        public static RuleChoice Of(RulePreset preset)
        {
            return new() { Preset = preset };
        }
    }
}
