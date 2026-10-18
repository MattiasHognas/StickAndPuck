namespace StickAndPuck.App.Core
{
    /// <summary>The events of the current step. Rules, physics and players publish; HUD, camera, referee,
    /// season and audio read. The list is cleared at the start of every step, so read it in
    /// Update of a system that runs after the publisher.</summary>
    public sealed class MatchEvents
    {
        private readonly List<MatchEvent> current = [];

        /// <summary>Gets the events published so far this step.</summary>
        public IReadOnlyList<MatchEvent> Current => current;

        /// <summary>Publishes an event to every system that runs after this one.</summary>
        public void Publish(MatchEvent e)
        {
            current.Add(e);
        }

        /// <summary>Gets this step's events of one type.</summary>
        public IEnumerable<T> Of<T>()
            where T : MatchEvent
        {
            return current.OfType<T>();
        }

        /// <summary>Clears the list. Only Game.cs calls this, at the start of each step.</summary>
        public void Clear()
        {
            current.Clear();
        }
    }
}
