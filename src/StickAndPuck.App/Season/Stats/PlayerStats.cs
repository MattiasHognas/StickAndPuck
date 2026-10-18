using System.Globalization;
using System.Text.Json.Serialization;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.Stats
{
    /// <summary>One player's totals over a season, a playoff run or an arcade run. A player is known by club
    /// id and jersey number, so the line survives a restart and a roster edit that keeps the number.</summary>
    public sealed class PlayerStats
    {
        /// <summary>Gets or sets the club id.</summary>
        public string Club { get; set; } = "";

        /// <summary>Gets or sets the jersey number.</summary>
        public int Number { get; set; }

        /// <summary>Gets or sets the last name, as the HUD shows it.</summary>
        public string Name { get; set; } = "";

        /// <summary>Gets or sets the playing position.</summary>
        public PlayingPosition Position { get; set; }

        /// <summary>Gets or sets the games played (on the ice at least once).</summary>
        public int Games { get; set; }

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

        /// <summary>Gets or sets how often the player was player of the game.</summary>
        public int Stars { get; set; }

        /// <summary>Gets the points: goals plus assists.</summary>
        [JsonIgnore]
        public int Points => Goals + Assists;

        /// <summary>Gets a value indicating whether this is a goalie's line.</summary>
        [JsonIgnore]
        public bool IsGoalie => Position == PlayingPosition.G;

        /// <summary>Gets "#NR NAME".</summary>
        [JsonIgnore]
        public string Label => string.Create(CultureInfo.InvariantCulture, $"#{Number} {Name}");

        /// <summary>Gets a value indicating whether this line is for the given player.</summary>
        public bool Is(string club, int number)
        {
            return Number == number && string.Equals(Club, club, StringComparison.Ordinal);
        }

        /// <summary>Adds a match's line to this one.</summary>
        public void Add(MatchStatLine line)
        {
            if (line.Played)
            {
                Games++;
            }

            Goals += line.Goals;
            Assists += line.Assists;
            Shots += line.Shots;
            Hits += line.Hits;
            Clears += line.Clears;
            Saves += line.Saves;
        }
    }
}
