using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Stats;

namespace StickAndPuck.App.Season.Injuries
{
    /// <summary>The season's unavailable players: everyone injured (W128) or suspended (W129) for some of his
    /// club's next games, saved inside season.json with the stats (an older save reads with an empty list:
    /// everyone is fit). Every credited game counts its two clubs' absences down by one and then adds the
    /// absences the game itself handed out (<see cref="MatchStatSheet.Absences"/>), so a player hurt in a
    /// game misses exactly the next games of his club. A season match, and a game settled on paper, is
    /// played by the club <see cref="Without(ClubData)"/> them: the lines and pairs are built from who is
    /// left, the players behind moving up, and the fourth line and the last pair fill the gaps
    /// (<see cref="Lines.Squad.Build"/>). A club always keeps <see cref="MinSkaters"/> skaters and a goalie.</summary>
    public sealed class AbsenceTable
    {
        /// <summary>The fewest skaters a club dresses: below this the players with the fewest games left play after all.</summary>
        public const int MinSkaters = 12;

        /// <summary>Gets or sets every absence still running, in no particular order.</summary>
        public ICollection<Absence> Players { get; set; } = [];

        /// <summary>Gets a value indicating whether a player misses his club's next game.</summary>
        public bool IsOut(string club, int number)
        {
            return Players.Any(a => a.Is(club, number) && a.GamesLeft > 0);
        }

        /// <summary>Gets a club's absences, the longest first.</summary>
        public IReadOnlyList<Absence> Of(string club)
        {
            return
            [
                .. Players
                    .Where(a => string.Equals(a.Club, club, StringComparison.Ordinal))
                    .OrderByDescending(a => a.GamesLeft)
                    .ThenBy(a => a.Number),
            ];
        }

        /// <summary>Adds an absence; a player already out for the same reason keeps the longer of the two.</summary>
        public void Add(Absence absence)
        {
            if (absence.GamesLeft <= 0)
            {
                return;
            }

            if (
                Players
                    .FirstOrNone(a =>
                        a.Is(absence.Club, absence.Number) && a.Reason == absence.Reason
                    )
                    .TryGet(out Absence found)
            )
            {
                if (absence.GamesLeft > found.GamesLeft)
                {
                    found.Games = absence.Games;
                    found.GamesLeft = absence.GamesLeft;
                    found.Cause = absence.Cause;
                }

                return;
            }

            Players.Add(
                new Absence
                {
                    Club = absence.Club,
                    Number = absence.Number,
                    Reason = absence.Reason,
                    Cause = absence.Cause,
                    Games = absence.Games,
                    GamesLeft = absence.GamesLeft,
                }
            );
        }

        /// <summary>One credited game: its two clubs' absences count down by one (a player back at 0 leaves the
        /// list), then the absences the game handed out are added.</summary>
        public void Play(MatchStatSheet sheet)
        {
            foreach (
                Absence a in Players
                    .Where(a =>
                        string.Equals(a.Club, sheet.Home, StringComparison.Ordinal)
                        || string.Equals(a.Club, sheet.Away, StringComparison.Ordinal)
                    )
                    .ToList()
            )
            {
                a.GamesLeft--;
                if (a.GamesLeft <= 0)
                {
                    _ = Players.Remove(a);
                }
            }

            foreach (Absence a in sheet.Absences)
            {
                Add(a);
            }
        }

        /// <summary>Gets the players a club leaves out of its next game, by <see cref="FormTable.Key"/>: everyone
        /// absent, unless that would leave fewer than <see cref="MinSkaters"/> skaters or no goalie, when
        /// those with the fewest games left play after all.</summary>
        public IReadOnlyList<string> Out(ClubData club)
        {
            int skaters = club.Roster.Count(p => p.Position != PlayingPosition.G);
            int goalies = club.Roster.Count(p => p.Position == PlayingPosition.G);
            List<string> keys = [];
            foreach (
                PlayerData p in club
                    .Roster.Where(p => GamesLeft(club.Id, p.Number) > 0)
                    .OrderByDescending(p => GamesLeft(club.Id, p.Number))
            )
            {
                bool goalie = p.Position == PlayingPosition.G;
                if (goalie ? goalies <= 1 : skaters <= MinSkaters)
                {
                    continue;
                }

                if (goalie)
                {
                    goalies--;
                }
                else
                {
                    skaters--;
                }

                keys.Add(FormTable.Key(club.Id, p.Number));
            }

            return keys;
        }

        /// <summary>Gets the games a player still misses, the longest of his absences; 0 when he is fit.</summary>
        public int GamesLeft(string club, int number)
        {
            return Players
                .Where(a => a.Is(club, number))
                .Select(a => a.GamesLeft)
                .DefaultIfEmpty(0)
                .Max();
        }

        /// <summary>The club as its next game dresses it: the roster without the players <see cref="Out(ClubData)"/>.</summary>
        public ClubData Without(ClubData club)
        {
            return Without(club, Out(club));
        }

        /// <summary>The club without the players in the list, by <see cref="FormTable.Key"/>; the rest keep their roster order.</summary>
        public static ClubData Without(ClubData club, IReadOnlyCollection<string> keys)
        {
            HashSet<string> left = new(keys, StringComparer.Ordinal);
            return !club.Roster.Any(p => left.Contains(FormTable.Key(club.Id, p.Number)))
                ? club
                : club with
                {
                    Roster =
                    [
                        .. club.Roster.Where(p => !left.Contains(FormTable.Key(club.Id, p.Number))),
                    ],
                };
        }
    }
}
