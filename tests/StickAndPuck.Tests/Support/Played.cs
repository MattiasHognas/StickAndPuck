using System.Collections.Concurrent;

namespace StickAndPuck.Tests.Support;

/// <summary>
/// What a split test's matches came to. Each match is its own test case, so the runner spreads them over
/// every core; each adds its result here, and the verdict test, which depends on them all, reads them
/// back in order. One per test class (inject it with <c>[ClassDataSource&lt;Played&lt;T&gt;&gt;(Shared = SharedType.PerClass)]</c>).
/// </summary>
/// <typeparam name="T">What one match comes to.</typeparam>
public sealed class Played<T>
    where T : notnull
{
    readonly ConcurrentDictionary<int, T> results = new();

    /// <summary>Keeps match <paramref name="match"/>'s result.</summary>
    public void Add(int match, T result) => results[match] = result;

    /// <summary>Gets matches 0 to <paramref name="count"/> - 1, in order.</summary>
    public T[] All(int count) => [.. Enumerable.Range(0, count).Select(i => results.TryGetValue(i, out var r) ? r : throw new InvalidOperationException($"match {i} has not been played"))];
}
