using System.Text.Json.Serialization;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.Playoffs
{
    /// <summary>One playoff series between a higher and a lower seed. In a best of seven the higher seed is at
    /// home in games 1, 3, 5 and 7, or 1, 2, 5 and 7 under <see cref="PlayoffHomeIce.TwoTwoOneOneOne"/>
    /// (NHL); in the best of three play-in the lower seed hosts game 1 and the
    /// higher seed games 2 and 3, as in the SHL.</summary>
    public sealed class PlayoffSeries
    {
        /// <summary>Gets or sets the higher seed's place in the final table, from 1.</summary>
        public int HighSeed { get; set; }

        /// <summary>Gets or sets the lower seed's place in the final table, from 1.</summary>
        public int LowSeed { get; set; }

        /// <summary>Gets or sets the higher seed's club id.</summary>
        public string High { get; set; } = "";

        /// <summary>Gets or sets the lower seed's club id.</summary>
        public string Low { get; set; } = "";

        /// <summary>Gets or sets the most games the series can take: 3 or 7.</summary>
        public int BestOf { get; set; }

        /// <summary>Gets or sets which games of a best of seven the higher seed hosts, from the season's rules.</summary>
        public PlayoffHomeIce HomeIce { get; set; } = PlayoffHomeIce.Alternating;

        /// <summary>Gets or sets the games played so far.</summary>
        public IList<PlayoffGame> Games { get; set; } = [];

        /// <summary>Gets the wins needed to take the series.</summary>
        [JsonIgnore]
        public int WinsNeeded => (BestOf / 2) + 1;

        /// <summary>Gets the higher seed's wins.</summary>
        [JsonIgnore]
        public int HighWins => Games.Count(g => Same(g.Winner, High));

        /// <summary>Gets the lower seed's wins.</summary>
        [JsonIgnore]
        public int LowWins => Games.Count - HighWins;

        /// <summary>Gets the winner's club id, or None while the series is open.</summary>
        [JsonIgnore]
        public Maybe<string> Winner =>
            (HighWins >= WinsNeeded)
                ? ((Maybe<string>)High)
                : ((LowWins >= WinsNeeded) ? ((Maybe<string>)Low) : Maybe.None<string>());

        /// <summary>Gets a value indicating whether one club has won the series.</summary>
        [JsonIgnore]
        public bool IsOver => Winner.HasValue;

        /// <summary>Gets the club at home in the next game.</summary>
        [JsonIgnore]
        public string NextHome => HighHosts(Games.Count) ? High : Low;

        /// <summary>Gets the club away in the next game.</summary>
        [JsonIgnore]
        public string NextAway => HighHosts(Games.Count) ? Low : High;

        /// <summary>Gets a value indicating whether a club plays in this series.</summary>
        public bool Involves(string clubId)
        {
            return Same(High, clubId) || Same(Low, clubId);
        }

        /// <summary>Gets the seed a club has in this series.</summary>
        public int SeedOf(string clubId)
        {
            return Same(High, clubId) ? HighSeed : (Same(Low, clubId) ? LowSeed : 0);
        }

        /// <summary>Gets a value indicating whether the higher seed is at home in a game, from 0.</summary>
        public bool HighHosts(int game)
        {
            return BestOf == 3 ? game != 0
                : HomeIce == PlayoffHomeIce.TwoTwoOneOneOne ? game is 0 or 1 or 4 or 6
                : game % 2 == 0;
        }

        private static bool Same(string a, string b)
        {
            return string.Equals(a, b, StringComparison.Ordinal);
        }
    }
}
