using System.Text.Json.Serialization;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.Trades
{
    /// <summary>One player-for-player offer an AI club makes to the human's club at the trade deadline (W135), as
    /// the season save holds it. The players are named by their numbers in the rosters as the offer was
    /// made; their last names are kept for the news.</summary>
    public sealed class TradeOffer
    {
        /// <summary>Gets or sets the id of the club making the offer.</summary>
        public string Club { get; set; } = "";

        /// <summary>Gets or sets the position both players play.</summary>
        public PlayingPosition Position { get; set; }

        /// <summary>Gets or sets the number of the human club's player the offer asks for.</summary>
        public int Give { get; set; }

        /// <summary>Gets or sets that player's last name.</summary>
        public string GiveName { get; set; } = "";

        /// <summary>Gets or sets the number of the player the other club offers.</summary>
        public int Get { get; set; }

        /// <summary>Gets or sets that player's last name.</summary>
        public string GetName { get; set; } = "";

        /// <summary>Gets or sets the human club's games played when the offer came.</summary>
        public int Game { get; set; }

        /// <summary>Gets or sets where the offer stands.</summary>
        public TradeAnswer Answer { get; set; } = TradeAnswer.Open;

        /// <summary>Gets a value indicating whether the offer still waits for an answer.</summary>
        [JsonIgnore]
        public bool IsOpen => Answer == TradeAnswer.Open;
    }
}
