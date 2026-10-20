using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Playoffs;
using StickAndPuck.App.Season.Schedule;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens
{
    /// <summary>Where the season ends when the human's club loses the SHL playout (W76): the club's crest and
    /// name, that it goes down, the playout result and the champion, in place of the trophy screen.</summary>
    internal sealed class RelegationScreen(AppState app) : MenuScreen(app)
    {
        private const int PanelW = 360,
            Line = 12;

        /// <inheritdoc/>
        public override IReadOnlyList<MenuItem> Items()
        {
            return
            [
                new MenuItem(Text.Get(K.Continue), Flow.Pop),
                new MenuItem(
                    Text.Get(K.SeasonStats),
                    () => Flow.Replace(new SeasonStatsScreen(App))
                ),
            ];
        }

        /// <summary>Gets the lines under the club: the playout result and the champion, label and value.</summary>
        public static List<(string Label, string Value)> Lines(
            PlayoffState playoffs,
            IReadOnlyList<ClubData> clubs
        )
        {
            List<(string, string)> lines = [];
            if (playoffs.Playout.TryGet(out PlayoffSeries playout))
            {
                string user = playoffs.UserClub;
                bool high = string.Equals(playout.High, user, StringComparison.Ordinal);
                int won = high ? playout.HighWins : playout.LowWins,
                    lost = high ? playout.LowWins : playout.HighWins;
                lines.Add(
                    (
                        Text.Get(K.PlayoutTitle),
                        Text.Format(
                            K.PlayoutLine,
                            won,
                            lost,
                            Name(clubs, high ? playout.Low : playout.High)
                        )
                    )
                );
            }

            if (playoffs.Champion.TryGet(out string champion))
            {
                lines.Add((Text.Get(K.Champions), Name(clubs, champion)));
            }

            return lines;
        }

        /// <inheritdoc/>
        public override void Draw(ScreenArt art, World world)
        {
            if (
                !App.Season.Current.TryGet(out SeasonState season)
                || !season.Playoffs.TryGet(out PlayoffState playoffs)
            )
            {
                return;
            }

            IReadOnlyList<ClubData> clubs = Flow.Clubs;
            IReadOnlyList<MenuItem> items = Items();
            List<(string Label, string Value)> lines = Lines(playoffs, clubs);
            int body = 22 + 14 + (lines.Count * Line) + 4 + Line;
            int h = 5 + ScreenArt.Row + 2 + body + 8 + (items.Count * ScreenArt.Row) + 6;
            (int x, int top) = art.Panel(
                Text.Get(K.Relegation),
                PanelW,
                h,
                Math.Max(4, (Game.Height - h - 16) / 2)
            );
            int cx = Game.Width / 2;
            string club = Name(clubs, season.UserClub);
            art.Hud.DrawCrest(season.UserClub, x + 14, top);
            art.Left(art.Clip(club, PanelW - 50), x + 38, top + 3, art.Text);
            int y = top + 22;
            art.Centred(Text.Format(K.Relegated, club), cx, y, art.Gold);
            y += 14;
            foreach ((string label, string value) in lines)
            {
                art.Left(label, x + 40, y, ScreenArt.Muted);
                art.Left(art.Clip(value, PanelW - 140), x + 120, y, art.Text);
                y += Line;
            }

            art.Centred(Text.Get(K.RelegationNote), cx, y + 4, ScreenArt.Muted);
            art.Menu(items, Selected, x + 80, PanelW - 160, top + body + 8);
        }

        private static string Name(IReadOnlyList<ClubData> clubs, string id)
        {
            return clubs
                .FirstOrNone(c => string.Equals(c.Id, id, StringComparison.Ordinal))
                .Select(c => c.Name)
                .Or(id);
        }
    }
}
