namespace StickAndPuck.App.Season.Stats
{
    /// <summary>One season player's carry-over form: a hidden value from -5 (ice cold) to +5 (red hot) that
    /// follows their recent games. Known by club id and jersey number, as <see cref="PlayerStats"/> is.</summary>
    public sealed class PlayerForm
    {
        /// <summary>Gets or sets the club id.</summary>
        public string Club { get; set; } = "";

        /// <summary>Gets or sets the jersey number.</summary>
        public int Number { get; set; }

        /// <summary>Gets or sets the form, -5 to +5.</summary>
        public double Value { get; set; }

        /// <summary>Gets a value indicating whether this is the given player's form.</summary>
        public bool Is(string club, int number)
        {
            return Number == number && string.Equals(Club, club, StringComparison.Ordinal);
        }
    }
}
