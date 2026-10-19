using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Playoffs;
using StickAndPuck.App.Season.Schedule;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Season.DressingRoom
{
    /// <summary>The pre-game talk's opening line, at priority 0: tonight's opponent and the question of how
    /// to go out there; without a known opponent (the playoffs not drawn yet) just the question.</summary>
    internal sealed class TalkOpening : IPreGameTalkSource
    {
        /// <inheritdoc/>
        public void Fill(DressingRoomContext context, PreGameTalkLines lines)
        {
            lines.Say(
                new DressingRoomEntry(
                    Opponent(context.Season).TryGet(out string opponent)
                        ? Text.Format(K.TalkOpening, context.Name(opponent))
                        : Text.Get(K.TalkOpeningPlain)
                )
            );
        }

        /// <summary>Gets the club the human's club plays next: in its playoff series once the playoffs are on,
        /// else its next fixture; None when it has no game left.</summary>
        public static Maybe<string> Opponent(SeasonState season)
        {
            string user = season.UserClub;
            (string home, string away) =
                season.Playoffs.TryGet(out PlayoffState playoffs)
                && playoffs.UserSeries.TryGet(out PlayoffSeries series)
                    ? (series.NextHome, series.NextAway)
                : !season.Playoffs.HasValue && season.NextGame.TryGet(out Fixture g)
                    ? (g.Home, g.Away)
                : ("", "");
            return home.Length == 0 ? Maybe.None<string>()
                : string.Equals(home, user, StringComparison.Ordinal) ? away
                : home;
        }
    }
}
