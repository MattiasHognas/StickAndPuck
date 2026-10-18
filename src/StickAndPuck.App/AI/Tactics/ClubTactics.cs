using System.Text.Json.Serialization;

namespace StickAndPuck.App.AI.Tactics
{
    /// <summary>How a club plays, from the <c language="C#">tactics</c> field of its file in data/clubs/. A club with
    /// no field plays balanced, carries the puck in, mixes its power play and has no style of its own
    /// (a missing <c language="C#">style</c> is balanced too). An unknown key or value
    /// fails the club load, as an unknown trait does.</summary>
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record ClubTactics
    {
        /// <summary>Gets how the club plays the other side's breakout.</summary>
        public Forecheck Forecheck { get; init; } = Forecheck.Balanced;

        /// <summary>Gets how the club gets the puck into the attacking zone.</summary>
        public ZoneEntry Entry { get; init; } = ZoneEntry.Carry;

        /// <summary>Gets the power play shape the club sets up, or mixed for either.</summary>
        public PowerPlayShape PowerPlay { get; init; } = PowerPlayShape.Mixed;

        /// <summary>Gets the club's style on top of its forecheck and entry: physical, shooting from anywhere,
        /// a defensive shell, or balanced (none of its own) when the file leaves it out.</summary>
        public PlayStyle Style { get; init; } = PlayStyle.Balanced;
    }
}
