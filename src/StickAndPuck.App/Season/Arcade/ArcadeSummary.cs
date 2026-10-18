using System.Globalization;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Hud;
using StickAndPuck.App.Season.Schedule;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Season.Arcade
{
    /// <summary>A plain arcade panel in the A3 pixel font: the human's club, the rule, clubs beaten and lives
    /// left, then every game with W or L, the score and how it was decided. The heading says whether the
    /// run goes on, ended on a loss, or ended as champions, and after the run the best runs so far
    /// (<see cref="ArcadeRun.Records"/>). The trophy screen reads the same <see cref="ArcadeRun"/>.</summary>
    internal static class ArcadeSummary
    {
        private const int Line = 12,
            Pad = 6,
            Width = 300,
            BestShown = 3;

        /// <summary>Draws the panel centred on the 480x270 frame.</summary>
        public static void Draw(HudArt art, ArcadeRun run, IReadOnlyList<ClubData> clubs)
        {
            List<ArcadeRecord> best = run.IsOver ? [.. run.Records.Take(BestShown)] : [];
            int rows =
                run.Games.Count + 4 + (run.IsOver ? 0 : 1) + (best.Count > 0 ? best.Count + 1 : 0);
            int h = (Pad * 2) + (rows * Line);
            int x = (Game.Width - Width) / 2,
                y = Math.Max(2, (Game.Height - h) / 2);

            art.DrawPanel(GetPanel(run), x, y, Width, h);

            int left = x + Pad + 4,
                top = y + Pad - 2;
            top = DrawHeader(art, run, clubs, left, top, x);
            top = DrawGames(art, run, clubs, left, top, x);

            if (!run.IsOver)
            {
                art.DrawText(
                    Text.Format(
                        run.NextAtHome ? K.NextVs : K.NextAt,
                        ClubName(clubs, run.NextOpponent.Or(""))
                    ),
                    left,
                    top,
                    art.Score
                );
            }

            if (best.Count == 0)
            {
                return;
            }

            DrawBestRuns(art, run, clubs, best, left, top, x);
        }

        private static string GetPanel(ArcadeRun run)
        {
            return run.Champion ? "panel_gold" : (run.Lost ? "panel_red" : "panel");
        }

        private static int DrawHeader(
            HudArt art,
            ArcadeRun run,
            IReadOnlyList<ClubData> clubs,
            int left,
            int top,
            int x
        )
        {
            string head = run.Champion
                ? Text.Get(K.ArcadeHeadChampions)
                : (run.Lost ? Text.Get(K.ArcadeHeadRunOver) : Text.Get(K.ArcadeHead));
            art.DrawText(head, left, top, art.Score);
            top += Line;
            art.DrawText(ClubName(clubs, run.UserClub), left, top, art.Score);
            art.DrawText(
                Text.Get(run.Rule == ArcadeRule.SuddenDeath ? K.SuddenDeath : K.ThreeLives),
                x + 190,
                top,
                art.Text
            );
            top += Line;
            art.DrawText(
                Text.Format(K.BeatenOf, run.Stage, run.Opponents.Count),
                left,
                top,
                art.Text
            );
            art.DrawText(
                Text.Format(K.LivesOf, run.LivesLeft, run.StartLives),
                x + 190,
                top,
                art.Text
            );
            top += Line + (Line / 2);
            return top;
        }

        private static int DrawGames(
            HudArt art,
            ArcadeRun run,
            IReadOnlyList<ClubData> clubs,
            int left,
            int top,
            int x
        )
        {
            int i = 0;
            foreach (ArcadeGame g in run.Games)
            {
                Color ink = g.Won ? art.Text : art.Score;
                string extra = g.Result.Kind switch
                {
                    ResultKind.Overtime => Text.Get(K.SuffixOt),
                    ResultKind.Shootout => Text.Get(K.SuffixSo),
                    ResultKind.Regulation => "",
                    _ => throw new NotSupportedException(
                        $"Unhandled result kind {g.Result.Kind} in arcade summary"
                    ),
                };
                art.DrawText(Num(i + 1), left, top, ink);
                art.DrawText(Text.Get(g.Won ? K.Win : K.Loss), left + 18, top, ink);
                art.DrawText(
                    Text.Format(g.Home ? K.VsClub : K.AtClub, ClubName(clubs, g.Opponent)),
                    left + 34,
                    top,
                    ink
                );
                art.DrawText($"{Num(g.GoalsFor)}-{Num(g.GoalsAgainst)}{extra}", x + 230, top, ink);
                top += Line;
                i++;
            }

            return top;
        }

        private static void DrawBestRuns(
            HudArt art,
            ArcadeRun run,
            IReadOnlyList<ClubData> clubs,
            List<ArcadeRecord> best,
            int left,
            int top,
            int x
        )
        {
            art.DrawText(Text.Get(K.BestRunsHead), left, top, art.Score);
            top += Line;
            for (int i = 0; i < best.Count; i++)
            {
                ArcadeRecord r = best[i];
                bool mine = i + 1 == run.Place;
                Color ink = mine ? art.Score : art.Text;
                art.DrawText(Num(i + 1), left, top, ink);
                art.DrawText(
                    Text.Format(
                        K.BestRunLine,
                        ClubName(clubs, r.Club),
                        r.Champion ? Text.Get(K.Champions) : Text.Format(K.Beaten, r.Beaten)
                    ),
                    left + 18,
                    top,
                    ink
                );
                art.DrawText(
                    Text.Get(r.Rule == ArcadeRule.SuddenDeath ? K.RuleSd : K.Rule3l),
                    x + 230,
                    top,
                    ink
                );
                art.DrawText($"{Num(r.GoalsFor)}-{Num(r.GoalsAgainst)}", x + 250, top, ink);
                top += Line;
            }
        }

        private static string ClubName(IReadOnlyList<ClubData> clubs, string id)
        {
            return clubs
                .FirstOrNone(c => string.Equals(c.Id, id, StringComparison.Ordinal))
                .Select(c => c.Name)
                .Or(id);
        }

        private static string Num(int n)
        {
            return n.ToString(CultureInfo.InvariantCulture);
        }
    }
}
