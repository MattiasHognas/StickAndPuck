using System.Text.Json.Serialization;

namespace StickAndPuck.App.Core
{
    /// <summary>One player in a club's roster.</summary>
    public sealed record PlayerData
    {
        /// <summary>Gets the jersey number.</summary>
        public int Number { get; init; }

        /// <summary>Gets the first name.</summary>
        public string FirstName { get; init; } = "";

        /// <summary>Gets the last name, shown on the HUD.</summary>
        public string LastName { get; init; } = "";

        /// <summary>Gets the playing position.</summary>
        public PlayingPosition Position { get; init; }

        /// <summary>Gets the stick hand.</summary>
        public Hand Shoots { get; init; } = Hand.Left;

        /// <summary>Gets the player's age in years at the start of the season (W132), or 0 when the club file has
        /// none: such a player neither develops nor declines. Left out of the written JSON when 0.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Age { get; init; }

        /// <summary>Gets the player's traits.</summary>
        public Traits Traits { get; init; } = new();
    }
}
