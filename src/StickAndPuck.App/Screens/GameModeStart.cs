using StickAndPuck.App.Core;

namespace StickAndPuck.App.Screens
{
    /// <summary>What a mode is started with.</summary>
    /// <param name="Clubs">Every club from data/clubs/.</param>
    /// <param name="Picks">The clubs the player picked, theirs first.</param>
    /// <param name="Device">The player's input device (<see cref="InputDevice.None"/> lets the AI play).</param>
    /// <param name="App">The game's state: the sessions a mode plays in, the player's settings.</param>
    public sealed record GameModeStart(
        IReadOnlyList<ClubData> Clubs,
        IReadOnlyList<ClubData> Picks,
        InputDevice Device,
        AppState App
    );
}
