namespace StickAndPuck.App.Season.News
{
    /// <summary>A news card in play, saved with the season (<see cref="Schedule.SeasonState.News"/>) until its
    /// games are used up.</summary>
    public sealed class NewsEvent
    {
        /// <summary>Gets or sets the card's id.</summary>
        public string Id { get; set; } = "";

        /// <summary>Gets or sets the string key of the news line.</summary>
        public string Text { get; set; } = "";

        /// <summary>Gets or sets what the event does.</summary>
        public NewsEffect Effect { get; set; }

        /// <summary>Gets or sets the club it happens to.</summary>
        public string Club { get; set; } = "";

        /// <summary>Gets or sets the shirt numbers of the skaters a traits event hits.</summary>
        public IList<int> Players { get; set; } = [];

        /// <summary>Gets or sets the trait or morale points.</summary>
        public int Amount { get; set; }

        /// <summary>Gets or sets the energy the club starts a game with, for an energy event.</summary>
        public float Energy { get; set; } = 1f;

        /// <summary>Gets or sets the club's games the event still lasts.</summary>
        public int GamesLeft { get; set; }

        /// <summary>Gets or sets the club's games played when it was drawn.</summary>
        public int Drawn { get; set; }
    }
}
