using StickAndPuck.App.Core;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens;

/// <summary>A game mode on the start screen's "new game" list. Any class implementing this with a
/// parameterless constructor is found automatically, so playoffs, arcade and versus add their
/// entry from their own folder. The screens pick the clubs, build the world from the returned
/// setup with the player's settings and period length, and show the results after the final.</summary>
public interface IGameMode
{
    /// <summary>Gets the name on the list, in capitals.</summary>
    string Title { get; }

    /// <summary>Gets where the mode sits on the list; lower comes first. Exhibition is 0.</summary>
    int Order => 100;

    /// <summary>Gets how many clubs the player picks before the match: 0, 1 (their club) or 2 (their club, then the opponent).</summary>
    int ClubPicks => 2;

    /// <summary>Gets a value indicating whether the mode shows on the list now (a "continue" entry hides without a save).</summary>
    bool Available(AppState app) => true;

    /// <summary>Gets a value indicating whether the results screen offers the mode's next game (a run of games, like arcade).</summary>
    bool HasNextGame(AppState app) => false;

    /// <summary>Gets a line for the results screen about where the mode stands (lives left, the next opponent), or None.</summary>
    Maybe<string> Status(AppState app) => Maybe<string>.None;

    /// <summary>Gets the team select heading for the given pick (it names the player picking).</summary>
    string PickHeading(AppState app, int index) => Text.Get(index == 0 ? K.PickYourClub : K.PickOpponent);

    /// <summary>Builds the next match. Called when the mode starts, and again for each next game.</summary>
    /// <returns>The setup, or None when the mode has nothing more to play (the screens go back to the start screen).</returns>
    Maybe<MatchSetup> Setup(GameModeStart start);
}

/// <summary>What a mode is started with.</summary>
/// <param name="Clubs">Every club from data/clubs/.</param>
/// <param name="Picks">The clubs the player picked, theirs first.</param>
/// <param name="Device">The player's input device (<see cref="InputDevice.None"/> lets the AI play).</param>
/// <param name="App">The game's state: the sessions a mode plays in, the player's settings.</param>
public sealed record GameModeStart(IReadOnlyList<ClubData> Clubs, IReadOnlyList<ClubData> Picks, InputDevice Device, AppState App);

/// <summary>A single match between two picked clubs, the player on the home club, SHL overtime.</summary>
public sealed class ExhibitionMode : IGameMode
{
    /// <inheritdoc/>
    public string Title => Text.Get(K.Exhibition);

    /// <inheritdoc/>
    public int Order => 0;

    /// <inheritdoc/>
    public Maybe<MatchSetup> Setup(GameModeStart start)
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
    public static IReadOnlyList<IGameMode> All(AppState app)
    {
        return typeof(GameModes).Assembly.GetTypes()
            .Where(t => typeof(IGameMode).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false }
                && t.GetConstructor(Type.EmptyTypes) is { })
            .Select(t => (IGameMode)Activator.CreateInstance(t)!)
            .Where(m => m.Available(app))
            .OrderBy(m => m.Order)
            .ThenBy(m => m.Title, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Gets a fresh mode by its type name, as a replay names it.</summary>
    public static Maybe<IGameMode> Named(string typeName)
    {
        var type = typeof(GameModes).Assembly.GetTypes().FirstOrDefault(t => string.Equals(t.Name, typeName, StringComparison.Ordinal)
            && typeof(IGameMode).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false } && t.GetConstructor(Type.EmptyTypes) is { });
        return type is { } found ? Maybe.Some((IGameMode)Activator.CreateInstance(found)!) : Maybe<IGameMode>.None;
    }
}
