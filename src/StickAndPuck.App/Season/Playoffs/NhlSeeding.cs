using System.Globalization;
using StickAndPuck.App.Season.Schedule;

namespace StickAndPuck.App.Season.Playoffs
{
    /// <summary>The NHL's 16-club playoff field, picked from the table by division: the top three of every
    /// division and, in each conference, the next two clubs on points as wild cards. In the first
    /// round the conference's best division winner meets the second wild card, the other division
    /// winner the first wild card, and each division's second and third meet; the bracket is fixed
    /// from there (no reseeding), so the second round pairs the two series of each division's half (a wild
    /// card plays on in the half it was slotted into), then the conference finals and the final.</summary>
    public static class NhlSeeding
    {
        /// <summary>Clubs of each division that qualify by place.</summary>
        public const int PerDivision = 3;

        /// <summary>Wild cards in each conference.</summary>
        public const int WildCards = 2;

        /// <summary>The field: the first-round pairs in bracket order (conferences by name, inside each the
        /// best division winner's half first, its winner against the wild card first), and each
        /// qualifier's tag: the division's initial and place (A1, M3) or WC1 and WC2.</summary>
        public static NhlField Field(SeasonState season, IReadOnlyList<StandingRow> table)
        {
            Dictionary<string, int> place = [with(StringComparer.Ordinal)];
            for (int i = 0; i < table.Count; i++)
            {
                place[table[i].ClubId] = i;
            }

            List<(string, string)> pairs = [];
            List<string> conferences = [];
            Dictionary<string, string> tags = [with(StringComparer.Ordinal)];
            foreach (
                IGrouping<string, DivisionTable> conference in DivisionStandings
                    .Of(season, table)
                    .GroupBy(d => d.Conference, StringComparer.Ordinal)
            )
            {
                List<DivisionTable> divisions =
                [
                    .. conference.OrderBy(d => place[d.Rows[0].ClubId]),
                ];
                if (divisions.Count != 2 || divisions.Exists(d => d.Rows.Count < PerDivision))
                {
                    throw new InvalidOperationException(
                        $"the {conference.Key} conference needs two divisions of at least {PerDivision} clubs for the NHL playoffs"
                    );
                }

                List<string> wild =
                [
                    .. divisions
                        .SelectMany(d => d.Rows.Skip(PerDivision))
                        .OrderBy(r => place[r.ClubId])
                        .Take(WildCards)
                        .Select(r => r.ClubId),
                ];
                if (wild.Count < WildCards)
                {
                    throw new InvalidOperationException(
                        $"the {conference.Key} conference has too few clubs for two wild cards"
                    );
                }

                conferences.Add(conference.Key);
                for (int w = 0; w < wild.Count; w++)
                {
                    tags[wild[w]] = "WC" + N(w + 1);
                }

                for (int d = 0; d < divisions.Count; d++)
                {
                    IReadOnlyList<StandingRow> rows = divisions[d].Rows;
                    string initial =
                        divisions[d].Division.Length > 0
                            ? divisions[d].Division[..1].ToUpperInvariant()
                            : "D";
                    for (int p = 0; p < PerDivision; p++)
                    {
                        tags[rows[p].ClubId] = initial + N(p + 1);
                    }

                    // the best division winner gets the weaker wild card
                    pairs.Add((rows[0].ClubId, wild[d == 0 ? 1 : 0]));
                    pairs.Add((rows[1].ClubId, rows[2].ClubId));
                }
            }
            return new NhlField(pairs, tags, conferences);
        }

        private static string N(int n)
        {
            return n.ToString(CultureInfo.InvariantCulture);
        }
    }
}
