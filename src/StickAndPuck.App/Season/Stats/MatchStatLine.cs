using System.Globalization;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.Stats
{
    /// <summary>What one player did in one match.</summary>
    public sealed class MatchStatLine
    {
        /// <summary>Gets or sets the club id.</summary>
        public string Club { get; set; } = "";

        /// <summary>Gets or sets the club side in that match.</summary>
        public Side Side { get; set; }

        /// <summary>Gets or sets the jersey number.</summary>
        public int Number { get; set; }

        /// <summary>Gets or sets the last name.</summary>
        public string Name { get; set; } = "";

        /// <summary>Gets or sets the playing position.</summary>
        public PlayingPosition Position { get; set; }

        /// <summary>Gets or sets a value indicating whether the player was on the ice at some point.</summary>
        public bool Played { get; set; }

        /// <summary>Gets or sets the goals.</summary>
        public int Goals { get; set; }

        /// <summary>Gets or sets the assists.</summary>
        public int Assists { get; set; }

        /// <summary>Gets or sets the shots.</summary>
        public int Shots { get; set; }

        /// <summary>Gets or sets the body checks landed.</summary>
        public int Hits { get; set; }

        /// <summary>Gets or sets the penalty-kill clears: shots a man short from the club's own half.</summary>
        public int Clears { get; set; }

        /// <summary>Gets or sets the saves, for goalies.</summary>
        public int Saves { get; set; }

        /// <summary>Gets the points.</summary>
        public int Points => Goals + Assists;

        /// <summary>Gets a value indicating whether this is a goalie's line.</summary>
        public bool IsGoalie => Position == PlayingPosition.G;

        /// <summary>Gets "#NR NAME".</summary>
        public string Label => string.Create(CultureInfo.InvariantCulture, $"#{Number} {Name}");

        /// <summary>A line for a roster player, nothing done yet.</summary>
        public static MatchStatLine For(string club, Side side, PlayerData player)
        {
            return new MatchStatLine
            {
                Club = club,
                Side = side,
                Number = player.Number,
                Name = player.LastName,
                Position = player.Position,
            };
        }
    }
}
