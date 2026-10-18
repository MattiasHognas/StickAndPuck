using System.Text.Json.Serialization;

namespace StickAndPuck.App.Core
{
    /// <summary>The league a club plays in; club files write it as "SHL" or "NHL".</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<League>))]
    public enum League
    {
        /// <summary>The Swedish Hockey League: the 14 clubs in data/clubs/ (the default).</summary>
        [JsonStringEnumMemberName("SHL")]
        Shl,

        /// <summary>The National Hockey League: the 32 clubs in data/clubs/nhl/ (W115).</summary>
        [JsonStringEnumMemberName("NHL")]
        Nhl,
    }
}
