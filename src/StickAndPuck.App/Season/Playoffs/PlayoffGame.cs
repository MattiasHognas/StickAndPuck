using System.Text.Json.Serialization;
using StickAndPuck.App.Season.Schedule;

namespace StickAndPuck.App.Season.Playoffs
{
    /// <summary>One played playoff game. Playoff games always have a winner.</summary>
    public sealed record PlayoffGame(string Home, string Away, FixtureResult Result)
    {
        /// <summary>Gets the winning club's id.</summary>
        [JsonIgnore]
        public string Winner => Result.HomeGoals > Result.AwayGoals ? Home : Away;
    }
}
