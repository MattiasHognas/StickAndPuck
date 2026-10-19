using System.Globalization;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Playoffs;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens
{
    /// <summary>The playoff bracket of the season being played (<see cref="PlayoffState"/>): one column
    /// per round, each series as two seeds with their wins, the human's club in gold and series losers
    /// greyed, and the champion or the human's next game underneath. Enter plays it.</summary>
    internal sealed class BracketScreen(AppState app) : MenuScreen(app)
    {
        private const int PanelW = 468,
            Line = 11,
            Column = 114;

        /// <inheritdoc/>
        public override IReadOnlyList<MenuItem> Items()
        {
            List<MenuItem> items = [];
            bool decided = App.Playoffs.Current.Is(p => p.Champion.HasValue);
            if (!decided) // no games are left once the final is won, and the menu has to fit the frame
            {
                items.Add(
                    new MenuItem(
                        Text.Get(K.PlayNextGame),
                        Flow.PlaySeasonGame,
                        enabled: Flow.SeasonHasNextGame()
                    )
                );
            }

            if (Flow.Settings.SimGame && !decided)
            {
                items.Add(
                    new MenuItem(
                        Text.Get(K.SimNextGame),
                        Flow.SimSeasonGame,
                        enabled: Flow.CanSimGame()
                    )
                );
            }

            if (decided)
            {
                items.Add(
                    new MenuItem(Text.Get(K.Trophy), () => Flow.Push(TrophyScreen.Season(App)))
                );
            }

            items.Add(
                new MenuItem(Text.Get(K.TableItem), () => Flow.Replace(new StandingsScreen(App)))
            );
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
            if (!App.Playoffs.Current.TryGet(out PlayoffState playoffs))
            {
                return;
            }

            IReadOnlyList<ClubData> clubs = Flow.Clubs;
            IReadOnlyList<MenuItem> items = Items();
            int rows = 4 * 3;
            int h = BracketHeight(rows, items.Count);
            bool nhl = playoffs.League == League.Nhl;
            (int x, int top) = art.Panel(
                Text.Get(LeagueText.Of(playoffs.League).Playoffs),
                PanelW,
                h,
                Math.Max(2, (Game.Height - h - 15) / 2)
            );

            if (nhl)
            {
                NhlBracketDraw.Draw(
                    art.Hud,
                    playoffs,
                    clubs,
                    x + 6,
                    top,
                    PanelW - 12,
                    Line + 4 + (rows * Line),
                    Line,
                    (art.Text, art.Gold, ScreenArt.Muted)
                );
            }
            else
            {
                DrawRounds(art, playoffs, clubs, x, top, rows);
            }
            top += Line + 4 + (rows * Line) + 6;
            art.Centred(Footer(playoffs, clubs), Game.Width / 2, top, art.Gold);
            top += Line + 4;
            art.Menu(items, Selected, x + 140, PanelW - 280, top);
        }

        private static int BracketHeight(int rows, int itemCount)
        {
            return 5
                + ScreenArt.Row
                + 2
                + Line
                + 4
                + (rows * Line)
                + 6
                + Line
                + 4
                + (itemCount * ScreenArt.Row)
                + 4;
        }

        private static void DrawRounds(
            ScreenArt art,
            PlayoffState playoffs,
            IReadOnlyList<ClubData> clubs,
            int x,
            int top,
            int rows
        )
        {
            for (int r = 0; r < PlayoffState.RoundCount; r++)
            {
                int cx = x + 10 + (r * Column);
                bool drawn = r < playoffs.Rounds.Count;
                art.Left(PlayoffState.RoundTitle(r), cx, top, !drawn ? ScreenArt.Muted : art.Text);
                if (!drawn)
                {
                    continue;
                }

                PlayoffRound round = playoffs.Rounds[r];
                int gap =
                    round.Series.Count <= 1
                        ? 0
                        : ((rows * Line) - (2 * Line)) / (round.Series.Count - 1);
                gap = Math.Min(gap, 3 * Line);
                for (int s = 0; s < round.Series.Count; s++)
                {
                    DrawSeries(
                        art,
                        round.Series[s],
                        playoffs.UserClub,
                        clubs,
                        cx,
                        top + Line + 4 + (s * gap)
                    );
                }
            }
        }

        private static void DrawSeries(
            ScreenArt art,
            PlayoffSeries series,
            string user,
            IReadOnlyList<ClubData> clubs,
            int x,
            int y
        )
        {
            DrawSide(art, series.HighSeed, series.High, series.HighWins, series, user, clubs, x, y);
            DrawSide(
                art,
                series.LowSeed,
                series.Low,
                series.LowWins,
                series,
                user,
                clubs,
                x,
                y + Line
            );
        }

        private static void DrawSide(
            ScreenArt art,
            int seed,
            string club,
            int wins,
            PlayoffSeries series,
            string user,
            IReadOnlyList<ClubData> clubs,
            int x,
            int y
        )
        {
            Color ink = string.Equals(club, user, StringComparison.Ordinal) ? art.Gold : art.Text;
            if (
                series.IsOver
                && !series.Winner.Is(w => string.Equals(w, club, StringComparison.Ordinal))
            )
            {
                ink = ScreenArt.Muted;
            }

            art.Right(N(seed), x + 10, y, ink);
            art.Left(Short(clubs, club), x + 16, y, ink);
            art.Right(N(wins), x + 76, y, ink);
        }

        private static string Footer(PlayoffState playoffs, IReadOnlyList<ClubData> clubs)
        {
            if (playoffs.IsOver)
            {
                return Text.Format(
                    LeagueText.Of(playoffs.League).ChampionsOf,
                    Name(clubs, playoffs.Champion.Or(""))
                );
            }

            string user = Name(clubs, playoffs.UserClub);
            if (!playoffs.UserQualified)
            {
                return Text.Format(K.MissedPlayoffs, user);
            }

            if (playoffs.UserSeries.TryGet(out PlayoffSeries s))
            {
                string other = Name(
                    clubs,
                    string.Equals(s.High, playoffs.UserClub, StringComparison.Ordinal)
                        ? s.Low
                        : s.High
                );
                return Text.Format(
                    K.NextPlayoffGame,
                    playoffs.CurrentTitle,
                    s.Games.Count + 1,
                    other
                );
            }
            return Text.Format(playoffs.UserAlive ? K.WaitForNextRound : K.AreOut, user);
        }

        private static string Short(IReadOnlyList<ClubData> clubs, string id)
        {
            ClubData club = clubs.FirstOrDefault(c =>
                string.Equals(c.Id, id, StringComparison.Ordinal)
            );
            return club is { ShortName.Length: > 0 } ? club.ShortName : id;
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
