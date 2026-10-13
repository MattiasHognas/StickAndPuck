using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace StickAndPuck.App.Core;

/// <summary>A value that may be absent, in place of a nullable type (the game bans T? and null, SAP0001/SAP0002).
/// The default is <see cref="None"/>; a value converts to Some by itself.</summary>
[JsonConverter(typeof(MaybeJsonConverterFactory))]
public readonly struct Maybe<T> : IEquatable<Maybe<T>>
    where T : notnull
{
    readonly T value;

    Maybe(T value)
    {
        ArgumentNullException.ThrowIfNull(value);    // a test comparing a Maybe with null fails loudly instead of quietly
        this.value = value;
        HasValue = true;
    }

    /// <summary>Gets the absent value.</summary>
    public static Maybe<T> None => default;

    /// <summary>Gets a value indicating whether a value is present.</summary>
    public bool HasValue { get; }

    /// <summary>Wraps a present value.</summary>
    public static implicit operator Maybe<T>(T value) => new(value);

    /// <summary>Compares two maybes: both absent, or both present with equal values.</summary>
    public static bool operator ==(Maybe<T> left, Maybe<T> right) => left.Equals(right);

    /// <summary>Compares two maybes.</summary>
    public static bool operator !=(Maybe<T> left, Maybe<T> right) => !left.Equals(right);

    /// <summary>Wraps a present value.</summary>
    public static Maybe<T> Some(T value) => new(value);

    /// <summary>Gets the value when present.</summary>
    public bool TryGet(out T result)
    {
        result = value;
        return HasValue;
    }

    /// <summary>Gets the value, or a fallback when absent.</summary>
    public T Or(T fallback) => HasValue ? value : fallback;

    /// <summary>Maps a present value.</summary>
    public Maybe<TOut> Select<TOut>(Func<T, TOut> map)
        where TOut : notnull => HasValue ? map(value) : Maybe<TOut>.None;

    /// <summary>Tells whether a value is present and passes a test.</summary>
    public bool Is(Func<T, bool> test) => HasValue && test(value);

    /// <inheritdoc/>
    public bool Equals(Maybe<T> other) => HasValue == other.HasValue && (!HasValue || EqualityComparer<T>.Default.Equals(value, other.value));

    /// <inheritdoc/>
    public override bool Equals([AllowNull] object obj) => obj is Maybe<T> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HasValue ? EqualityComparer<T>.Default.GetHashCode(value) : 0;

    /// <inheritdoc/>
    public override string ToString() => HasValue ? $"Some({value})" : "None";
}

/// <summary>Builds <see cref="Maybe{T}"/> values.</summary>
public static class Maybe
{
    /// <summary>Wraps a present value.</summary>
    public static Maybe<T> Some<T>(T value)
        where T : notnull => Maybe<T>.Some(value);

    /// <summary>Gets the first item that passes a test, in place of FirstOrDefault and Find.</summary>
    public static Maybe<T> FirstOrNone<T>(this IEnumerable<T> source, Func<T, bool> test)
        where T : notnull
    {
        foreach (var item in source)
            if (test(item)) return item;
        return Maybe<T>.None;
    }

    /// <summary>Gets the first item, in place of FirstOrDefault.</summary>
    public static Maybe<T> FirstOrNone<T>(this IEnumerable<T> source)
        where T : notnull
    {
        using var items = source.GetEnumerator();
        return items.MoveNext() ? items.Current : Maybe<T>.None;
    }

    /// <summary>Converts a reference an outside API may leave unset (JSON, the runtime) at the boundary; give <typeparamref name="T"/> explicitly.</summary>
    public static Maybe<T> From<T>([AllowNull] T value)
        where T : class => value is { } v ? Maybe<T>.Some(v) : Maybe<T>.None;
}
