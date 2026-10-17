using StickAndPuck.App.Core;

namespace StickAndPuck.App.Screens;

/// <summary>What the match being played is, so the results screen knows where to go next.</summary>
/// <param name="Mode">The game mode, or None for a season game.</param>
/// <param name="Picks">The clubs the player picked for the mode.</param>
internal sealed record MatchPlan(Maybe<IGameMode> Mode, IReadOnlyList<ClubData> Picks)
{
    /// <summary>Gets the plan of a season game.</summary>
    public static MatchPlan SeasonGame() => new(Maybe<IGameMode>.None, []);

    /// <summary>Gets a value indicating whether the match is a season game.</summary>
    public bool Season => !Mode.HasValue;
}
