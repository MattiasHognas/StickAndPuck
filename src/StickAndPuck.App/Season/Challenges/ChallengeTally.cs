namespace StickAndPuck.App.Season.Challenges
{
    /// <summary>How a coach's challenge is going: its games played so far and what they brought.</summary>
    /// <param name="Played">Its games played, 0 to 3.</param>
    /// <param name="Wins">The human's club's wins in them (overtime and shootout wins count).</param>
    /// <param name="Losses">Its losses in them (an overtime or shootout loss is a loss).</param>
    /// <param name="GoalsFor">The goals it scored in them.</param>
    /// <param name="Shutouts">The games it kept a clean sheet in.</param>
    /// <param name="RivalBeaten">True once the rival's game is played and won.</param>
    /// <param name="RivalLost">True once the rival's game is played and lost.</param>
    /// <param name="LastWon">True when the latest game played was won.</param>
    /// <param name="LastConceded">The goals let in in the latest game played.</param>
    public sealed record ChallengeTally(
        int Played,
        int Wins,
        int Losses,
        int GoalsFor,
        int Shutouts,
        bool RivalBeaten,
        bool RivalLost,
        bool LastWon,
        int LastConceded
    );
}
