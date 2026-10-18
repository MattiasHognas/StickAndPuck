using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.Stats
{
    /// <summary>Every player's totals over a run of games, and the leaders. The leaders page reads
    /// <see cref="Leaders(int)"/> and <see cref="Goalies"/>; the season keeps one of these for the regular
    /// season and one for the playoffs, the arcade run one per run.</summary>
    public sealed class StatTable
    {
        /// <summary>Gets or sets every player with a line, in no particular order.</summary>
        public ICollection<PlayerStats> Players { get; set; } = [];

        /// <summary>Gets or sets the games credited.</summary>
        public int GamesCredited { get; set; }

        /// <summary>Gets the line for a player, adding an empty one if needed.</summary>
        public PlayerStats Line(string club, int number, string name, PlayingPosition position)
        {
            if (Players.FirstOrNone(p => p.Is(club, number)).TryGet(out PlayerStats line))
            {
                return line;
            }

            PlayerStats added = new()
            {
                Club = club,
                Number = number,
                Name = name,
                Position = position,
            };
            Players.Add(added);
            return added;
        }

        /// <summary>Gets a player's line, or None when they have none yet.</summary>
        public Maybe<PlayerStats> Find(string club, int number)
        {
            return Players.FirstOrNone(p => p.Is(club, number));
        }

        /// <summary>Adds a match's lines to the totals.</summary>
        public void Add(MatchStatSheet sheet)
        {
            foreach (MatchStatLine line in sheet.Lines)
            {
                Line(line.Club, line.Number, line.Name, line.Position).Add(line);
            }

            if (sheet.PlayerOfGame.TryGet(out MatchStatLine star))
            {
                Line(star.Club, star.Number, star.Name, star.Position).Stars++;
            }

            GamesCredited++;
        }

        /// <summary>Gets the scoring leaders of every club: by points, then goals, then fewer games, then name.</summary>
        public IReadOnlyList<PlayerStats> Leaders(int count = 10)
        {
            return Leaders(count, Maybe.None<string>());
        }

        /// <summary>Gets the scoring leaders: by points, then goals, then fewer games, then name.</summary>
        public IReadOnlyList<PlayerStats> Leaders(int count, Maybe<string> club)
        {
            return
            [
                .. Players
                    .Where(p =>
                        !p.IsGoalie
                        && club.Select(only =>
                                string.Equals(p.Club, only, StringComparison.Ordinal)
                            )
                            .Or(fallback: true)
                        && p.Points > 0
                    )
                    .OrderByDescending(p => p.Points)
                    .ThenByDescending(p => p.Goals)
                    .ThenBy(p => p.Games)
                    .ThenBy(p => p.Name, StringComparer.Ordinal)
                    .Take(count),
            ];
        }

        /// <summary>Gets the goal scorers, most goals first.</summary>
        public IReadOnlyList<PlayerStats> Scorers(int count = 10)
        {
            return
            [
                .. Players
                    .Where(p => !p.IsGoalie && p.Goals > 0)
                    .OrderByDescending(p => p.Goals)
                    .ThenByDescending(p => p.Points)
                    .ThenBy(p => p.Games)
                    .ThenBy(p => p.Name, StringComparer.Ordinal)
                    .Take(count),
            ];
        }

        /// <summary>Gets the goalies, most saves first.</summary>
        public IReadOnlyList<PlayerStats> Goalies(int count = 10)
        {
            return
            [
                .. Players
                    .Where(p => p.IsGoalie && p.Games > 0)
                    .OrderByDescending(p => p.Saves)
                    .ThenBy(p => p.Games)
                    .ThenBy(p => p.Name, StringComparer.Ordinal)
                    .Take(count),
            ];
        }

        /// <summary>Gets the players most often player of the game.</summary>
        public IReadOnlyList<PlayerStats> Stars(int count = 10)
        {
            return
            [
                .. Players
                    .Where(p => p.Stars > 0)
                    .OrderByDescending(p => p.Stars)
                    .ThenByDescending(p => p.Points)
                    .ThenBy(p => p.Name, StringComparer.Ordinal)
                    .Take(count),
            ];
        }
    }
}
