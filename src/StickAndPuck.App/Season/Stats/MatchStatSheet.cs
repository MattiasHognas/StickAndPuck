using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.Stats
{
    /// <summary>Every player's line for one match, and the player of the game.</summary>
    public sealed class MatchStatSheet
    {
        /// <summary>Gets or sets the home club id.</summary>
        public string Home { get; set; } = "";

        /// <summary>Gets or sets the away club id.</summary>
        public string Away { get; set; } = "";

        /// <summary>Gets or sets the home goals on the final score.</summary>
        public int HomeGoals { get; set; }

        /// <summary>Gets or sets the away goals on the final score.</summary>
        public int AwayGoals { get; set; }

        /// <summary>Gets or sets every player's line, home club first.</summary>
        public ICollection<MatchStatLine> Lines { get; set; } = [];

        /// <summary>Gets or sets the player of the game, None only when nobody played.</summary>
        public Maybe<MatchStatLine> PlayerOfGame { get; set; }

        /// <summary>Gets or sets the biggest lead the home club held during the game (0 in a sheet settled on paper).</summary>
        public int HomeMaxLead { get; set; }

        /// <summary>Gets or sets the biggest lead the away club held during the game (0 in a sheet settled on paper).</summary>
        public int AwayMaxLead { get; set; }

        /// <summary>Gets the biggest lead a side held during the game.</summary>
        public int MaxLead(Side side)
        {
            return side == Side.Home ? HomeMaxLead : AwayMaxLead;
        }

        /// <summary>Gets a value indicating whether a side won the match.</summary>
        public bool Won(Side side)
        {
            return side == Side.Home ? HomeGoals > AwayGoals : AwayGoals > HomeGoals;
        }

        /// <summary>Gets the lines of one side, best first.</summary>
        public IEnumerable<MatchStatLine> Of(Side side)
        {
            return Lines.Where(l => l.Side == side).OrderByDescending(l => Merit(l, this));
        }

        /// <summary>Gets the award for the player of the game, for the season's or the run's list.</summary>
        public Maybe<PlayerOfGameAward> Award(string game)
        {
            return PlayerOfGame.TryGet(out MatchStatLine s)
                ? new PlayerOfGameAward(
                    game,
                    Home,
                    Away,
                    HomeGoals,
                    AwayGoals,
                    s.Club,
                    s.Number,
                    s.Name,
                    s.Goals,
                    s.Assists,
                    s.Saves
                )
                : Maybe.None<PlayerOfGameAward>();
        }

        /// <summary>Picks the player of the game: the line with the most merit, where a goal is worth 3, an
        /// assist 2, a shot or a hit a little, a goalie's saves add up and a shutout counts a lot;
        /// the winners get an edge, so the star is nearly always on the winning side.</summary>
        public void PickPlayerOfGame()
        {
            PlayerOfGame = Lines
                .Where(l => l.Played)
                .OrderByDescending(l => Merit(l, this))
                .ThenByDescending(l => l.Points)
                .ThenBy(l => l.Number)
                .FirstOrNone();
        }

        /// <summary>How much one line earned the award.</summary>
        private static float Merit(MatchStatLine l, MatchStatSheet sheet)
        {
            float m;
            if (l.IsGoalie)
            {
                int against = l.Side == Side.Home ? sheet.AwayGoals : sheet.HomeGoals;
                m = (l.Saves * 0.18f) - (against * 1.2f) + (against == 0 && l.Saves > 0 ? 3f : 0f);
            }
            else
            {
                m = (l.Goals * 3f) + (l.Assists * 2f) + (l.Shots * 0.25f) + (l.Hits * 0.3f);
            }
            return m + (sheet.Won(l.Side) ? 1f : 0f);
        }
    }
}
