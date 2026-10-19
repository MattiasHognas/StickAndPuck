namespace StickAndPuck.App.Season.News
{
    /// <summary>One card of the news deck in data/season/news.json.</summary>
    public sealed record NewsCard
    {
        /// <summary>Gets the card's id; a card already running is not drawn again.</summary>
        public string Id { get; init; } = "";

        /// <summary>Gets how likely the card is against the others; 0 never draws it.</summary>
        public int Weight { get; init; } = 1;

        /// <summary>Gets what the card does.</summary>
        public NewsEffect Effect { get; init; }

        /// <summary>Gets which skaters a traits card hits.</summary>
        public NewsPick Pick { get; init; }

        /// <summary>Gets which next game the card can come before.</summary>
        public NewsVenue Venue { get; init; }

        /// <summary>Gets the fewest skaters a traits card hits.</summary>
        public int PlayersMin { get; init; } = 1;

        /// <summary>Gets the most skaters a traits card hits.</summary>
        public int PlayersMax { get; init; } = 1;

        /// <summary>Gets the fewest games the card lasts.</summary>
        public int GamesMin { get; init; } = 1;

        /// <summary>Gets the most games the card lasts.</summary>
        public int GamesMax { get; init; } = 1;

        /// <summary>Gets the trait points (traits) or morale points (morale) the card moves.</summary>
        public int Amount { get; init; }

        /// <summary>Gets the energy, 0 to 1, the club starts with (energy).</summary>
        public float Energy { get; init; } = 1f;

        /// <summary>Gets the string key of the news line: {0} the players' names, {1} the games left.</summary>
        public string Text { get; init; } = "";
    }
}
