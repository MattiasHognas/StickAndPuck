using System.Collections.Immutable;
using System.Globalization;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Morale;
using StickAndPuck.App.Season.Schedule;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens
{
    /// <summary>Standings: the SHL table of the season being played (<see cref="SeasonState.Table"/>), or the
    /// NHL standings by division and conference with the playoff spots as they stand, the human's
    /// club in gold, each club's team morale while the season plays with it (SHL table), and the next
    /// game. Enter plays it.</summary>
    internal sealed class StandingsScreen(AppState app) : MenuScreen(app)
    {
        private const int PanelW = 380,
            MoraleW = 30,
            MoraleX = 380,
            Line = 11;

        private static readonly ImmutableArray<(string Head, int X)> Columns =
        [
            (K.ColGp, 170),
            (K.ColW, 196),
            (K.ColOtw, 216),
            (K.ColOtl, 246),
            (K.ColL, 276),
            (K.ColGoals, 296),
            (K.ColPts, 348),
        ];

        /// <inheritdoc/>
        public override IReadOnlyList<MenuItem> Items()
        {
            Maybe<SeasonState> season = App.Season.Current;
            string play = Text.Get(
                season.Is(s => s.IsOver && !s.Playoffs.HasValue)
                    ? K.StartPlayoffsItem
                    : K.PlayNextGame
            );
            List<MenuItem> items =
            [
                new MenuItem(play, Flow.PlaySeasonGame, enabled: Flow.SeasonHasNextGame()),
            ];
            if (Flow.Settings.SimGame)
            {
                items.Add(
                    new MenuItem(
                        Text.Get(K.SimNextGame),
                        Flow.SimSeasonGame,
                        enabled: Flow.CanSimGame()
                    )
                );
            }

            if (season.Is(s => s.Playoffs.HasValue))
            {
                items.Add(
                    new MenuItem(Text.Get(K.Playoffs), () => Flow.Replace(new BracketScreen(App)))
                );
            }

            items.Add(
                new MenuItem(
                    Text.Get(K.SeasonStats),
                    () => Flow.Replace(new SeasonStatsScreen(App))
                )
            );
            items.Add(new MenuItem(Text.Get(K.MainMenu), Back));
            return items;
        }

        /// <inheritdoc/>
        public override void Draw(ScreenArt art, World world)
        {
            if (!App.Season.Current.TryGet(out SeasonState season))
            {
                return;
            }

            IReadOnlyList<ClubData> clubs = Flow.Clubs;
            if (season.League == League.Nhl)
            {
                DrawNhl(art, season, clubs);
                return;
            }

            IReadOnlyList<StandingRow> table = season.Table;
            IReadOnlyList<MenuItem> items = Items();
            int h = PanelHeight(table, items.Count);
            int round = Round(season);
            bool morale = TeamMorale.Scale(season) > 0f;
            int w = PanelW + (morale ? MoraleW : 0);
            (int x, int top) = art.Panel(
                LeagueText.Of(season.League).TableHeading(season.IsOver, round, season.Rounds),
                w,
                h,
                Math.Max(2, (Game.Height - h - 14) / 2)
            );

            DrawColumns(art, x, top, morale);
            top = DrawTable(art, x, top, table, clubs, season, morale);
            DrawNextGame(art, top, season, clubs);
            art.Menu(items, Selected, x + 100, w - 200, top + Line + 4);
        }

        /// <summary>Gets a value indicating whether the NHL standings show the wild card view rather than the divisions.</summary>
        public bool WildCardView { get; private set; }

        /// <inheritdoc/>
        public override void Update(World world, IReadOnlySet<MenuButton> press)
        {
            bool nhl = App.Season.Current.Is(s => s.League == League.Nhl);
            if (nhl && (press.Contains(MenuButton.Left) || press.Contains(MenuButton.Right)))
            {
                WildCardView = !WildCardView; // no row here changes with left and right
                return;
            }

            base.Update(world, press);
        }

        /// <summary>The NHL standings: the four divisions side by side under their conferences, with the
        /// playoff spots as they stand (<see cref="NhlStandingsDraw"/>), or with left and right the wild
        /// card view (each conference's division leaders and its wild card race), the next game and the menu.</summary>
        private void DrawNhl(ScreenArt art, SeasonState season, IReadOnlyList<ClubData> clubs)
        {
            const int W = 468,
                WildCardLine = 10; // a pixel tighter, so the longer view clears the hint
            IReadOnlyList<MenuItem> items = Items();
            int line = WildCardView ? WildCardLine : Line;
            int lines = WildCardView
                ? NhlStandingsDraw.WildCardLines(season)
                : NhlStandingsDraw.Lines(season);
            int h =
                5
                + ScreenArt.Row
                + 2
                + (lines * line)
                + 4
                + Line
                + 4
                + (items.Count * ScreenArt.Row)
                + 4;
            (int x, int top) = art.Panel(
                LeagueText
                    .Of(season.League)
                    .TableHeading(season.IsOver, Round(season), season.Rounds),
                W,
                h,
                Math.Max(2, (Game.Height - h - 14) / 2)
            );
            NhlStandingsDraw.Inks inks = new(art.Text, art.Gold, ScreenArt.Muted);
            top = WildCardView
                ? NhlStandingsDraw.DrawWildCard(
                    art.Hud,
                    season,
                    clubs,
                    x + 6,
                    top,
                    W - 12,
                    line,
                    inks
                )
                : NhlStandingsDraw.Draw(art.Hud, season, clubs, x + 6, top, W - 12, line, inks);
            DrawNextGame(art, top + 4, season, clubs);
            art.Menu(items, Selected, x + 144, W - 288, top + 4 + Line + 4);
            art.Hint(Text.Get(WildCardView ? K.HintStandingsDivisions : K.HintStandingsWildCard));
        }

        private static int PanelHeight(IReadOnlyList<StandingRow> table, int itemCount)
        {
            return 5
                + ScreenArt.Row
                + 2
                + Line
                + (table.Count * Line)
                + 4
                + Line
                + 4
                + (itemCount * ScreenArt.Row)
                + 4;
        }

        private static void DrawColumns(ScreenArt art, int x, int top, bool morale)
        {
            foreach ((string head, int cx) in Columns)
            {
                art.Left(Text.Get(head), x + cx, top, ScreenArt.Muted);
            }

            if (morale)
            {
                art.Left(Text.Get(K.ColMorale), x + MoraleX, top, ScreenArt.Muted);
            }
        }

        private static int DrawTable(
            ScreenArt art,
            int x,
            int top,
            IReadOnlyList<StandingRow> table,
            IReadOnlyList<ClubData> clubs,
            SeasonState season,
            bool morale
        )
        {
            string userClub = season.UserClub;
            Maybe<string> relegated = season.Relegated;
            top += Line;
            for (int i = 0; i < table.Count; i++)
            {
                StandingRow r = table[i];
                Color ink = string.Equals(r.ClubId, userClub, StringComparison.Ordinal)
                    ? art.Gold
                    : art.Text;
                if (relegated.Is(d => string.Equals(d, r.ClubId, StringComparison.Ordinal)))
                {
                    ink = ScreenArt.Muted; // went down in the playout (W76)
                }

                string name = Name(clubs, r.ClubId);
                art.Right(N(i + 1), x + 22, top, ink);
                art.Left(name, x + 30, top, ink);
                string[] cells =
                [
                    N(r.Played),
                    N(r.Wins),
                    N(r.OvertimeWins),
                    N(r.OvertimeLosses),
                    N(r.Losses),
                    $"{N(r.GoalsFor)}-{N(r.GoalsAgainst)}",
                    N(r.Points),
                ];
                for (int c = 0; c < cells.Length; c++)
                {
                    art.Left(cells[c], x + Columns[c].X, top, ink);
                }

                if (morale)
                {
                    art.Left(N(TeamMorale.Of(season, r.ClubId)), x + MoraleX, top, ink);
                }

                top += Line;
            }

            return top + 4;
        }

        private static void DrawNextGame(
            ScreenArt art,
            int top,
            SeasonState season,
            IReadOnlyList<ClubData> clubs
        )
        {
            string nextText = season.IsOver
                ? Text.Get(K.RegularSeasonOver)
                : Text.Get(K.WaitingForRound);
            string next =
                season.NextGame.TryGet(out Fixture g)
                    ? Text.Format(K.NextMatch, Name(clubs, g.Home), Name(clubs, g.Away))
                : season.Relegated.TryGet(out string down)
                    ? Text.Format(K.Relegated, Name(clubs, down))
                : nextText;
            art.Centred(next, Game.Width / 2, top, art.Gold);
        }

        /// <summary>The round in the header: the one the human's next game is in, as the NEXT line under the
        /// table says, from 1. Once the human's club has played all its games, the round being finished.</summary>
        public static int Round(SeasonState season)
        {
            int round = season.NextGame.TryGet(out Fixture next) ? next.Round : season.CurrentRound;
            return Math.Clamp(round + 1, 1, Math.Max(1, season.Rounds));
        }

        private static string Name(IReadOnlyList<ClubData> clubs, string id)
        {
            return clubs
                .FirstOrNone(c => string.Equals(c.Id, id, StringComparison.Ordinal))
                .Select(c => c.Name)
                .Or(id);
        }

        private static string N(int n)
        {
            return n.ToString(CultureInfo.InvariantCulture);
        }
    }
}
