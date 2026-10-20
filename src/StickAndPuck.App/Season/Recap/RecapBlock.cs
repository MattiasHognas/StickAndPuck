using System.Runtime.InteropServices;

namespace StickAndPuck.App.Season.Recap
{
    /// <summary>One "month" of the human's regular season in the recap: the season has no calendar, so its games are
    /// split into <see cref="SeasonRecap.Months"/> equal blocks named after the league's months (SHL September to
    /// March, NHL October to April).</summary>
    /// <param name="Month">The month: 0 for September up to 7 for April.</param>
    /// <param name="Scheduled">The human's games in the block.</param>
    /// <param name="Played">Those played (fewer for a fired coach).</param>
    /// <param name="Wins">Wins, after overtime and shootouts too.</param>
    /// <param name="Losses">Losses in regulation.</param>
    /// <param name="OvertimeLosses">Losses in overtime or a shootout.</param>
    /// <param name="Points">Table points under the season's rules.</param>
    [StructLayout(LayoutKind.Auto)]
    public readonly record struct RecapBlock(
        int Month,
        int Scheduled,
        int Played,
        int Wins,
        int Losses,
        int OvertimeLosses,
        int Points
    );
}
