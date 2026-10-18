using StickAndPuck.App.Core;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens
{
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
        bool Available(AppState app)
        {
            return true;
        }

        /// <summary>Gets a value indicating whether the results screen offers the mode's next game (a run of games, like arcade).</summary>
        bool HasNextGame(AppState app)
        {
            return false;
        }

        /// <summary>Gets a line for the results screen about where the mode stands (lives left, the next opponent), or None.</summary>
        Maybe<string> Status(AppState app)
        {
            return Maybe.None<string>();
        }

        /// <summary>Gets the team select heading for the given pick (it names the player picking).</summary>
        string PickHeading(AppState app, int index)
        {
            return Text.Get(index == 0 ? K.PickYourClub : K.PickOpponent);
        }

        /// <summary>Builds the next match. Called when the mode starts, and again for each next game.</summary>
        /// <returns>The setup, or None when the mode has nothing more to play (the screens go back to the start screen).</returns>
        Maybe<MatchSetup> Setup(GameModeStart start);
    }
}
