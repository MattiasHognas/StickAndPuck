using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.Schedule
{
    /// <summary>Builds the NHL's 82-game schedule from the clubs' conferences and divisions: four games
    /// against most division rivals and three against two of them (a ring through the division, so
    /// every club gets exactly two three-game rivals), three against every club of the other division
    /// in the conference, and two, one at each rink, against every club of the other conference. With
    /// the NHL's two conferences of two eight-club divisions that is 26 + 24 + 32 = 82 games and 41 at
    /// home. The games are then packed into rounds where every club plays at most once, the clubs with
    /// the most games left going first, and a pair is kept apart from its last meeting when it can be,
    /// so the season comes out at about 82 rounds.</summary>
    public static class NhlSchedule
    {
        /// <summary>Rounds a pair should rest between two meetings when the round can do without them.</summary>
        private const int Rest = 6;

        /// <summary>Builds the schedule.</summary>
        public static IReadOnlyList<Fixture> Build(IReadOnlyList<ClubData> clubs, int seed)
        {
            if (clubs.Count < 2)
            {
                throw new ArgumentException("a season needs at least two clubs", nameof(clubs));
            }
            Random dice = Seeds.Dice(seed);
            List<(string Home, string Away)> games = Meetings(clubs, dice);
            return Pack(games, dice);
        }

        /// <summary>Every game of the season by pair, before it is given a round.</summary>
        private static List<(string Home, string Away)> Meetings(
            IReadOnlyList<ClubData> clubs,
            Random dice
        )
        {
            // each division in a random order, so which rivals meet three times changes from season to season
            List<List<ClubData>> divisions =
            [
                .. clubs
                    .GroupBy(c => (Conference(c), Division(c)))
                    .OrderBy(g => g.Key.Item1, StringComparer.Ordinal)
                    .ThenBy(g => g.Key.Item2, StringComparer.Ordinal)
                    .Select(g => g.OrderBy(c => c.Id, StringComparer.Ordinal).ToList()),
            ];
            foreach (List<ClubData> division in divisions)
            {
                Shuffle(division, dice);
            }

            List<(string, string)> games = [];
            for (int d = 0; d < divisions.Count; d++)
            {
                AddDivision(games, divisions[d]);
                for (int e = d + 1; e < divisions.Count; e++)
                {
                    bool sameConference = string.Equals(
                        Conference(divisions[d][0]),
                        Conference(divisions[e][0]),
                        StringComparison.Ordinal
                    );
                    AddBetween(games, divisions[d], divisions[e], sameConference);
                }
            }
            return games;
        }

        /// <summary>Division rivals: four games, two at each rink, except the next club round the ring, which
        /// meets three times with the first of the pair at home twice.</summary>
        private static void AddDivision(List<(string, string)> games, List<ClubData> division)
        {
            int n = division.Count;
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    string a = division[i].Id,
                        b = division[j].Id;
                    bool ring = n >= 4 && (j == i + 1 || (i == 0 && j == n - 1));
                    if (!ring)
                    {
                        AddGames(games, a, b, 2, 2);
                        continue;
                    }

                    // round the ring each club is host twice against the club after it
                    bool aFirst = j == i + 1;
                    (string twice, string once) = aFirst ? (a, b) : (b, a);
                    AddGames(games, twice, once, 2, 1);
                }
            }
        }

        /// <summary>Clubs of two different divisions: three games in the same conference, the host of two of
        /// them alternating along the division so each club hosts twice against half the other
        /// division; two games, one at each rink, across the conferences.</summary>
        private static void AddBetween(
            List<(string, string)> games,
            List<ClubData> first,
            List<ClubData> second,
            bool sameConference
        )
        {
            for (int i = 0; i < first.Count; i++)
            {
                for (int j = 0; j < second.Count; j++)
                {
                    string a = first[i].Id,
                        b = second[j].Id;
                    if (!sameConference)
                    {
                        AddGames(games, a, b, 1, 1);
                        continue;
                    }

                    bool aTwice =
                        (((j - i) % second.Count) + second.Count) % second.Count < second.Count / 2;
                    AddGames(games, aTwice ? a : b, aTwice ? b : a, 2, 1);
                }
            }
        }

        private static void AddGames(
            List<(string, string)> games,
            string a,
            string b,
            int aHome,
            int bHome
        )
        {
            for (int k = 0; k < aHome; k++)
            {
                games.Add((a, b));
            }

            for (int k = 0; k < bHome; k++)
            {
                games.Add((b, a));
            }
        }

        /// <summary>Gives every game a round: each round takes the open games of the clubs with the most games
        /// left first, keeping a pair apart for <see cref="Rest"/> rounds when other games can fill the
        /// round, until every game is placed.</summary>
        private static List<Fixture> Pack(List<(string Home, string Away)> games, Random dice)
        {
            List<(string Home, string Away, int Order)> open =
            [
                .. games
                    .Select(g => (g.Home, g.Away, Key: dice.Next()))
                    .OrderBy(g => g.Key)
                    .Select((g, i) => (g.Home, g.Away, i)),
            ];
            Dictionary<string, int> left = [with(StringComparer.Ordinal)];
            foreach ((string home, string away, _) in open)
            {
                left[home] = left.GetValueOrDefault(home) + 1;
                left[away] = left.GetValueOrDefault(away) + 1;
            }

            Dictionary<(string, string), int> lastMet = [];
            List<Fixture> fixtures = [];
            int round = -1;
            while (open.Count > 0)
            {
                round++;
                List<(string Home, string Away, int Order)> ranked =
                [
                    .. open.OrderByDescending(g => Math.Max(left[g.Home], left[g.Away]))
                        .ThenByDescending(g => left[g.Home] + left[g.Away])
                        .ThenBy(g => g.Order),
                ];
                HashSet<string> busy = [with(StringComparer.Ordinal)];
                HashSet<int> taken = [];
                for (int pass = 0; pass < 2; pass++)
                {
                    foreach ((string home, string away, int order) in ranked)
                    {
                        (string, string) pair = Pair(home, away);
                        bool rested =
                            pass == 1
                            || !lastMet.TryGetValue(pair, out int met)
                            || round - met > Rest;
                        if (
                            rested
                            && !taken.Contains(order)
                            && !busy.Contains(home)
                            && !busy.Contains(away)
                        )
                        {
                            _ = busy.Add(home);
                            _ = busy.Add(away);
                            _ = taken.Add(order);
                            lastMet[pair] = round;
                            left[home]--;
                            left[away]--;
                            fixtures.Add(new Fixture(fixtures.Count, round, home, away));
                        }
                    }
                }
                _ = open.RemoveAll(g => taken.Contains(g.Order));
            }
            return fixtures;
        }

        private static (string, string) Pair(string a, string b)
        {
            return string.CompareOrdinal(a, b) < 0 ? (a, b) : (b, a);
        }

        private static void Shuffle(List<ClubData> list, Random dice)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int k = dice.Next(i + 1);
                (list[i], list[k]) = (list[k], list[i]);
            }
        }

        private static string Conference(ClubData club)
        {
            return club.Conference.Or("");
        }

        private static string Division(ClubData club)
        {
            return club.Division.Or("");
        }
    }
}
