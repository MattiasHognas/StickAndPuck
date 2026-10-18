using System.Text.Json.Serialization;

namespace StickAndPuck.App.Core
{
    /// <summary>Language dictionaries preserve their literal keys and values.</summary>
    [JsonSerializable(typeof(Dictionary<string, string>))]
    internal sealed partial class TextJsonContext : JsonSerializerContext;
}
