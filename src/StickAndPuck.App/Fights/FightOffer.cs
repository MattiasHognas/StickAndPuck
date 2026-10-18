namespace StickAndPuck.App.Fights
{
    /// <summary>A fight offered after a hard check, waiting for both sides to accept.</summary>
    public sealed record FightOffer(int CheckerId, int VictimId, float Time)
    {
        /// <summary>Gets the human slot deciding for each fighter (checker, victim), or -1 for the AI.</summary>
        public int[] Slots { get; } = [-1, -1];

        /// <summary>Gets whether each fighter has said yes (an AI decides at once).</summary>
        public bool[] Accepted { get; } = new bool[2];
    }
}
