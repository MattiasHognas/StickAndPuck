namespace StickAndPuck.App.Core;

/// <summary>
/// One system's state on the <see cref="World"/>, made the first time the system asks for it,
/// as the side tables it replaces (W56) made theirs.
/// </summary>
/// <typeparam name="T">The state.</typeparam>
public sealed class Slot<T>
    where T : class
{
    Maybe<T> value;

    /// <summary>Gets a value indicating whether the state has been made.</summary>
    public bool HasValue => value.HasValue;

    /// <summary>Gets the state if it has been made.</summary>
    /// <param name="state">The state.</param>
    /// <returns>Whether it has been made.</returns>
    public bool TryGet(out T state) => value.TryGet(out state);

    /// <summary>Gets the state, making it first if this is the first ask.</summary>
    /// <param name="make">Makes the state.</param>
    /// <returns>The state.</returns>
    public T Get(Func<T> make)
    {
        if (!value.TryGet(out var state))
        {
            state = make();
            value = state;
        }

        return state;
    }
}
