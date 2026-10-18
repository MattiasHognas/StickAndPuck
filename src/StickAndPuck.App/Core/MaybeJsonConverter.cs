using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace StickAndPuck.App.Core
{
    /// <summary>Reads and writes one <see cref="Maybe{T}"/>.</summary>
    public sealed class MaybeJsonConverter<T> : JsonConverter<Maybe<T>>
        where T : notnull
    {
        private static JsonTypeInfo<T> Contract(JsonSerializerOptions options)
        {
            return options.GetTypeInfo<T>();
        }

        /// <inheritdoc/>
        public override bool HandleNull => true;

        /// <inheritdoc/>
        public override Maybe<T> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options
        )
        {
            return (reader.TokenType == JsonTokenType.Null)
                ? Maybe.None<T>()
                : (
                    (JsonSerializer.Deserialize(ref reader, Contract(options)) is { } value)
                        ? Maybe.Some(value)
                        : Maybe.None<T>()
                );
        }

        /// <inheritdoc/>
        public override void Write(
            Utf8JsonWriter writer,
            Maybe<T> value,
            JsonSerializerOptions options
        )
        {
            if (value.TryGet(out T v))
            {
                JsonSerializer.Serialize(writer, v, Contract(options));
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}
