using System.Numerics;

namespace StickAndPuck.App.AI.Decisions
{
    /// <summary>A human's pass in flight to a teammate the human now controls. The AI skates him to meet
    /// it until the player gives fresh stick input: the stick held as the pass left does not count.</summary>
    internal sealed class HumanMeet(int receiver, Vector2 held)
    {
        public int Receiver { get; } = receiver;

        /// <summary>Gets the stick held as the pass left.</summary>
        public Vector2 Held { get; } = held;

        /// <summary>Gets or sets a value indicating whether the stick has been at rest since the pass.</summary>
        public bool Released { get; set; }

        /// <summary>Gets or sets a value indicating whether the player has steered, handing the receiver back.</summary>
        public bool HandedBack { get; set; }
    }
}
