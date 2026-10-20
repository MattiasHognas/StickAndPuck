namespace StickAndPuck.App.Arena
{
    /// <summary>W120: whether the home tifo is up, over which section, and how far it has been passed up the
    /// stands. <see cref="TifoMoments"/> raises and lowers it, the arena draws it (<see cref="TifoRenderer"/>)
    /// and the camera looks up at it (Camera/TifoShot). Presentation only: no simulation reads it.</summary>
    public sealed class TifoState
    {
        /// <summary>How long the cloth takes to go all the way up the stand, in seconds.</summary>
        public const float RiseSeconds = 1.6f;

        /// <summary>How long the cloth takes to come back down, in seconds.</summary>
        public const float LowerSeconds = 1.2f;

        private float changedAt;
        private float liftAtChange;

        /// <summary>Gets a value indicating whether the tifo is up or going up.</summary>
        public bool Up { get; private set; }

        /// <summary>Gets the section the tifo is (or was last) raised over.</summary>
        public CrowdSection Section { get; private set; } = CrowdSection.FarCentre;

        /// <summary>Gets the world time it was last raised: a second raise while it is up (another home goal)
        /// moves this on, so it stays up longer.</summary>
        public float RaisedAt { get; private set; }

        /// <summary>Gets how many times it has been raised, counting a raise while it is up.</summary>
        public int Raises { get; private set; }

        /// <summary>Raises the tifo over a section. Raising it over another section starts that cloth from the seats.</summary>
        /// <param name="section">The section that holds it.</param>
        /// <param name="time">The world time now.</param>
        public void Raise(CrowdSection section, float time)
        {
            RaisedAt = time;
            Raises++;
            if (Up && section == Section)
            {
                return;
            }

            liftAtChange = section == Section ? Lift(time) : 0f;
            changedAt = time;
            Section = section;
            Up = true;
        }

        /// <summary>Lowers the tifo; it comes back down over <see cref="LowerSeconds"/>.</summary>
        /// <param name="time">The world time now.</param>
        public void Lower(float time)
        {
            if (!Up)
            {
                return;
            }

            liftAtChange = Lift(time);
            changedAt = time;
            Up = false;
        }

        /// <summary>Gets how far up the stand the cloth reaches: 0 down in the seats, 1 all the way up.</summary>
        /// <param name="time">The world time now.</param>
        /// <returns>0 to 1.</returns>
        public float Lift(float time)
        {
            float t = Math.Max(0f, time - changedAt);
            return Up
                ? Math.Min(1f, liftAtChange + (t / RiseSeconds))
                : Math.Max(0f, liftAtChange - (t / LowerSeconds));
        }
    }
}
