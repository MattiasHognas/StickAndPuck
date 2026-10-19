using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Schedule;

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
