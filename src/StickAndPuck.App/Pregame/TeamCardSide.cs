using StickAndPuck.App.Core;
using StickAndPuck.App.Screens;

namespace StickAndPuck.App.Pregame
{
    /// <summary>One club's half of the pre-game stats card (<see cref="TeamCard"/>).</summary>
    /// <param name="ClubId">The club's id, for its crest.</param>
    /// <param name="Name">The club's name.</param>
    /// <param name="Strengths">Its ratings and how it plays.</param>
    /// <param name="Record">Wins, losses, overtime losses and points this season, or None outside a season.</param>
    /// <param name="Place">Its place in the table, or None outside a season.</param>
    /// <param name="Form">Its last results as form dots, oldest first; empty outside a season or before its first game.</param>
    /// <param name="Scorer">Its top goal scorer this season, or None outside a season or before anyone has scored.</param>
    internal sealed record TeamCardSide(
        string ClubId,
        string Name,
        ClubStrengths Strengths,
        Maybe<string> Record,
        Maybe<string> Place,
        IReadOnlyList<string> Form,
        Maybe<string> Scorer
    );
}
