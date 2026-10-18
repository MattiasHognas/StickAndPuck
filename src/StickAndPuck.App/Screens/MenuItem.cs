using StickAndPuck.App.Core;

namespace StickAndPuck.App.Screens
{
    /// <summary>One menu row.</summary>
    internal sealed record MenuItem
    {
        private static readonly Action NoPick = () => { };
        private static readonly Action<int> NoChange = _ => { };

        /// <summary>Initializes a new instance of the <see cref="MenuItem"/> class: a row that does nothing (a heading).</summary>
        public MenuItem(string label, bool enabled = true)
            : this(label, NoPick, Maybe.None<string>(), NoChange, enabled) { }

        /// <summary>Initializes a new instance of the <see cref="MenuItem"/> class: a row enter picks.</summary>
        public MenuItem(string label, Action pick, bool enabled = true)
            : this(label, pick, Maybe.None<string>(), NoChange, enabled) { }

        /// <summary>Initializes a new instance of the <see cref="MenuItem"/> class: a row enter picks, showing a value.</summary>
        public MenuItem(string label, Action pick, string value, bool enabled = true)
            : this(label, pick, Maybe.Some(value), NoChange, enabled) { }

        /// <summary>Initializes a new instance of the <see cref="MenuItem"/> class: a setting left and right change; enter steps it forward.</summary>
        public MenuItem(string label, string value, Action<int> change, bool enabled = true)
            : this(label, () => change(1), Maybe.Some(value), change, enabled) { }

        private MenuItem(
            string label,
            Action pick,
            Maybe<string> value,
            Action<int> change,
            bool enabled
        )
        {
            Label = label;
            Pick = pick;
            Value = value;
            Change = change;
            Enabled = enabled;
        }

        public string Label { get; init; }

        /// <summary>Gets what enter does: nothing on a heading, a step forward on a setting.</summary>
        public Action Pick { get; init; }

        /// <summary>Gets the current value of a setting, shown on the right, or None.</summary>
        public Maybe<string> Value { get; init; }

        /// <summary>Gets what left (-1) and right (+1) do to the value; nothing on other rows.</summary>
        public Action<int> Change { get; init; }

        /// <summary>Gets a value indicating whether the row can be picked.</summary>
        public bool Enabled { get; init; }
    }
}
