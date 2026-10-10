using StickAndPuck.Core;

namespace StickAndPuck.Screens;

/// <summary>
/// A game mode on the start screen's "new game" list. Any class implementing this with a
/// parameterless constructor is found automatically, so playoffs, arcade and versus add their
/// entry from their own folder. The screens pick the clubs, build the world from the returned
/// setup with the player's settings and period length, and show the results after the final.
/// </summary>
public interface IGameMode
{
    /// <summary>Gets the name on the list, in capitals.</summary>
    string Title { get; }

    /// <summary>Gets where the mode sits on the list; lower comes first. Exhibition is 0.</summary>
    int Order => 100;

    /// <summary>Gets how many clubs the player picks before the match: 0, 1 (their club) or 2 (their club, then the opponent).</summary>
    int ClubPicks => 2;

    /// <summary>Gets a value indicating whether the mode shows on the list now (a "continue" entry hides without a save).</summary>
    bool Available => true;

    /// <summary>Gets a value indicating whether the results screen offers the mode's next game (a run of games, like arcade).</summary>
    bool HasNextGame => false;

    /// <summary>Gets a line for the results screen about where the mode stands (lives left, the next opponent), or null.</summary>
    string? Status => null;

    /// <summary>Gets the team select heading for the given pick (W17 names the player picking).</summary>
    /// <param name="index">0 for the first pick, 1 for the second.</param>
    /// <returns>The heading, in capitals.</returns>
    string PickHeading(int index) => index == 0 ? "PICK YOUR CLUB" : "PICK THE OPPONENT";

    /// <summary>Builds the next match. Called when the mode starts, and again for each next game.</summary>
    /// <param name="start">The clubs, the player's picks and input device.</param>
    /// <returns>The setup, or null when the mode has nothing more to play (the screens go back to the start screen).</returns>
    MatchSetup? Setup(GameModeStart start);
}

/// <summary>What a mode is started with.</summary>
/// <param name="Clubs">Every club from data/clubs/.</param>
/// <param name="Picks">The clubs the player picked, theirs first.</param>
/// <param name="Device">The player's input device (<see cref="InputDevice.None"/> lets the AI play).</param>
public sealed record GameModeStart(IReadOnlyList<ClubData> Clubs, IReadOnlyList<ClubData> Picks, InputDevice Device);

/// <summary>A single match between two picked clubs, the player on the home club, SHL overtime.</summary>
public sealed class ExhibitionMode : IGameMode
{
    /// <inheritdoc/>
    public string Title => "EXHIBITION";

    /// <inheritdoc/>
    public int Order => 0;

    /// <inheritdoc/>
    public MatchSetup? Setup(GameModeStart start)
    {
        var home = start.Picks.Count > 0 ? start.Picks[0] : start.Clubs[0];
        var away = start.Picks.Count > 1 ? start.Picks[1] : start.Clubs.FirstOrDefault(c => c != home) ?? home;
        return new MatchSetup
        {
            Home = home,
            Away = away,
            Inputs = [new InputSlot(start.Device, Side.Home), new InputSlot(InputDevice.None, Side.Away)],
            Overtime = OvertimeRule.Shl,
        };
    }
}

/// <summary>Finds the game modes.</summary>
internal static class GameModes
{
    /// <summary>Gets every mode available now, in list order.</summary>
    public static IReadOnlyList<IGameMode> All()
    {
        return typeof(GameModes).Assembly.GetTypes()
            .Where(t => typeof(IGameMode).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false }
                && t.GetConstructor(Type.EmptyTypes) != null)
            .Select(t => (IGameMode)Activator.CreateInstance(t)!)
            .Where(m => m.Available)
            .OrderBy(m => m.Order)
            .ThenBy(m => m.Title, StringComparer.Ordinal)
            .ToList();
    }
}
