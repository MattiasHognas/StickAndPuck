namespace StickAndPuck.App.Core
{
    /// <summary><see cref="World.Players"/>: a list that counts every change to who is in it or on the ice, so
    /// the world can keep its on-ice lists and rebuild them only after a change.</summary>
    public sealed class PlayerList : List<Player>
    {
        /// <summary>Gets the change count: it goes up whenever a player is added, replaced, or goes on or off the ice.</summary>
        public int Version { get; private set; }

        /// <summary>Gets or sets the player at an index; setting replaces that player.</summary>
        public new Player this[int index]
        {
            get => base[index];
            set
            {
                base[index] = value;
                value.Join(this);
                Changed();
            }
        }

        /// <summary>Adds a player.</summary>
        public new void Add(Player player)
        {
            base.Add(player);
            player.Join(this);
            Changed();
        }

        /// <summary>Counts a change.</summary>
        internal void Changed()
        {
            Version++;
        }
    }
}
