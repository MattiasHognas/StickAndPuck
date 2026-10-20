using StickAndPuck.App.Season.Schedule;

namespace StickAndPuck.App.Season.Recap
{
    /// <summary>One of the human's games as the recap tells it, from the human's side.</summary>
    /// <param name="Opponent">The other club's id.</param>
    /// <param name="For">The human club's goals.</param>
    /// <param name="Against">The other club's goals.</param>
    /// <param name="Home">Whether the human's club was at home.</param>
    /// <param name="Playoff">Whether it was a playoff or playout game.</param>
    /// <param name="Kind">How it was decided.</param>
    public sealed record RecapGame(
        string Opponent,
        int For,
        int Against,
        bool Home,
        bool Playoff,
        ResultKind Kind
    )
    {
        /// <summary>Gets a value indicating whether the human's club won.</summary>
        public bool Won => For > Against;

        /// <summary>Gets the goal margin, below 0 for a loss.</summary>
        public int Margin => For - Against;
    }
}
