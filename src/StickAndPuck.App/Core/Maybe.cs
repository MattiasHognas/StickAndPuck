using System.Diagnostics.CodeAnalysis;

namespace StickAndPuck.App.Core
{
    /// <summary>A value that may be absent, in place of a nullable type (the game bans T? and null, SAP0001/SAP0002).
    /// The default is <see cref="Maybe.None{T}()"/>; a value converts to Some by itself.</summary>
    public readonly struct Maybe<T> : IEquatable<Maybe<T>>
        where T : notnull
    {
        private readonly T value;

        private Maybe(T value)
        {
            ArgumentNullException.ThrowIfNull(value); // a test comparing a Maybe with null fails loudly instead of quietly
            this.value = value;
            HasValue = true;
        }

        /// <summary>Gets a value indicating whether a value is present.</summary>
        public bool HasValue { get; }

        /// <summary>Wraps a present value.</summary>
        public static implicit operator Maybe<T>(T value)
        {
            return new(value);
        }

        /// <summary>Compares two maybes: both absent, or both present with equal values.</summary>
        public static bool operator ==(Maybe<T> left, Maybe<T> right)
        {
            return left.Equals(right);
        }

        /// <summary>Compares two maybes.</summary>
        public static bool operator !=(Maybe<T> left, Maybe<T> right)
        {
            return !left.Equals(right);
        }

        /// <summary>Gets the value when present.</summary>
        public bool TryGet(out T result)
        {
            result = value;
            return HasValue;
        }

        /// <summary>Gets the value, or a fallback when absent.</summary>
        public T Or(T fallback)
        {
            return HasValue ? value : fallback;
        }

        /// <summary>Maps a present value.</summary>
        public Maybe<TOut> Select<TOut>(Func<T, TOut> map)
            where TOut : notnull
        {
            return HasValue ? map(value) : Maybe.None<TOut>();
        }

        /// <summary>Tells whether a value is present and passes a test.</summary>
        public bool Is(Func<T, bool> test)
        {
            return HasValue && test(value);
        }

        /// <inheritdoc/>
        public bool Equals(Maybe<T> other)
        {
            return HasValue == other.HasValue
                && (!HasValue || EqualityComparer<T>.Default.Equals(value, other.value));
        }

        /// <inheritdoc/>
        public override bool Equals([AllowNull] object obj)
        {
            return obj is Maybe<T> other && Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HasValue ? EqualityComparer<T>.Default.GetHashCode(value) : 0;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return HasValue ? $"Some({value})" : "None";
        }
    }

    /// <summary>Builds <see cref="Maybe{T}"/> values.</summary>
    public static class Maybe
    {
        /// <summary>Gets the absent value.</summary>
        public static Maybe<T> None<T>()
            where T : notnull
        {
            return default;
        }

        /// <summary>Wraps a present value.</summary>
        public static Maybe<T> Some<T>(T value)
            where T : notnull
        {
            return value;
        }

        /// <summary>Gets the first item that passes a test, in place of FirstOrDefault and Find.</summary>
        public static Maybe<T> FirstOrNone<T>(this IEnumerable<T> source, Func<T, bool> test)
            where T : notnull
        {
            return source.Where(test).FirstOrNone();
        }

        /// <summary>Gets the first item, in place of FirstOrDefault.</summary>
        public static Maybe<T> FirstOrNone<T>(this IEnumerable<T> source)
            where T : notnull
        {
            using IEnumerator<T> items = source.GetEnumerator();
            return items.MoveNext() ? items.Current : None<T>();
        }

        /// <summary>Converts a reference an outside API may leave unset (JSON, the runtime) at the boundary; give <typeparamref name="T"/> explicitly.</summary>
        public static Maybe<T> From<T>([AllowNull] T value)
            where T : class
        {
            return value is { } v ? v : None<T>();
        }
    }
}
