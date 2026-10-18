using System.Text.Json.Serialization;

namespace StickAndPuck.App.Season.Playoffs
{
    /// <summary>One playoff round: its name, its series length and its series, best seed first.</summary>
    public sealed class PlayoffRound
    {
        /// <summary>Gets or sets the round's name.</summary>
        public string Name { get; set; } = "";

        /// <summary>Gets or sets the series length: 3 or 7.</summary>
        public int BestOf { get; set; }

        /// <summary>Gets or sets the series, the top seed's first.</summary>
        public IList<PlayoffSeries> Series { get; set; } = [];

        /// <summary>Gets a value indicating whether every series in the round has a winner.</summary>
        [JsonIgnore]
        public bool IsOver => Series.All(s => s.IsOver);
    }
}
