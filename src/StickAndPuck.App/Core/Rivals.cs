using System.Text.Json;

namespace StickAndPuck.App.Core
{
    /// <summary>Rivalries (W131): the rival pairs in each league's data/season/{league}/rivals.json and what a game between them changes.
    /// Whether a match is one is decided when it is set up and kept on <see cref="MatchSetup.Rivalry"/>
    /// (and in its replay), so a replay plays the same even if the data file changes later. A rivalry
    /// game gets a banner on the opening card, more checking (<see cref="HitFactor"/>) and fights
    /// (<see cref="FightFactor"/>), and in a season a bigger morale swing (<see cref="MoraleFactor"/>).
    /// <see cref="RuleSet.Rivalries"/> turns it off and <see cref="RuleSet.RivalryStrength"/> scales it.</summary>
    public static class Rivals
    {
        /// <summary>Gets the data file with a league's pairs.</summary>
        public static string File(League league)
        {
            return $"data/season/{Leagues.Id(league)}/rivals.json";
        }

        /// <summary>How much more often a skater in a rivalry game decides to finish a check, at normal strength (+40%).</summary>
        public const float HitBoost = 0.4f;

        /// <summary>How much more often a hard check drops the gloves in a rivalry game, at normal strength (+75%).</summary>
        public const float FightBoost = 0.75f;

        /// <summary>How much bigger a rivalry result swings team morale, at normal strength (+50%).</summary>
        public const float MoraleBoost = 0.5f;

        private static readonly Lazy<IReadOnlyList<(string A, string B)>> Loaded = new(() =>
            [
                .. Leagues.All.SelectMany(league =>
                    Parse(ResourceLoader.ReadText(File(league)), File(league))
                ),
            ]
        );

        /// <summary>Gets every rival pair in every league's data file.</summary>
        public static IReadOnlyList<(string A, string B)> Pairs => Loaded.Value;

        /// <summary>Gets a value indicating whether two clubs are rivals, in either order.</summary>
        public static bool Are(string a, string b)
        {
            return !string.Equals(a, b, StringComparison.Ordinal)
                && Pairs.Any(p =>
                    (
                        string.Equals(p.A, a, StringComparison.Ordinal)
                        && string.Equals(p.B, b, StringComparison.Ordinal)
                    )
                    || (
                        string.Equals(p.A, b, StringComparison.Ordinal)
                        && string.Equals(p.B, a, StringComparison.Ordinal)
                    )
                );
        }

        /// <summary>Gets a value indicating whether a match plays as a rivalry game: its clubs are rivals and its rules have rivalries on.</summary>
        public static bool Playing(MatchSetup setup)
        {
            return setup.Rivalry && setup.Rules.RivalryScale > 0f;
        }

        /// <summary>Gets the factor on an AI skater's chance to finish a check: 1 outside a rivalry game.</summary>
        public static float HitFactor(MatchSetup setup)
        {
            return Playing(setup) ? 1f + (HitBoost * setup.Rules.RivalryScale) : 1f;
        }

        /// <summary>Gets the factor on the chance a hard check drops the gloves: 1 outside a rivalry game.</summary>
        public static float FightFactor(MatchSetup setup)
        {
            return Playing(setup) ? 1f + (FightBoost * setup.Rules.RivalryScale) : 1f;
        }

        /// <summary>Gets the factor on the morale swing of a result between two clubs: 1 unless they are rivals and the rules have rivalries on.</summary>
        public static float MoraleFactor(RuleSet rules, string home, string away)
        {
            return rules.RivalryScale > 0f && Are(home, away)
                ? 1f + (MoraleBoost * rules.RivalryScale)
                : 1f;
        }

        /// <summary>Reads the pairs from a data file's text; <paramref name="file"/> names it in errors.</summary>
        public static IReadOnlyList<(string A, string B)> Parse(string json, string file)
        {
            using JsonDocument doc = JsonDocument.Parse(
                json,
                new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                }
            );
            List<(string, string)> pairs = [];
            foreach (JsonElement pair in doc.RootElement.GetProperty("pairs").EnumerateArray())
            {
                string[] ids = [.. pair.EnumerateArray().Select(e => e.GetString() ?? "")];
                if (ids.Length != 2 || ids.Any(string.IsNullOrWhiteSpace))
                {
                    throw new InvalidDataException(
                        $"{file}: every pair needs two club ids, got {pair.GetRawText()}"
                    );
                }

                pairs.Add((ids[0], ids[1]));
            }

            return pairs;
        }
    }
}
