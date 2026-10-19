using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Board;
using StickAndPuck.App.Season.Schedule;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens
{
    /// <summary>FIRED (W133): the board has let the human go for missing its season goal badly
    /// (<see cref="BoardExpectation.CheckHotSeat"/>). Over the dressing room, a red panel with the club's crest, the
    /// board's word, the target, where the club stood, and that the season is over; then a new season, the
    /// table as it was left, or the main menu. Shown in place of the table or bracket for a fired season
    /// (<see cref="ScreenFlow.Overview"/>).</summary>
    internal sealed class FiredScreen(AppState app) : MenuScreen(app)
    {
        private const int PanelW = 340,
            Line = 11,
            Crest = 32;

        /// <inheritdoc/>
        public override IReadOnlyList<MenuItem> Items()
        {
            return
            [
                new MenuItem(
                    Text.Get(K.NewSeason),
                    () => StartScreen.PickNewSeason(App, ask: false)
                ),
                new MenuItem(Text.Get(K.Standings), () => Flow.Push(new StandingsScreen(App))),
                new MenuItem(Text.Get(K.MainMenu), Back),
            ];
        }

        /// <inheritdoc/>
        protected override void Back()
        {
            Flow.ToMenus();
        }

        /// <inheritdoc/>
        public override void Draw(ScreenArt art, World world)
        {
            if (!App.Season.Current.TryGet(out SeasonState season))
            {
                return;
            }

            Maybe<ClubData> club = Flow.Clubs.FirstOrNone(c =>
                string.Equals(c.Id, season.UserClub, StringComparison.Ordinal)
            );
            _ = art.DrawDressingRoom(club.Select(c => c.Home).Or(new KitColours()));
            IReadOnlyList<MenuItem> items = Items();
            int h =
                5
                + ScreenArt.Row
                + 2
                + Crest
                + 4
                + (2 * Line)
                + 8
                + (items.Count * ScreenArt.Row)
                + 6;
            (int x, int top) = art.Panel(
                Text.Get(K.Fired),
                PanelW,
                h,
                (Game.Height - h - 14) / 2,
                "panel_red"
            );
            string name = club.Select(c => c.Name).Or(season.UserClub).ToUpperInvariant();
            art.Hud.DrawCrest(season.UserClub, x + 14, top + 2, 2);
            int tx = x + 14 + Crest + 6;
            int tw = PanelW - (tx - x) - 10;
            art.Left(art.Clip(Text.Format(K.FiredBoard, name), tw), tx, top + 2, art.Gold);
            art.Left(
                art.Clip(Text.Format(K.FiredTarget, BoardExpectation.Describe(season)), tw),
                tx,
                top + 2 + Line,
                art.Text
            );
            art.Left(
                art.Clip(
                    Text.Format(
                        K.FiredPlace,
                        BoardExpectation.Place(season),
                        season.Clubs.Count,
                        season.PlayedBy(season.UserClub).Count()
                    ),
                    tw
                ),
                tx,
                top + 2 + (2 * Line),
                art.Text
            );
            top += Crest + 4;
            art.Centred(Text.Get(K.FiredOver), Game.Width / 2, top + 6, art.Gold);
            top += (2 * Line) + 8;
            art.Menu(items, Selected, x + 70, PanelW - 140, top);
        }
    }
}
