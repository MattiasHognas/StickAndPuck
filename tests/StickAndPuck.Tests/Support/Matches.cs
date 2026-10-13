namespace StickAndPuck.Tests.Support;

/// <summary>
/// Plays independent full matches side by side. Each match has its own world, systems and seeds, so
/// the results are the same as playing them one after another; they come back in order, so totals
/// and printed lines are too. Only for matches that share no static the test changes while they run.
/// </summary>
public static class Matches
{
    /// <summary>Plays <paramref name="count"/> matches at once.</summary>
    /// <typeparam name="T">What one match comes to.</typeparam>
    /// <param name="count">How many matches.</param>
    /// <param name="match">Plays match i, 0 to count - 1.</param>
    /// <returns>Each match's result, in order.</returns>
    public static T[] Play<T>(int count, Func<int, T> match)
    {
        var results = new T[count];
        Parallel.For(0, count, i => results[i] = match(i));
        return results;
    }
}
