using System.Globalization;
using System.Text.Json.Serialization;
using StickAndPuck.App.Core;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Season.Stats
{
    /// <summary>The player of one game, as kept with the season or the arcade run.</summary>
    /// <param name="Game">Which game: "g12" for season game 13, "p0.1.2" for playoff round 1 series 2 game 3, "a3" for arcade stage 4.</param>
    /// <param name="Home">The home club id.</param>
    /// <param name="Away">The away club id.</param>
    /// <param name="HomeGoals">The home goals.</param>
    /// <param name="AwayGoals">The away goals.</param>
    /// <param name="Club">The player's club id.</param>
    /// <param name="Number">The jersey number.</param>
    /// <param name="Name">The last name.</param>
    /// <param name="Goals">Goals in the game.</param>
    /// <param name="Assists">Assists in the game.</param>
    /// <param name="Saves">Saves in the game, for a goalie.</param>
    public sealed record PlayerOfGameAward(
        string Game,
        string Home,
        string Away,
        int HomeGoals,
        int AwayGoals,
        string Club,
        int Number,
        string Name,
        int Goals,
        int Assists,
        int Saves
    )
    {
        /// <summary>Gets "#NR NAME".</summary>
        [JsonIgnore]
        public string Label => string.Create(CultureInfo.InvariantCulture, $"#{Number} {Name}");

        /// <summary>Gets "2G 1A" or "31 SV", what earned the award.</summary>
        [JsonIgnore]
        public string Summary =>
            Saves > 0 && Goals + Assists == 0
                ? Text.Format(K.SummarySaves, Saves)
                : Text.Format(K.SummaryPoints, Goals, Assists);
    }
}
