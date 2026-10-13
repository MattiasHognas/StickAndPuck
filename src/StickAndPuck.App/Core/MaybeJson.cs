using System.Text.Json;
using System.Text.Json.Serialization;

namespace StickAndPuck.App.Core;

/// <summary>Saves <see cref="Maybe{T}"/> the way a nullable was saved: None is JSON null, Some is the value.</summary>
public sealed class MaybeJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc/>
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Maybe<>);

    /// <inheritdoc/>
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converter = typeof(MaybeJsonConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[0]);
        return (JsonConverter)Activator.CreateInstance(converter)!;
    }
}

/// <summary>Reads and writes one <see cref="Maybe{T}"/>.</summary>
public sealed class MaybeJsonConverter<T> : JsonConverter<Maybe<T>>
    where T : notnull
{
    /// <inheritdoc/>
    public override bool HandleNull => true;

    /// <inheritdoc/>
    public override Maybe<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return Maybe<T>.None;
        return JsonSerializer.Deserialize<T>(ref reader, options) is { } value ? Maybe.Some(value) : Maybe<T>.None;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Maybe<T> value, JsonSerializerOptions options)
    {
        if (value.TryGet(out var v)) JsonSerializer.Serialize(writer, v, options);
        else writer.WriteNullValue();
    }
}
