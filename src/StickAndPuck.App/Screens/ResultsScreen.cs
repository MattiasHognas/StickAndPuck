using System.Globalization;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Arcade;
using StickAndPuck.App.Season.Stats;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens
{
    /// <summary>Results: the final score with crests, how it was decided, shots and the goals, each club's in its
    /// own column (period, clock, scorer cut to fit), and the player of the game. Season games go on to
    /// the dressing room (<see cref="ScreenFlow.AfterSeasonGame"/>; the standings when it is off or the
    /// season has no game left), the season stats or the trophy once the champion is crowned; an arcade run that is
    /// over goes on to its trophy or the best runs board.</summary>
    internal sealed class ResultsScreen(AppState app, MatchResult result) : MenuScreen(app)
    {
        private const int PanelW = 400,
            Line = 11,
            ClockX = 20,
            ScorerX = 50;

        /// <inheritdoc/>
        public override IReadOnlyList<MenuItem> Items()
        {
            MatchPlan plan = result.Plan;
            return plan.Season ? SeasonItems()
                : plan.Mode.Is(m => m is ArcadeMode)
                && App.Arcade.Current.TryGet(out ArcadeRun run)
                && run.IsOver
                    ? ArcadeItems(run)
                : GameItems(plan);
        }

        private List<MenuItem> SeasonItems()
        {
            List<MenuItem> items = [];
            if (App.Playoffs.Current.Is(p => p.IsDone))
            {
                items.Add(
                    new MenuItem(
                        SeasonEnd.Label(App, K.Champions),
                        () => Flow.Replace(SeasonEnd.Screen(App))
                    )
                );
            }

            Screen after = Flow.AfterSeasonGame();
            items.Add(
                new MenuItem(
                    Text.Get(
                        after switch
                        {
                            TradeOfferScreen => K.TradeOffer,
                            DressingRoomScreen => K.DressingRoom,
                            _ => K.Standings,
                        }
                    ),
                    () => Flow.Replace(after)
                )
            );
            if (Flow.CanSimGame())
            {
                items.Add(new MenuItem(Text.Get(K.SimNextGame), Flow.SimSeasonGame));
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

        private List<MenuItem> ArcadeItems(ArcadeRun run)
        {
            List<MenuItem> items = [];
            if (run.Champion)
            {
                items.Add(
                    new MenuItem(
                        Text.Get(K.Trophy),
                        () => Flow.Replace(TrophyScreen.Arcade(App, run))
                    )
                );
            }

            items.Add(
                new MenuItem(Text.Get(K.BestRuns), () => Flow.Replace(new ArcadeBoardScreen(App)))
            );
            items.Add(new MenuItem(Text.Get(K.MainMenu), Back));
            return items;
        }

        private IReadOnlyList<MenuItem> GameItems(MatchPlan plan)
        {
            return (plan.Mode.TryGet(out IGameMode mode) && mode.HasNextGame(App))
                ?
                [
                    new MenuItem(Text.Get(K.NextGame), () => Flow.PlayMode(mode, plan.Picks)),
                    new MenuItem(Text.Get(K.MainMenu), Back),
                ]
                : (
                    (plan.Mode.TryGet(out IGameMode played) && played is ExhibitionMode exhibition)
                        ?
                        [
                            new MenuItem(
                                Text.Get(K.PlayAgain),
                                () => Flow.PlayMode(exhibition, plan.Picks)
                            ),
                            new MenuItem(Text.Get(K.MainMenu), Back),
                        ]
                        : [new MenuItem(Text.Get(K.MainMenu), Back)]
                );
        }

        /// <inheritdoc/>
        protected override void Back()
        {
            Flow.Pop();
        }

        /// <inheritdoc/>
        public override void Draw(ScreenArt art, World world)
        {
            IReadOnlyList<MenuItem> items = Items();
            Maybe<string> status = Status();
            Maybe<MatchStatLine> star = PlayerOfGame();
            List<GoalLine> home = [.. result.Goals.Where(g => g.Side == Side.Home)];
            List<GoalLine> away = [.. result.Goals.Where(g => g.Side == Side.Away)];
            int rows = ComputeRows(home, away, items.Count, star.HasValue, status.HasValue);
            int h = ComputeHeight(rows, items.Count, star.HasValue, status.HasValue);
            string heading =
                result.Decided.Length > 0
                    ? Text.Format(K.FinalDecided, result.Decided)
                    : Text.Get(K.Final);
            (int x, int top) = art.Panel(
                heading,
                PanelW,
                h,
                Math.Max(4, (Game.Height - h - 16) / 2)
            );
            int cx = Game.Width / 2;

            DrawHeader(art, x, top, cx);
            top += 24;
            art.Centred(
                Text.Format(K.ResultShots, result.HomeShots, result.AwayShots),
                cx,
                top,
                ScreenArt.Muted
            );
            top += 14 + 6;
            if (result.Goals.Count == 0)
            {
                art.Centred(Text.Get(K.NoGoals), cx, top, ScreenArt.Muted);
            }

            int colW = cx - 8 - (x + 12);
            Column(art, home, x + 12, colW, top, rows);
            Column(art, away, cx + 8, colW, top, rows);
            top += (rows * Line) + 6;
            DrawRecognition(art, cx, top, star, status);
            art.Menu(items, Selected, x + 80, PanelW - 160, top);
        }

        private Maybe<string> Status()
        {
            return result.Plan.Mode.TryGet(out IGameMode mode)
                ? mode.Status(App)
                : Maybe.None<string>();
        }

        private Maybe<MatchStatLine> PlayerOfGame()
        {
            return
                App.LastMatchStats.TryGet(out MatchStatSheet last)
                && last.PlayerOfGame.TryGet(out MatchStatLine p)
                && string.Equals(last.Home, result.Setup.Home.Id, StringComparison.Ordinal)
                && string.Equals(last.Away, result.Setup.Away.Id, StringComparison.Ordinal)
                ? p
                : Maybe.None<MatchStatLine>();
        }

        private static int ComputeRows(
            List<GoalLine> home,
            List<GoalLine> away,
            int itemCount,
            bool hasStar,
            bool hasStatus
        )
        {
            int extra =
                5
                + ScreenArt.Row
                + 2
                + 24
                + 14
                + 6
                + 6
                + (hasStar ? 14 : 0)
                + (hasStatus ? 14 : 0)
                + (itemCount * ScreenArt.Row)
                + 6;
            int fits = Math.Max(2, (Game.Height - 8 - extra) / Line);
            return Math.Max(1, Math.Min(Math.Max(home.Count, away.Count), fits));
        }

        private static int ComputeHeight(int rows, int itemCount, bool hasStar, bool hasStatus)
        {
            return 5
                + ScreenArt.Row
                + 2
                + 24
                + 14
                + 6
                + 6
                + (hasStar ? 14 : 0)
                + (hasStatus ? 14 : 0)
                + (itemCount * ScreenArt.Row)
                + 6
                + (rows * Line);
        }

        private void DrawHeader(ScreenArt art, int x, int top, int cx)
        {
            ClubData homeClub = result.Setup.Home;
            ClubData awayClub = result.Setup.Away;
            art.Hud.DrawCrest(homeClub.Id, cx - 40 - 16, top);
            art.Hud.DrawCrest(awayClub.Id, cx + 40, top);
            art.Centred($"{N(result.HomeGoals)} - {N(result.AwayGoals)}", cx, top + 3, art.Gold);
            art.Right(
                art.ClubName(homeClub.Name, cx - 62 - x - 8),
                cx - 40 - 22,
                top + 3,
                Ink(art, Side.Home)
            );
            art.Left(
                art.ClubName(awayClub.Name, x + PanelW - 8 - (cx + 62)),
                cx + 40 + 22,
                top + 3,
                Ink(art, Side.Away)
            );
        }

        private static void DrawRecognition(
            ScreenArt art,
            int cx,
            int top,
            Maybe<MatchStatLine> star,
            Maybe<string> status
        )
        {
            if (star.TryGet(out MatchStatLine shown))
            {
                art.Centred(
                    shown.IsGoalie
                        ? Text.Format(K.PlayerOfGameSaves, shown.Label, shown.Saves)
                        : Text.Format(
                            K.PlayerOfGamePoints,
                            shown.Label,
                            shown.Goals,
                            shown.Assists
                        ),
                    cx,
                    top,
                    art.Gold
                );
                top += 14;
            }
            if (status.HasValue)
            {
                art.Centred(status.Or(""), cx, top, art.Gold);
            }
        }

        /// <summary>One club's goals, oldest first: period, clock and the scorer cut to the column; "+N MORE" when they do not fit.</summary>
        private static void Column(
            ScreenArt art,
            List<GoalLine> goals,
            int x,
            int w,
            int top,
            int rows
        )
        {
            int shown = goals.Count > rows ? rows - 1 : goals.Count;
            for (int i = 0; i < shown; i++)
            {
                GoalLine g = goals[i];
                Color ink =
                    string.Equals(g.Period, "1", StringComparison.Ordinal)
                    || string.Equals(g.Period, "2", StringComparison.Ordinal)
                    || string.Equals(g.Period, "3", StringComparison.Ordinal)
                        ? art.Text
                        : art.Gold; // overtime and shootout goals stand out
                art.Left(g.Period, x, top, ink);
                art.Left(g.Clock, x + ClockX, top, ink);
                art.Left(art.Clip(g.Scorer, w - ScorerX), x + ScorerX, top, ink);
                top += Line;
            }
            if (goals.Count > shown)
            {
                art.Left(
                    Text.Format(K.MoreGoals, goals.Count - shown),
                    x + ScorerX,
                    top,
                    ScreenArt.Muted
                );
            }
        }

        private Color Ink(ScreenArt art, Side side)
        {
            int mine = side == Side.Home ? result.HomeGoals : result.AwayGoals,
                theirs = side == Side.Home ? result.AwayGoals : result.HomeGoals;
            return mine > theirs ? art.Gold : art.Text;
        }

        private static string N(int n)
        {
            return n.ToString(CultureInfo.InvariantCulture);
        }
    }
}
