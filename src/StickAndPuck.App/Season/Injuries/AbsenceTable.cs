using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Stats;

namespace StickAndPuck.App.Season.Injuries
{
    /// <summary>The season's unavailable players: everyone injured (W128) or suspended (W129) for some of his
    /// club's next games, saved inside season.json with the stats (an older save reads with an empty list:
    /// everyone is fit). Every credited game counts its two clubs' absences down by one and then adds the
    /// absences the game itself handed out (<see cref="MatchStatSheet.Absences"/>), so a player hurt in a
    /// game misses exactly the next games of his club. A season match, and a game settled on paper, is
    /// played by the club <see cref="Without(ClubData)"/> them: a spare (W86) of the same group, forward or
    /// defence, steps into each absent skater's slot while the club has one left (<see cref="Dressed"/>); after
    /// that the lines and pairs are built from who is left, the players behind moving up, and the fourth line
    /// and the last pair fill the gaps (<see cref="Lines.Squad.Build"/>). A club always keeps
    /// <see cref="MinSkaters"/> skaters, spares counted, and a goalie.</summary>
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
        /// absent, spares too, unless that would leave fewer than <see cref="MinSkaters"/> skaters (spares
        /// counted) or no goalie, when those with the fewest games left play after all.</summary>
        public IReadOnlyList<string> Out(ClubData club)
        {
            int skaters = club.Everyone().Count(p => p.Position != PlayingPosition.G);
            int goalies = club.Everyone().Count(p => p.Position == PlayingPosition.G);
            List<string> keys = [];
            foreach (
                PlayerData p in club.Everyone()
                    .Where(p => GamesLeft(club.Id, p.Number) > 0)
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

        /// <summary>The club as its next game dresses it: the roster without the players <see cref="Out(ClubData)"/>,
        /// the spares in their slots (<see cref="Dressed"/>).</summary>
        public ClubData Without(ClubData club)
        {
            return Dressed(club, Out(club));
        }

        /// <summary>The club as a game dresses it without the players in the list, by <see cref="FormTable.Key"/>:
        /// each absent skater's roster slot goes to a spare who is not in the list (<see cref="StepIns"/>), and
        /// the slots no spare is left for are dropped, so the players behind move up. The spares who step in
        /// leave <see cref="ClubData.Spares"/>; with everyone fit the club is returned as it is.</summary>
        public static ClubData Dressed(ClubData club, IReadOnlyCollection<string> keys)
        {
            IReadOnlyList<SpareIn> stepIns = StepIns(club, keys);
            ClubData without = Without(club, keys);
            if (stepIns.Count == 0)
            {
                return without;
            }

            List<PlayerData> roster = [];
            foreach (PlayerData p in club.Roster)
            {
                if (stepIns.FirstOrNone(s => ReferenceEquals(s.For, p)).TryGet(out SpareIn step))
                {
                    roster.Add(step.Spare with { Position = p.Position });
                }
                else if (without.Roster.Contains(p))
                {
                    roster.Add(p);
                }
            }

            return without with
            {
                Roster = roster,
                Spares =
                [
                    .. club.Spares.Where(s => !stepIns.Any(i => ReferenceEquals(i.Spare, s))),
                ],
            };
        }

        /// <summary>Gets the spares who step in for the players in the list, by <see cref="FormTable.Key"/>: the absent
        /// skaters in roster order (the top line first) each take the first fit spare of their own
        /// position, else of their group (forward or defence), while one is left. Goalies have no spares.</summary>
        public static IReadOnlyList<SpareIn> StepIns(
            ClubData club,
            IReadOnlyCollection<string> keys
        )
        {
            HashSet<string> left = new(keys, StringComparer.Ordinal);
            List<PlayerData> spares =
            [
                .. club.Spares.Where(p =>
                    p.Position != PlayingPosition.G
                    && !left.Contains(FormTable.Key(club.Id, p.Number))
                ),
            ];
            List<SpareIn> stepIns = [];
            foreach (
                PlayerData p in club.Roster.Where(p =>
                    p.Position != PlayingPosition.G
                    && left.Contains(FormTable.Key(club.Id, p.Number))
                )
            )
            {
                if (
                    spares.FirstOrNone(s => s.Position == p.Position).TryGet(out PlayerData spare)
                    || spares.FirstOrNone(s => Defence(s) == Defence(p)).TryGet(out spare)
                )
                {
                    stepIns.Add(new SpareIn(spare, p));
                    _ = spares.Remove(spare);
                }
            }

            return stepIns;
        }

        private static bool Defence(PlayerData p)
        {
            return p.Position == PlayingPosition.D;
        }

        /// <summary>The club without the players in the list, by <see cref="FormTable.Key"/>; the rest keep their
        /// roster order and no spare steps in. A replay recorded before clubs had spares (W86) dresses its clubs this way.</summary>
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
