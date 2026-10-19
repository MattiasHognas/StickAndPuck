using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Schedule;
using StickAndPuck.App.Season.Stats;

namespace StickAndPuck.App.Season.DressingRoom
{
    /// <summary>What a <see cref="IDressingRoomSource"/> reads to fill the dressing room: the season as saved,
    /// the club data, the human's last game and the season's rules (the Milestone 10 switches and strengths).</summary>
    /// <param name="Season">The season being played.</param>
    /// <param name="Clubs">Every club's data.</param>
    /// <param name="Last">The human's last game, or None before the first.</param>
    /// <param name="Rules">The rules the season plays.</param>
    public sealed record DressingRoomContext(
        SeasonState Season,
        IReadOnlyList<ClubData> Clubs,
        Maybe<LastGame> Last,
        RuleSet Rules
    )
    {
        /// <summary>Gets the human's club id.</summary>
        public string UserClub => Season.UserClub;

        /// <summary>Builds the context for a season.</summary>
        public static DressingRoomContext Of(
            SeasonState season,
            IReadOnlyList<ClubData> clubs,
            RuleSet rules
        )
        {
            return new(season, clubs, LastGame.Of(season), rules);
        }

        /// <summary>Gets the stat sheet of the last game: the one the human played when the save has it, else
        /// the paper sheet of a simmed game (the same lines the leaders page was credited with); None before
        /// the first game or when a club has no data.</summary>
        public Maybe<MatchStatSheet> Sheet()
        {
            if (!Last.TryGet(out LastGame game))
            {
                return Maybe.None<MatchStatSheet>();
            }

            SeasonStats stats = Season.Stats;
            if (
                game.Key.Length > 0
                && string.Equals(stats.LastPlayedKey, game.Key, StringComparison.Ordinal)
                && stats.LastPlayed.TryGet(out MatchStatSheet played)
            )
            {
                return played;
            }

            Maybe<ClubData> home = Clubs.FirstOrNone(c =>
                string.Equals(c.Id, game.Home, StringComparison.Ordinal)
            );
            Maybe<ClubData> away = Clubs.FirstOrNone(c =>
                string.Equals(c.Id, game.Away, StringComparison.Ordinal)
            );
            return home.TryGet(out ClubData h) && away.TryGet(out ClubData a)
                ? SyntheticStats.Sheet(h, a, game.Result, game.Seed)
                : Maybe.None<MatchStatSheet>();
        }

        /// <summary>Gets a club's name, or its id when it has no data file.</summary>
        public string Name(string clubId)
        {
            return Clubs
                .FirstOrNone(c => string.Equals(c.Id, clubId, StringComparison.Ordinal))
                .Select(c => c.Name)
                .Or(clubId);
        }
    }
}
