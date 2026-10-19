namespace StickAndPuck.App.Core
{
    /// <summary>The coach's face on the dressing room portrait (A20 draws one cell per mood in
    /// <see cref="Assets.CoachMoods"/>, in this order).</summary>
    public enum CoachMood
    {
        /// <summary>A flat, firm look: the default.</summary>
        Neutral,

        /// <summary>Raised brows and a grin.</summary>
        Pleased,

        /// <summary>Brows down, red in the face, shouting.</summary>
        Angry,
    }
}
