namespace StickAndPuck.App.Season.Stats
{
    /// <summary>One season player's development (W132): the trait points age, ice time and form have added
    /// (young) or taken (veteran) so far this season, before the rules' strength scales them. Known by club
    /// id and jersey number, as <see cref="PlayerForm"/> is.</summary>
    public sealed class PlayerDevelopment
    {
        /// <summary>Gets or sets the club id.</summary>
        public string Club { get; set; } = "";

        /// <summary>Gets or sets the jersey number.</summary>
        public int Number { get; set; }

        /// <summary>Gets or sets the development so far, in unscaled trait points.</summary>
        public double Value { get; set; }

        /// <summary>Gets or sets <see cref="Value"/> before the last game that moved it.</summary>
        public double Before { get; set; }

        /// <summary>Gets or sets the stats key of the last game that moved it ("g12", "p0.1.2").</summary>
        public string Game { get; set; } = "";

        /// <summary>Gets a value indicating whether this is the given player's development.</summary>
        public bool Is(string club, int number)
        {
            return Number == number && string.Equals(Club, club, StringComparison.Ordinal);
        }
    }
}
