using StickAndPuck.App.Core;

namespace StickAndPuck.App.Fights
{
    /// <summary>One fighter's side of a fight.</summary>
    /// <remarks>Initializes a new instance of the <see cref="Fighter"/> class.</remarks>
    public sealed class Fighter(int playerId, int slot)
    {
        /// <summary>Gets the skater.</summary>
        public int PlayerId { get; } = playerId;

        /// <summary>Gets the human slot playing the fight for him, or -1 for the AI.</summary>
        public int Slot { get; } = slot;

        /// <summary>Gets or sets the balance left: 1 at the start, down at 0.</summary>
        public float Balance { get; set; } = 1f;

        /// <summary>Gets or sets a value indicating whether he has thrown this exchange's punch.</summary>
        public bool Punched { get; set; }

        /// <summary>Gets or sets where the marker was when he threw it, 0 to 1.</summary>
        public float PunchAt { get; set; } = float.NaN;

        /// <summary>Gets or sets when his punch lands (<see cref="World.Time"/>), NaN when none is on its way.</summary>
        public float LandsAt { get; set; } = float.NaN;

        /// <summary>Gets or sets a value indicating whether his guard is up.</summary>
        public bool Blocking { get; set; }

        /// <summary>Gets or sets when his guard went up.</summary>
        public float BlockSince { get; set; } = float.NaN;

        /// <summary>Gets or sets where on the bar the AI throws this exchange's punch.</summary>
        public float AiPunchAt { get; set; } = float.NaN;

        /// <summary>Gets or sets when the AI raises its guard against a wind-up it saw, or NaN.</summary>
        public float AiBlockAt { get; set; } = float.NaN;

        /// <summary>Gets or sets punches that landed.</summary>
        public int Landed { get; set; }

        /// <summary>Gets or sets punches the other fighter blocked.</summary>
        public int Blocked { get; set; }

        /// <summary>Gets or sets punches thrown outside the sweet spot.</summary>
        public int Missed { get; set; }

        /// <summary>Gets or sets the clip the renderer shows.</summary>
        public string Clip { get; set; } = "grab";

        /// <summary>Gets or sets when that clip started.</summary>
        public float ClipSince { get; set; }
    }
}
