using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Hud.Dev;
using StickAndPuck.App.Render;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Rules.Overtime;
using StickAndPuck.App.Rules.Penalties;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Hud
{
    /// <summary>Scorebug with crests and short names, clock, period and goal messages, drawn in DrawHud at
    /// 480x270 from <see cref="World.Match"/> and <see cref="World.Events"/>. Shows only in worlds
    /// that run the match flow (or when <see cref="WorldSwitches.HudAlways"/> is set), so test scenes stay clean.
    /// Adds the power play tag beside the scorebug (PP 5V4), the shootout panel under it with every
    /// round and who scored, and the extra banner lines (hat trick, shutout, game winner, first goal of the season).</summary>
    internal sealed class HudSystem : ISystem
    {
        /// <summary>The first free row under the scorebug; the penalty box goes here.</summary>
        public const int PenaltyTop = 28;

        /// <summary>The top of the centre banner, above the centre-ice faceoff.</summary>
        private const int BannerTop = 88;

        /// <summary>The top of the faceoff prompt, under the faceoff.</summary>
        private const int PromptTop = 200;

        private readonly HudMessages messages = new();
        private Maybe<HudArt> art;

        public bool RunsWhilePaused => true;

        public void Start(World world)
        {
            art = HudArt.Load();
            messages.Clear();
        }

        public void Update(World world, float dt)
        {
            HudDemo.Update(world);
            messages.Update(world);
        }

        public void DrawHud(World world, RenderContext render)
        {
            if (
                !art.TryGet(out HudArt hud)
                || !(world.Switches.HudAlways || MatchFlow.IsRunning(world))
            )
            {
                return;
            }

            DrawScorebug(world, hud);
            string pp = PowerPlayText(world);
            if (pp.Length > 0)
            {
                DrawPowerPlay(world, hud, pp);
            }

            if (
                OvertimeRules.Shootout(world).TryGet(out ShootoutState so)
                && world.Match.Phase is MatchPhase.Shootout or MatchPhase.Final
            )
            {
                DrawShootout(world, hud, so);
            }

            if (world.Match.Phase == MatchPhase.Intro && !Pregame.TeamCard.Covering(world))
            {
                DrawIntro(world, hud);
            }
            else if (TimeOuts.Running(world).TryGet(out Side calling))
            {
                DrawBanner(TimeOutBanner(world, calling), hud);
            }
            else if (messages.Current.TryGet(out HudMessage m))
            {
                DrawBanner(m, hud);
            }

            if (MatchFlow.FaceoffPrompt(world))
            {
                DrawFaceoffPrompt(hud);
            }
        }

        /// <summary>The time-out banner for as long as it runs: TIME-OUT, the calling club with its crest, and the clock until play resumes.</summary>
        internal static HudMessage TimeOutBanner(World world, Side side)
        {
            ClubData club = world.Setup.Club(side);
            return new HudMessage(
                "panel_gold",
                Text.Get(K.TimeOut),
                club.Name,
                club.Id,
                float.PositiveInfinity
            )
            {
                Notes =
                [
                    Text.Format(K.TimeOutLeft, MatchFlow.ClockText(TimeOuts.SecondsLeft(world))),
                ],
            };
        }

        /// <summary>The faceoff prompt, low and centred so the dot stays clear: press shoot as the puck drops.</summary>
        private static void DrawFaceoffPrompt(HudArt art)
        {
            const int pad = 5,
                h = 17;
            string text = Text.Get(K.FaceoffPrompt);
            int w = pad + art.Measure(text) + pad;
            int x = (Game.Width - w) / 2,
                y = PromptTop;
            art.DrawPanel("panel_gold", x, y, w, h);
            art.DrawText(text, x + pad, y + ((h - 7) / 2) - 2, art.Text);
        }

        /// <summary>Gets the power play tag, "PP 5V4" or "PP 4V3" from the club with the extra skater, or empty at even strength.</summary>
        public static string PowerPlayText(World world)
        {
            if (world.Match.Phase is MatchPhase.Intro or MatchPhase.Shootout or MatchPhase.Final)
            {
                return "";
            }

            if (!PenaltyCalls.PowerPlay(world).TryGetSide(out Side pp))
            {
                return "";
            }

            Side other = pp == Side.Home ? Side.Away : Side.Home;
            return Text.Get(K.PowerPlay)
                + Text.Format(K.PowerPlayStrength, Skaters(world, pp), Skaters(world, other));
        }

        /// <summary>Skaters a club has on the ice by the rules: five less the penalties it serves, never under three.</summary>
        private static int Skaters(World world, Side side)
        {
            int full =
                world.Match.Phase == MatchPhase.Overtime ? OvertimeTuning.OvertimeSkaters : 5;
            return Math.Max(3, full - PenaltyCalls.Serving(world, side));
        }

        /// <summary>Gets the period text: 1, 2, 3, OT or SO.</summary>
        public static string PeriodText(World world)
        {
            MatchState m = world.Match;
            return (m.Phase == MatchPhase.Shootout)
                ? Text.Get(K.PeriodSo)
                : (
                    (m.Period > world.Setup.Periods)
                        ? Text.Get(K.PeriodOt)
                        : m.Period.ToString(System.Globalization.CultureInfo.InvariantCulture)
                );
        }

        /// <summary>Gets the club colour for the stripe under a short name: the kit that side wears, on the nearest palette colour.</summary>
        public static Color StripeColour(World world, Side side)
        {
            ClubData club = world.Setup.Club(side);
            Color c = HudArt.Hex(side == Side.Home ? club.Home.Primary : club.Away.Primary);
            Vector3 want = new Vector3(c.R, c.G, c.B) / 255f;
            Vector3 near = Palette.Current.MinBy(p => Vector3.DistanceSquared(p, want)) * 255f;
            return new Color(
                (int)MathF.Round(near.X, MidpointRounding.ToEven),
                (int)MathF.Round(near.Y, MidpointRounding.ToEven),
                (int)MathF.Round(near.Z, MidpointRounding.ToEven),
                255
            );
        }

        private static void DrawScorebug(World world, HudArt art)
        {
            MatchState m = world.Match;
            art.DrawScorebug();
            foreach (Side side in new[] { Side.Home, Side.Away })
            {
                string key = side == Side.Home ? "home" : "away";
                ClubData club = world.Setup.Club(side);
                Rectangle crest = art.Slot($"{key}_crest");
                art.DrawCrest(club.Id, (int)crest.X, (int)crest.Y);
                art.DrawInSlot(club.ShortName, art.Slot($"{key}_name"), art.Text);
                Rectangle stripe = art.Slot($"{key}_stripe");
                Raylib.DrawRectangle(
                    (int)stripe.X,
                    (int)stripe.Y,
                    (int)stripe.Width,
                    (int)stripe.Height,
                    StripeColour(world, side)
                );
                art.DrawInSlot(
                    m.Score[(int)side].ToString(System.Globalization.CultureInfo.InvariantCulture),
                    art.Slot($"{key}_score"),
                    art.Score
                );
            }
            art.DrawInSlot(PeriodText(world), art.Slot("period"), art.Text);
            art.DrawInSlot(MatchFlow.ClockText(m.Clock), art.Slot("clock"), art.Text);
        }

        /// <summary>The power play tag right of the scorebug: the crest of the club with the extra skater and PP 5V4.</summary>
        private static void DrawPowerPlay(World world, HudArt art, string text)
        {
            Side side = PenaltyCalls.PowerPlay(world).TryGetSide(out Side leader)
                ? leader
                : Side.Home;
            const int pad = 3,
                crest = 16,
                gap = 4;
            int h = art.BugSize.H;
            int x = art.BugPosition.X + art.BugSize.W + 3,
                y = art.BugPosition.Y;
            int w = pad + crest + gap + art.Measure(text) + pad + 1;
            art.DrawPanel("panel_gold", x, y, w, h);
            art.DrawCrest(world.Setup.Club(side).Id, x + pad, y + ((h - crest) / 2));
            int tx = x + pad + crest + gap,
                ty = y + ((h - 7) / 2) - 2;
            string pp = Text.Get(K.PowerPlay);
            art.DrawText(pp, tx, ty, art.Score);
            art.DrawText(text[pp.Length..], tx + art.Measure(pp), ty, art.Text);
        }

        /// <summary>The shootout panel under the scorebug: one row per club with its crest, short name and a mark per
        /// round (check scored, cross missed, a dot still to come), then who is shooting or who won.</summary>
        private static void DrawShootout(World world, HudArt art, ShootoutState so)
        {
            const int pad = 3,
                crest = 16,
                gap = 4,
                row = 20,
                mark = 8,
                step = 10;
            int rounds = Math.Max(so.Rounds, Math.Max(so.Taken(Side.Home), so.Taken(Side.Away)));
            int nameW = Math.Max(
                art.Measure(world.Setup.Home.ShortName),
                art.Measure(world.Setup.Away.ShortName)
            );
            string foot = Footer(world, so);
            int footH = foot.Length > 0 ? 12 : 0;
            int x = art.BugPosition.X,
                y = PenaltyTop;
            int marksX = pad + crest + gap + nameW + gap;
            int w = Math.Max(marksX + (rounds * step) + pad - 2, pad + art.Measure(foot) + pad + 1);
            int h = pad + (2 * row) + footH + pad;
            art.DrawPanel("panel", x, y, w, h);

            int ry = y + pad;
            foreach (Side side in new[] { Side.Home, Side.Away })
            {
                ClubData club = world.Setup.Club(side);
                art.DrawCrest(club.Id, x + pad, ry + ((row - crest) / 2));
                art.DrawText(
                    club.ShortName,
                    x + pad + crest + gap,
                    ry + ((row - 7) / 2) - 2,
                    art.Text
                );
                List<ShootoutShot> shots = [.. so.Shots.Where(s => s.Side == side)];
                for (int i = 0; i < rounds; i++)
                {
                    int mx = x + marksX + (i * step),
                        my = ry + ((row - mark) / 2);
                    if (i < shots.Count)
                    {
                        art.DrawIcon(shots[i].Scored ? "check" : "cross", mx, my);
                    }
                    else
                    {
                        Raylib.DrawRectangle(mx + 3, my + 3, 2, 2, StripeColour(world, side));
                    }
                }
                ry += row;
            }
            if (footH > 0)
            {
                art.DrawText(foot, x + pad, ry - 1, art.Score);
            }
        }

        /// <summary>The shootout panel's last line: the shooter up now, or the winner.</summary>
        private static string Footer(World world, ShootoutState so)
        {
            if (so.Winner.TryGetSide(out Side w))
            {
                return Text.Format(
                    K.ShootoutWin,
                    world.Setup.Club(w).ShortName,
                    so.Goals(w),
                    so.Goals(w == Side.Home ? Side.Away : Side.Home)
                );
            }

            if (so.ShooterId >= 0 && so.ShooterId < world.Players.Count)
            {
                Player p = world.Players[so.ShooterId];
                return Text.Format(
                    K.ShootoutRound,
                    so.Taken(p.Side) + 1,
                    p.Data.Number,
                    p.Data.LastName
                );
            }
            return "";
        }

        /// <summary>The opening card: both crests and club names.</summary>
        private static void DrawIntro(World world, HudArt art)
        {
            string home = world.Setup.Home.Name.ToUpperInvariant(),
                away = world.Setup.Away.Name.ToUpperInvariant();
            const int gap = 8,
                crest = 16;
            string versus = Text.Get(K.Vs);
            int vs = art.Measure(versus);
            int textW = art.Measure(home) + gap + vs + gap + art.Measure(away);
            int w = textW + (2 * (crest + gap)) + 12,
                h = crest + 12;
            int x = (Game.Width - w) / 2,
                y = BannerTop;
            art.DrawPanel("panel", x, y, w, h);
            int cx = x + 6;
            art.DrawCrest(world.Setup.Home.Id, cx, y + 6);
            cx += crest + gap;
            int ty = y + 6 + 2; // a capital is 7 px; text y is 2 px above it, so this centres caps on the crest
            art.DrawText(home, cx, ty, art.Text);
            cx += art.Measure(home) + gap;
            art.DrawText(versus, cx, ty, art.Score);
            cx += vs + gap;
            art.DrawText(away, cx, ty, art.Text);
            cx += art.Measure(away) + gap;
            art.DrawCrest(world.Setup.Away.Id, cx, y + 6);
            if (Rivals.Playing(world.Setup))
            {
                DrawRivalry(world, art, y + h + 3);
            }
        }

        /// <summary>Gets the rivalry banner's two lines for a rivalry game (W131): DERBY between SHL rivals,
        /// RIVALRY NIGHT between NHL ones, and the bad-blood note.</summary>
        internal static (string Title, string Note) RivalryText(MatchSetup setup)
        {
            return (
                Text.Get(setup.Home.League == League.Nhl ? K.RivalryGame : K.RivalryDerby),
                Text.Get(K.RivalryDerbyNote)
            );
        }

        /// <summary>The rivalry banner under the opening card: a gold panel with the derby title and the note.</summary>
        private static void DrawRivalry(World world, HudArt art, int y)
        {
            const int pad = 5,
                line = 11;
            (string title, string note) = RivalryText(world.Setup);
            int w = pad + Math.Max(art.Measure(title), art.Measure(note)) + pad,
                h = pad + line + 7 + pad + 1;
            int x = (Game.Width - w) / 2;
            art.DrawPanel("panel_gold", x, y, w, h);
            int top = y + pad - 2; // text y is 2 px above the capitals
            art.DrawText(title, x + ((w - art.Measure(title)) / 2), top, art.Text);
            art.DrawText(note, x + ((w - art.Measure(note)) / 2), top + line, art.Text);
        }

        private static void DrawBanner(HudMessage m, HudArt art)
        {
            const int pad = 6,
                crest = 16,
                gap = 6,
                line = 11;
            int lead = (m.Crest.Length > 0) ? (crest + gap) : ((m.Icon.Length > 0) ? (8 + gap) : 0);
            List<string> rows = [m.Title];
            if (m.Detail.Length > 0)
            {
                rows.Add(m.Detail);
            }

            rows.AddRange(m.Lines);
            int textW = rows.Max(r => art.Measure(r.ToUpperInvariant()));
            int textH = (rows.Count * line) - 2;
            int inner = Math.Max(textH, m.Crest.Length > 0 ? crest : 0);
            int w = pad + lead + textW + pad,
                h = pad + inner + pad;
            int x = (Game.Width - w) / 2,
                y = BannerTop;
            art.DrawPanel(m.Panel, x, y, w, h);

            int top = y + pad + ((inner - textH) / 2) - 2; // text y is 2 px above the capitals
            if (m.Crest.Length > 0)
            {
                art.DrawCrest(m.Crest, x + pad, y + pad + ((inner - crest) / 2));
            }
            else if (m.Icon.Length > 0)
            {
                art.DrawIcon(m.Icon, x + pad, top + 2);
            }

            int tx = x + pad + lead;
            bool gold = string.Equals(m.Panel, "panel_gold", StringComparison.Ordinal);
            art.DrawText(m.Title, tx, top, gold ? art.Text : art.Score);
            for (int i = 1; i < rows.Count; i++)
            {
                // the detail in cream; the notes (shutout, game winner, first of the season) in the other colour
                bool note = i >= (m.Detail.Length > 0 ? 2 : 1);
                art.DrawText(rows[i], tx, top + (i * line), note && !gold ? art.Score : art.Text);
            }
        }
    }
}
