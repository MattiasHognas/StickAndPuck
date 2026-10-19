using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Schedule;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Season.DressingRoom
{
    /// <summary>The placeholder coach line, at priority 0: one line for how the last game went (a big win, a
    /// win, a win in overtime or the shootout, a loss after regulation, a loss, a big loss), or a
    /// first-game line before any. The post-game reaction (W127) says something sharper over it.</summary>
    internal sealed class LastResultCoach : IDressingRoomSource
    {
        /// <summary>The goal margin that makes a win or a loss a big one.</summary>
        public const int BigMargin = 3;

        /// <inheritdoc/>
        public void Fill(DressingRoomContext context, DressingRoomReport report)
        {
            report.Say(new DressingRoomEntry(Text.Get(LineFor(context.Last))));
        }

        /// <summary>Gets the string key of the coach line for a last game.</summary>
        public static string LineFor(Maybe<LastGame> last)
        {
            if (!last.TryGet(out LastGame game))
            {
                return K.CoachFirstGame;
            }

            int margin = game.GoalsFor - game.GoalsAgainst;
            bool extra = game.Result.Kind != ResultKind.Regulation;
            return margin switch
            {
                >= BigMargin => K.CoachBigWin,
                > 0 => extra ? K.CoachExtraWin : K.CoachWin,
                <= -BigMargin => K.CoachBigLoss,
                _ => extra ? K.CoachExtraLoss : K.CoachLoss,
            };
        }
    }
}
