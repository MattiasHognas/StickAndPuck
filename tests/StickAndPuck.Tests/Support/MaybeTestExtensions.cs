using StickAndPuck.App.Core;

namespace StickAndPuck.Tests;

/// <summary>Reads Maybe and NextMatch results back as plain references, so checks read as they did with nullables.</summary>
internal static class MaybeTestExtensions
{
    /// <summary>Gets the value, or null when there is none.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The maybe.</param>
    /// <returns>The value or null.</returns>
    public static T? OrNull<T>(this Maybe<T> value)
        where T : class => value.TryGet(out var v) ? v : null;

    /// <summary>Gets the match to play, or null when the run is over.</summary>
    /// <param name="next">The next match.</param>
    /// <returns>The setup or null.</returns>
    public static MatchSetup? OrNull(this NextMatch next) => next is NextMatch.Play play ? play.Setup : null;
}
