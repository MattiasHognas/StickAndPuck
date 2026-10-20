using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Recap;
using StickAndPuck.App.Season.Schedule;
using static Raylib_cs.Raylib;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens
{
    /// <summary>The season recap (W136), the last screen of a season: after the trophy, the relegation or the sack
    /// (<see cref="SeasonEnd.ToRecap"/>). Over the dressing room, the club and its regular season line, the season
    /// month by month, the best game, the top scorer, the injuries and the board's verdict, then the season in
    /// newspaper headlines (<see cref="SeasonRecap"/>), and NEW SEASON or MAIN MENU: one season is the whole game.</summary>
    internal sealed class SeasonRecapScreen : MenuScreen
    {
        private const int PanelW = 460,
            Line = 11,
            Crest = 32,
            BarX = 36,
            BarW = 50,
            RecordRight = 178;

        private readonly Maybe<SeasonRecap> recap;

        /// <summary>Initializes a new instance of the <see cref="SeasonRecapScreen"/> class: the recap of the season being played,
        /// its paper games credited first.</summary>
        public SeasonRecapScreen(AppState app)
            : base(app)
        {
            if (app.Season.Current.TryGet(out SeasonState season))
            {
                _ = season.Stats.CatchUp(season, app.Season.Clubs);
                recap = SeasonRecap.Build(season, app.Season.Clubs);
            }
        }

        /// <inheritdoc/>
        public override IReadOnlyList<MenuItem> Items()
        {
            return
            [
                new MenuItem(
                    Text.Get(K.NewSeason),
                    () => StartScreen.PickNewSeason(App, ask: false)
                ),
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
            if (!recap.TryGet(out SeasonRecap r))
            {
                return;
            }

            IReadOnlyList<ClubData> clubs = App.Season.Clubs;
            Maybe<ClubData> club = clubs.FirstOrNone(c =>
                string.Equals(c.Id, r.UserClub, StringComparison.Ordinal)
            );
            _ = art.DrawDressingRoom(club.Select(c => c.Home).Or(new KitColours()), r.UserClub);
            IReadOnlyList<MenuItem> items = Items();
            int headlines = r.Headlines.Count;
            int h =
                5
                + ScreenArt.Row
                + 2
                + Crest
                + 6
                + Line
                + (SeasonRecap.Months * Line)
                + 6
                + Line
                + (headlines * Line)
                + 8
                + (items.Count * ScreenArt.Row)
                + 4;
            (int x, int top) = art.Panel(
                Text.Get(K.SeasonRecap),
                PanelW,
                h,
                Math.Max(2, (Game.Height - h - 14) / 2)
            );

            art.Hud.DrawCrest(r.UserClub, x + 12, top, 2);
            int tx = x + 12 + Crest + 8;
            string name = club.Select(c => c.Name).Or(r.UserClub);
            art.Left(art.Clip(name, PanelW - (tx - x) - 10), tx, top + 4, art.Gold);
            art.Left(RecapText.SeasonLine(r), tx, top + 4 + Line + 2, art.Text);
            top += Crest + 6;

            DrawMonths(art, r, x + 12, top);
            DrawFacts(art, r, clubs, x + 200, top, PanelW - 210);
            top += Line + (SeasonRecap.Months * Line) + 6;

            DrawRectangle(x + 12, top - 3, PanelW - 24, 1, ScreenArt.Muted);
            art.Centred(Text.Get(K.RecapHeadlines), Game.Width / 2, top, ScreenArt.Muted);
            top += Line;
            for (int i = 0; i < headlines; i++)
            {
                art.Centred(
                    art.Clip(RecapText.Headline(r.Headlines[i], r.League), PanelW - 20),
                    Game.Width / 2,
                    top,
                    i == 0 ? art.Gold : art.Text
                );
                top += Line;
            }

            art.Menu(items, Selected, x + 150, PanelW - 300, top + 8);
        }

        /// <summary>The regular season month by month: the month, its record, its points and a bar of the points taken out of those there were.</summary>
        private static void DrawMonths(ScreenArt art, SeasonRecap r, int x, int top)
        {
            art.Left(Text.Get(K.RecapByMonth), x, top, ScreenArt.Muted);
            art.Right(Text.Get(K.RecapRecordColumns), x + RecordRight, top, ScreenArt.Muted);
            int perGame = r.League == League.Nhl ? 2 : 3;
            int y = top + Line;
            foreach (RecapBlock b in r.Blocks)
            {
                art.Left(RecapText.Month(b.Month), x, y, b.Played > 0 ? art.Text : ScreenArt.Muted);
                art.Right(RecapText.Record(b), x + RecordRight, y, art.Text);
                if (b.Played > 0)
                {
                    int fill = Math.Clamp(
                        b.Points * BarW / Math.Max(1, b.Played * perGame),
                        0,
                        BarW
                    );
                    DrawRectangle(x + BarX, y + 3, BarW, 5, ScreenArt.Muted);
                    DrawRectangle(x + BarX, y + 3, fill, 5, art.Gold);
                    art.Right(
                        Text.Format(K.RecapPoints, b.Points),
                        x + BarX + BarW + 32,
                        y,
                        ScreenArt.Muted
                    );
                }

                y += Line;
            }
        }

        /// <summary>The best game, the top scorer, the injuries and the board, each a label over its value.</summary>
        private static void DrawFacts(
            ScreenArt art,
            SeasonRecap r,
            IReadOnlyList<ClubData> clubs,
            int x,
            int top,
            int w
        )
        {
            (string Label, string Value)[] facts =
            [
                (Text.Get(K.RecapBestGame), RecapText.BestGame(r, clubs)),
                (Text.Get(K.TopScorer), RecapText.TopScorer(r)),
                (Text.Get(K.RoomInjuries), RecapText.Injuries(r)),
                (Text.Get(K.RoomBoard), RecapText.Verdict(r)),
            ];
            int y = top;
            foreach ((string label, string value) in facts)
            {
                art.Left(label, x, y, ScreenArt.Muted);
                art.Left(art.Clip(value, w), x, y + Line, art.Text);
                y += (2 * Line) - 1;
            }
        }
    }
}
