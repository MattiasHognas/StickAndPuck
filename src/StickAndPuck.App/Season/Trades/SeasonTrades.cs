using System.Text.Json.Serialization;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.Trades
{
    /// <summary>A season's trade deadline (W135), as its save holds it: whether the deadline has come, the offers
    /// the human's club got and the trades made. The club data files stay the base; <see cref="Deals"/>
    /// are the deltas (<see cref="TradeDeals.Apply"/>).</summary>
    public sealed class SeasonTrades
    {
        /// <summary>Gets or sets a value indicating whether the deadline has come (its offers were made, if any).</summary>
        public bool Held { get; set; }

        /// <summary>Gets or sets the offers, in the order they came.</summary>
        public IList<TradeOffer> Offers { get; set; } = [];

        /// <summary>Gets or sets the trades made, in order.</summary>
        public IList<Trade> Deals { get; set; } = [];

        /// <summary>Gets the offers still waiting for an answer.</summary>
        [JsonIgnore]
        public IReadOnlyList<TradeOffer> Open => [.. Offers.Where(o => o.IsOpen)];
    }
}
