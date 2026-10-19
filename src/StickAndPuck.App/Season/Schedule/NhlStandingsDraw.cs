using System.Globalization;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Hud;
using StickAndPuck.App.Screens;
using StickAndPuck.App.Season.Playoffs;
using static Raylib_cs.Raylib;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Season.Schedule
{
    /// <summary>The NHL standings by division: four blocks side by side, each conference's two divisions under
    /// its name, every club as its code with wins, losses, overtime losses and points (the round in the header gives the games played). The
    /// place column shows who would make the playoffs if the season ended now (1 to 3 in the division,
    /// WC1 and WC2 in the conference, <see cref="NhlSeeding"/>); the clubs outside are dimmed there.
    /// Used by the standings screen and the table panel after a season game.</summary>
    internal static class NhlStandingsDraw
    {
        /// <summary>The colours the blocks are drawn in.</summary>
        /// <param name="Ink">Text.</param>
        /// <param name="Gold">The human's club and the conference names.</param>
        /// <param name="Muted">Column heads and places outside the playoffs.</param>
        public sealed record Inks(Color Ink, Color Gold, Color Muted);

        /// <summary>Lines the blocks take: the conference names, division names, column heads and the clubs of the biggest division.</summary>
        public static int Lines(SeasonState season)
        {
            IReadOnlyList<DivisionTable> divisions = DivisionStandings.Of(season, season.Table);
            return 3 + (divisions.Count == 0 ? 0 : divisions.Max(d => d.Rows.Count));
        }

        /// <summary>Draws the blocks from the top left corner across a width, a line apart, and returns the y under them.</summary>
        public static int Draw(
            HudArt art,
            SeasonState season,
            IReadOnlyList<ClubData> clubs,
            int x,
            int top,
            int width,
            int line,
            Inks inks
        )
        {
            IReadOnlyList<StandingRow> table = season.Table;
            IReadOnlyList<DivisionTable> divisions = DivisionStandings.Of(season, table);
            if (divisions.Count == 0)
            {
                return top;
            }

            IReadOnlyDictionary<string, string> tags = Tags(season, table);
            int block = width / divisions.Count;
            int deepest = divisions.Max(d => d.Rows.Count);
            for (int d = 0; d < divisions.Count; d++)
            {
                DivisionTable division = divisions[d];
                int bx = x + (d * block);
                bool firstOfConference =
                    d == 0
                    || !string.Equals(
                        divisions[d - 1].Conference,
                        division.Conference,
                        StringComparison.Ordinal
                    );
                if (firstOfConference)
                {
                    int span = divisions
                        .Skip(d)
                        .TakeWhile(o =>
                            string.Equals(
                                o.Conference,
                                division.Conference,
                                StringComparison.Ordinal
                            )
                        )
                        .Count();
                    Centred(
                        art,
                        division.Conference.ToUpperInvariant(),
                        bx + (span * block / 2),
                        top,
                        inks.Gold
                    );
                }

                Left(art, division.Division.ToUpperInvariant(), bx + 4, top + line, inks.Ink);
                DrawHeads(art, bx, block, top + (2 * line), inks.Muted);
                for (int i = 0; i < division.Rows.Count; i++)
                {
                    DrawRow(
                        art,
                        division.Rows[i],
                        i,
                        tags,
                        season.UserClub,
                        clubs,
                        bx,
                        block,
                        top + ((3 + i) * line),
                        inks
                    );
                }
            }
            return top + ((3 + deepest) * line);
        }

        /// <summary>The playoff spot each club holds on the table as it stands, or none outside them.</summary>
        private static IReadOnlyDictionary<string, string> Tags(
            SeasonState season,
            IReadOnlyList<StandingRow> table
        )
        {
            try
            {
                return NhlSeeding.Field(season, table).Tags;
            }
            catch (InvalidOperationException)
            {
                return new Dictionary<string, string>(StringComparer.Ordinal); // not the NHL's shape: places only
            }
        }

        /// <summary>Lines the wild card view takes: the conference names, the first division's name or the
        /// wild card heading, column heads, and the deeper of the leaders (two divisions of three under
        /// their names) and the wild card race.</summary>
        public static int WildCardLines(SeasonState season)
        {
            IReadOnlyList<WildCardTable> conferences = WildCardStandings.Of(season, season.Table);
            int deepest = 0;
            foreach (WildCardTable c in conferences)
            {
                int leaders = c.Leaders.Sum(d => d.Rows.Count) + c.Leaders.Count - 1;
                deepest = Math.Max(deepest, Math.Max(leaders, c.Race.Count));
            }
            return 3 + deepest;
        }

        /// <summary>Draws the wild card view from the top left corner across a width and returns the y under
        /// it: each conference as two blocks under its name, the division leaders (1 to 3 in each division)
        /// and the wild card race, WC1 and WC2 above a line, the clubs chasing them under it.</summary>
        public static int DrawWildCard(
            HudArt art,
            SeasonState season,
            IReadOnlyList<ClubData> clubs,
            int x,
            int top,
            int width,
            int line,
            Inks inks
        )
        {
            IReadOnlyList<WildCardTable> conferences = WildCardStandings.Of(season, season.Table);
            if (conferences.Count == 0)
            {
                return top;
            }

            int block = width / (conferences.Count * 2);
            for (int c = 0; c < conferences.Count; c++)
            {
                WildCardTable conference = conferences[c];
                int lx = x + (c * 2 * block),
                    rx = lx + block;
                Centred(art, conference.Conference.ToUpperInvariant(), rx, top, inks.Gold);
                DrawHeads(art, lx, block, top + (2 * line), inks.Muted);
                DrawHeads(art, rx, block, top + (2 * line), inks.Muted);

                int y = top;
                for (int d = 0; d < conference.Leaders.Count; d++)
                {
                    DivisionTable division = conference.Leaders[d];
                    int ny = d == 0 ? top + line : y; // the first name sits above the heads
                    Left(art, division.Division.ToUpperInvariant(), lx + 4, ny, inks.Ink);
                    y = ny + ((d == 0 ? 2 : 1) * line);
                    for (int i = 0; i < division.Rows.Count; i++)
                    {
                        DrawClub(
                            art,
                            division.Rows[i],
                            N(i + 1),
                            spot: true,
                            season.UserClub,
                            clubs,
                            lx,
                            block,
                            y,
                            inks
                        );
                        y += line;
                    }
                }

                Left(art, Text.Get(K.WildCardHeading), rx + 4, top + line, inks.Ink);
                for (int i = 0; i < conference.Race.Count; i++)
                {
                    bool wild = i < NhlSeeding.WildCards;
                    int ry = top + ((3 + i) * line);
                    if (i == NhlSeeding.WildCards)
                    {
                        DrawRectangle(rx + 4, ry, block - 8, 1, inks.Muted); // the cut line, under the glyphs of WC2
                    }

                    DrawClub(
                        art,
                        conference.Race[i],
                        wild ? TagText("WC" + N(i + 1)) : "",
                        wild,
                        season.UserClub,
                        clubs,
                        rx,
                        block,
                        ry,
                        inks
                    );
                }
            }
            return top + (WildCardLines(season) * line);
        }

        /// <summary>The column right edges in a block: wins, losses, overtime losses, points. Points stop
        /// short of the block's edge so a WC tag in the next block stays clear of them.</summary>
        private static int[] Columns(int block)
        {
            return [block - 60, block - 46, block - 32, block - 8];
        }

        private static void DrawHeads(HudArt art, int bx, int block, int y, Color muted)
        {
            string[] heads = [K.ColW, K.ColL, K.ColOt, K.ColPts];
            int[] cols = Columns(block);
            for (int c = 0; c < heads.Length; c++)
            {
                Right(art, Text.Get(heads[c]), bx + cols[c], y, muted);
            }
        }

        private static void DrawRow(
            HudArt art,
            StandingRow r,
            int place,
            IReadOnlyDictionary<string, string> tags,
            string user,
            IReadOnlyList<ClubData> clubs,
            int bx,
            int block,
            int y,
            Inks inks
        )
        {
            bool spot = tags.TryGetValue(r.ClubId, out string tag);
            string shown = spot
                ? TagText(tag.StartsWith("WC", StringComparison.Ordinal) ? tag : N(place + 1))
                : N(place + 1);
            DrawClub(art, r, shown, spot, user, clubs, bx, block, y, inks);
        }

        /// <summary>Draws one club's line: its place or tag (dimmed outside the playoff spots), code and numbers.</summary>
        private static void DrawClub(
            HudArt art,
            StandingRow r,
            string shown,
            bool spot,
            string user,
            IReadOnlyList<ClubData> clubs,
            int bx,
            int block,
            int y,
            Inks inks
        )
        {
            Color ink = string.Equals(r.ClubId, user, StringComparison.Ordinal)
                ? inks.Gold
                : inks.Ink;
            Right(art, shown, bx + 18, y, spot ? ink : inks.Muted);
            Left(art, Short(clubs, r.ClubId), bx + 22, y, ink);
            int[] cols = Columns(block);
            string[] cells =
            [
                N(r.Wins + r.OvertimeWins),
                N(r.Losses),
                N(r.OvertimeLosses),
                N(r.Points),
            ];
            for (int c = 0; c < cells.Length; c++)
            {
                Right(art, cells[c], bx + cols[c], y, ink);
            }
        }

        /// <summary>A playoff tag as shown: a wild card's WC1 through the string table, a division place as it is.</summary>
        public static string TagText(string tag)
        {
            return tag.StartsWith("WC", StringComparison.Ordinal)
                ? Text.Format(K.WildCardTag, tag[2..])
                : tag;
        }

        /// <summary>The club's short code, or its id.</summary>
        public static string Short(IReadOnlyList<ClubData> clubs, string id)
        {
            return clubs
                .FirstOrNone(c => string.Equals(c.Id, id, StringComparison.Ordinal))
                .Select(c => c.ShortName.Length > 0 ? c.ShortName : id)
                .Or(id);
        }

        private static void Left(HudArt art, string text, int x, int y, Color ink)
        {
            art.DrawText(ScreenArt.Fit(text), x, y, ink);
        }

        private static void Right(HudArt art, string text, int right, int y, Color ink)
        {
            text = ScreenArt.Fit(text);
            art.DrawText(text, right - art.Measure(text), y, ink);
        }

        private static void Centred(HudArt art, string text, int cx, int y, Color ink)
        {
            text = ScreenArt.Fit(text);
            art.DrawText(text, cx - (art.Measure(text) / 2), y, ink);
        }

        private static string N(int n)
        {
            return n.ToString(CultureInfo.InvariantCulture);
        }
    }
}
