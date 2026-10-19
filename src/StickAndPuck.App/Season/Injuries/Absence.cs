using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.Injuries
{
    /// <summary>One season player who misses his club's next games: injured or suspended. Known by club id and
    /// jersey number, as the stats are. Saved inside season.json (<see cref="AbsenceTable"/>).</summary>
    public sealed class Absence
    {
        /// <summary>Gets or sets the club id.</summary>
        public string Club { get; set; } = "";

        /// <summary>Gets or sets the jersey number.</summary>
        public int Number { get; set; }

        /// <summary>Gets or sets why he is out.</summary>
        public AbsenceReason Reason { get; set; }

        /// <summary>Gets or sets what hurt him; <see cref="InjuryCause.None"/> for an absence that is not an injury.</summary>
        public InjuryCause Cause { get; set; }

        /// <summary>Gets or sets the games he was given when it started.</summary>
        public int Games { get; set; }

        /// <summary>Gets or sets the games of his club he still misses; he is back when it reaches 0.</summary>
        public int GamesLeft { get; set; }

        /// <summary>Gets a value indicating whether this is the given player's absence.</summary>
        public bool Is(string club, int number)
        {
            return Number == number && string.Equals(Club, club, StringComparison.Ordinal);
        }
    }
}
