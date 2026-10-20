using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Season.DressingRoom;
using StickAndPuck.App.Season.Playoffs;
using StickAndPuck.App.Season.Schedule;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens
{
    /// <summary>The dressing room (W122): after each season or playoff result and before the next game. The
    /// coach's portrait and line, the last game, then the news and injuries side by side and the board's
    /// notes, all from <see cref="DressingRoomSources"/>; the next game under them and the menu: play it,
    /// sim it, the table or bracket, the main menu (and the trade deadline's open offers first, W135). The season's <see cref="RuleSet.DressingRoom"/> sets how
    /// much shows (Brief keeps the coach and the games) or turns the screen off. The backdrop and portrait
    /// are A20's art when it exists (<see cref="Assets.DressingRoomBackdrop"/>, <see cref="Assets.CoachPortrait"/>).</summary>
    internal sealed class DressingRoomScreen(AppState app) : MenuScreen(app)
    {
        private const int PanelW = 440,
            Line = 11,
            PortraitW = 64,
            PortraitH = 80,
            CoachLines = 3,
            ListLines = 3;

        /// <summary>Gets the dressing room style the current season plays: the rules in its save, or the
        /// player's choice before its first game stores them.</summary>
        public static DressingRoomStyle Style(AppState app)
        {
            return Rules(app).DressingRoom;
        }

        /// <inheritdoc/>
        public override IReadOnlyList<MenuItem> Items()
        {
            Maybe<SeasonState> season = App.Season.Current;
            string play = Text.Get(
                season.Is(s => s.IsOver && !s.Playoffs.HasValue)
                    ? K.StartPlayoffsItem
                    : K.PlayNextGame
            );
            List<MenuItem> items = [];
            int offers = season.Select(s => s.Trades.Open.Count).Or(0);
            if (offers > 0 && Flow.SeasonHasNextGame())
            {
                items.Add(
                    new MenuItem(
                        Text.Format(K.TradeOffersItem, offers),
                        () => Flow.Push(new TradeOfferScreen(App))
                    )
                );
            }

            items.Add(new MenuItem(play, Play, enabled: Flow.SeasonHasNextGame()));
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

            items.Add(
                new MenuItem(
                    Text.Get(season.Is(s => s.Playoffs.HasValue) ? K.Playoffs : K.Standings),
                    () => Flow.Replace(Flow.Overview())
                )
            );
            items.Add(new MenuItem(Text.Get(K.MainMenu), Back));
            return items;
        }

        /// <summary>Plays the next game: through the coach's pre-game talk when the season's rules have it
        /// (<see cref="RuleSet.PreGameTalk"/>), else straight away.</summary>
        private void Play()
        {
            if (Rules(App).PreGameTalk)
            {
                Flow.Push(new CoachTalkScreen(App));
                return;
            }

            Flow.PlaySeasonGame();
        }

        /// <inheritdoc/>
        public override void Draw(ScreenArt art, World world)
        {
            if (!App.Season.Current.TryGet(out SeasonState season))
            {
                return;
            }

            RuleSet rules = Rules(App);
            bool full = rules.DressingRoom == DressingRoomStyle.Full;
            DressingRoomContext context = DressingRoomContext.Of(season, Flow.Clubs, rules);
            KitColours kit = Flow
                .Clubs.FirstOrNone(c =>
                    string.Equals(c.Id, season.UserClub, StringComparison.Ordinal)
                )
                .Select(c => c.Home)
                .Or(new KitColours());
            bool room = art.DrawDressingRoom(kit);
            DressingRoomReport report = DressingRoomSources.Build(context);
            IReadOnlyList<MenuItem> items = Items();
            bool board = full && report.Board.Count > 0;
            int h =
                5
                + ScreenArt.Row
                + 2
                + Line
                + 4
                + PortraitH
                + 6
                + (full ? Line + (ListLines * Line) + 6 : 0)
                + (board ? Line + Line + 6 : 0)
                + Line
                + 6
                + (items.Count * ScreenArt.Row)
                + 4;
            int py = Math.Max(2, (Game.Height - h - 14) / 2);
            (int x, int top) = room
                ? art.RoomPanel(kit, Text.Get(K.DressingRoom), PanelW, h, py)
                : art.Panel(Text.Get(K.DressingRoom), PanelW, h, py);
            int cx = Game.Width / 2;
            art.Centred(Stage(season), cx, top, ScreenArt.Muted);
            top += Line + 4;

            DrawCoach(art, x, top, report, context);
            top += PortraitH + 6;
            if (full)
            {
                int colW = (PanelW - 30) / 2;
                DrawList(
                    art,
                    x + 10,
                    colW,
                    top,
                    Text.Get(K.RoomNews),
                    report.News,
                    Text.Get(K.RoomNoNews)
                );
                DrawList(
                    art,
                    x + 20 + colW,
                    colW,
                    top,
                    Text.Get(K.RoomInjuries),
                    report.Injuries,
                    Text.Get(K.RoomNoInjuries)
                );
                top += Line + (ListLines * Line) + 6;
            }

            if (board)
            {
                art.Left(Text.Get(K.RoomBoard), x + 10, top, art.Gold);
                DressingRoomEntry note = report.Board[0];
                art.Left(
                    art.Clip(note.Text, PanelW - 20),
                    x + 10,
                    top + Line,
                    note.Highlight ? art.Gold : art.Text
                );
                top += Line + Line + 6;
            }

            art.Centred(Next(season, context), cx, top, art.Gold);
            top += Line + 6;
            art.Menu(items, Selected, x + 120, PanelW - 240, top);
        }

        /// <summary>The portrait on the left; COACH, the coach's line wrapped to three lines and the last game on the right.</summary>
        private static void DrawCoach(
            ScreenArt art,
            int x,
            int top,
            DressingRoomReport report,
            DressingRoomContext context
        )
        {
            string coach = Text.Get(K.RoomCoach);
            art.DrawCoach(
                x + 10,
                top,
                PortraitW,
                PortraitH,
                coach,
                report.Coach.Select(c => c.Mood).Or(CoachMood.Neutral)
            );
            int tx = x + 10 + PortraitW + 10;
            int tw = PanelW - (tx - x) - 10;
            art.Left(coach, tx, top, art.Gold);
            int y = top + Line + 2;
            if (report.Coach.TryGet(out DressingRoomEntry said))
            {
                foreach (string line in Wrap(art, said.Text, tw).Take(CoachLines))
                {
                    art.Left(line, tx, y, said.Highlight ? art.Gold : art.Text);
                    y += Line;
                }
            }

            int ly = top + PortraitH - Line - Line;
            art.Left(Text.Get(K.RoomLastGame), tx, ly, ScreenArt.Muted);
            if (!context.Last.TryGet(out LastGame last))
            {
                art.Left(Text.Get(K.RoomNoGameYet), tx, ly + Line, art.Text);
                return;
            }

            string kind = last.Result.Kind switch
            {
                ResultKind.Overtime => Text.Get(K.KindOt),
                ResultKind.Shootout => Text.Get(K.KindShootout),
                ResultKind.Regulation => "",
                _ => "",
            };
            string score = Text.Format(
                K.RoomScoreLine,
                context.Name(last.Home),
                last.Result.HomeGoals,
                last.Result.AwayGoals,
                context.Name(last.Away)
            );
            art.Left(art.Clip(score + kind, tw), tx, ly + Line, last.Won ? art.Gold : art.Text);
        }

        /// <summary>One list under its heading: up to three lines, or the empty-state line in grey.</summary>
        private static void DrawList(
            ScreenArt art,
            int x,
            int w,
            int top,
            string heading,
            IReadOnlyList<DressingRoomEntry> entries,
            string empty
        )
        {
            art.Left(heading, x, top, art.Gold);
            top += Line;
            if (entries.Count == 0)
            {
                art.Left(art.Clip(empty, w), x, top, ScreenArt.Muted);
                return;
            }

            foreach (DressingRoomEntry e in entries.Take(ListLines))
            {
                Color ink = e.Highlight ? art.Gold : art.Text;
                art.Left(art.Clip(e.Text, w), x, top, ink);
                top += Line;
            }
        }

        /// <summary>The round under the heading: the league round, or the playoff round once the playoffs are on.</summary>
        private static string Stage(SeasonState season)
        {
            return season.Playoffs.TryGet(out PlayoffState playoffs)
                ? playoffs.UserTitle
                : Text.Format(
                    LeagueText.Of(season.League).Round,
                    StandingsScreen.Round(season),
                    season.Rounds
                );
        }

        /// <summary>The next game: the human's next fixture or playoff game, else where the season stands.</summary>
        private static string Next(SeasonState season, DressingRoomContext context)
        {
            (string home, string away) =
                season.Playoffs.TryGet(out PlayoffState playoffs)
                && playoffs.UserSeries.TryGet(out PlayoffSeries series)
                    ? (series.NextHome, series.NextAway)
                : season.NextGame.TryGet(out Fixture g) ? (g.Home, g.Away)
                : ("", "");
            return home.Length > 0
                ? Text.Format(K.NextMatch, context.Name(home), context.Name(away))
                : Text.Get(season.IsOver ? K.RegularSeasonOver : K.WaitingForRound);
        }

        /// <summary>Breaks text into lines that fit a width, at spaces.</summary>
        internal static List<string> Wrap(ScreenArt art, string text, int width)
        {
            List<string> lines = [];
            string line = "";
            foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                string tried = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && art.Hud.Measure(ScreenArt.Fit(tried)) > width)
                {
                    lines.Add(line);
                    line = word;
                }
                else
                {
                    line = tried;
                }
            }

            if (line.Length > 0)
            {
                lines.Add(line);
            }

            return lines;
        }

        /// <summary>Gets the rules the current season plays: those in its save, or the player's choice before its first game stores them.</summary>
        internal static RuleSet Rules(AppState app)
        {
            return
                app.Season.Current.TryGet(out SeasonState season)
                && season.Rules.TryGet(out RuleSet stored)
                ? stored
                : RulePicks.Choice(app).Resolve();
        }
    }
}
