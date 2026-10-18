using System.Text.Json.Serialization;

namespace StickAndPuck.App.AI.Tactics
{
    /// <summary>A club's style on top of its forecheck and zone entry: how hard it hits, how readily it
    /// shoots and how it defends its own zone.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<PlayStyle>))]
    public enum PlayStyle
    {
        /// <summary>No style of its own: the AI as it plays without one.</summary>
        Balanced,

        /// <summary>Finishes its checks: the skater on the carrier lines up a hit far more often, and the skater nearest the carrier goes at him.</summary>
        Physical,

        /// <summary>Shoots from anywhere in the attacking zone with a lane, rather than working it in for a better chance.</summary>
        ShootFromAnywhere,

        /// <summary>Defensive shell: collapses around its own net while the other side has the puck in its zone,
        /// and with a balanced forecheck sends nobody in on the breakout but waits back in its own half.</summary>
        DefensiveShell,
    }
}
