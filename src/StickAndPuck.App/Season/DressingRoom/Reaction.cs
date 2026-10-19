using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.DressingRoom
{
    /// <summary>The coach's reaction to a game: what happened, who did it, a number for the line ({1}: the
    /// lead, the goals against, the saves or the penalties) and his face.</summary>
    /// <param name="Kind">What he reacts to.</param>
    /// <param name="Player">The player's last name, {0} in the line.</param>
    /// <param name="Count">The number in the line, {1}.</param>
    /// <param name="Mood">The coach's face.</param>
    public sealed record Reaction(ReactionKind Kind, string Player, int Count, CoachMood Mood);
}
