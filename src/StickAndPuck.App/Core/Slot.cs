namespace StickAndPuck.App.Core
{
    /// <summary>One system's state on the <see cref="World"/>, made the first time the system asks for it,
    /// as the side tables it replaces made theirs.</summary>
    public sealed class Slot<T>
        where T : class
    {
        private Maybe<T> value;

        /// <summary>Gets a value indicating whether the state has been made.</summary>
        public bool HasValue => value.HasValue;

        /// <summary>Gets the state if it has been made.</summary>
        public bool TryGet(out T state)
        {
            return value.TryGet(out state);
        }

        /// <summary>Gets the state, making it first if this is the first ask.</summary>
        public T Get(Func<T> make)
        {
            if (!value.TryGet(out T state))
            {
                state = make();
                value = state;
            }

            return state;
        }

        /// <summary>Sets the state.</summary>
        public void Set(T state)
        {
            value = state;
        }

        /// <summary>Drops the state; the next ask makes a new one.</summary>
        public void Clear()
        {
            value = Maybe.None<T>();
        }
    }
}
