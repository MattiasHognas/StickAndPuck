using StickAndPuck.App.Core;
using StickAndPuck.App.Screens;
using Color = Raylib_cs.Color;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Intermission
{
    /// <summary>Draws the trailing talk over the Zamboni break: the coach's portrait (angry two or more goals down,
    /// else neutral), the question, the two answers with the picked one in a gold box and what it means, and
    /// the keys; after the answer, the coach's reply for a moment (angry for push, neutral for patience).</summary>
    internal static class TalkPanel
    {
        private const int W = 300,
            H = 92,
            Top = 34,
            PortraitW = 58,
            PortraitH = 72,
            Line = 11;

        /// <summary>Dark ink for the picked answer on its gold box.</summary>
        private static readonly Color Ink = new(0x18, 0x13, 0x1f, 0xff);

        /// <summary>Gets the coach's face for the open talk or the reply.</summary>
        public static CoachMood Mood(int deficit, Maybe<TalkChoice> said)
        {
            bool angry = said.TryGet(out TalkChoice choice)
                ? choice == TalkChoice.Push
                : deficit >= 2;
            return angry ? CoachMood.Angry : CoachMood.Neutral;
        }

        public static void Draw(World world, ScreenArt art)
        {
            if (
                world.Match.Phase != MatchPhase.Intermission
                || !world.Talk.TryGet(out TalkState s)
                || !(s.Asking.HasValue || s.Said.HasValue)
            )
            {
                return;
            }

            int x = (Game.Width - W) / 2;
            art.Hud.DrawPanel("panel", x, Top, W, H);
            string coach = Text.Get(K.RoomCoach);
            art.DrawCoach(
                x + 8,
                Top + ((H - PortraitH) / 2),
                PortraitW,
                PortraitH,
                coach,
                Mood(s.Deficit, s.Said)
            );
            int tx = x + 8 + PortraitW + 8;
            int tw = x + W - 8 - tx;
            int y = Top + 8;
            art.Left(coach, tx, y, art.Gold);
            y += Line + 3;
            if (s.Said.TryGet(out TalkChoice said))
            {
                string reply = Text.Get(
                    said == TalkChoice.Push ? K.TalkPushSaid : K.TalkPatientSaid
                );
                foreach (string line in Wrap(art, reply, tw))
                {
                    art.Left(line, tx, y, art.Text);
                    y += Line;
                }

                return;
            }

            foreach (string line in Wrap(art, Text.Format(K.TalkAsk, s.Deficit, s.NextPeriod), tw))
            {
                art.Left(line, tx, y, art.Text);
                y += Line;
            }

            y += 3;
            int bw = (tw - 6) / 2;
            Option(art, Text.Get(K.TalkPush), tx, y, bw, s.Highlight == TalkChoice.Push);
            Option(
                art,
                Text.Get(K.TalkPatient),
                tx + bw + 6,
                y,
                bw,
                s.Highlight == TalkChoice.Patient
            );
            y += 18;
            string hint = Text.Get(
                s.Highlight == TalkChoice.Push ? K.TalkPushHint : K.TalkPatientHint
            );
            art.Left(art.Clip(hint, tw), tx, y, art.Text);
            y += Line + 2;
            art.Left(art.Clip(Text.Get(K.TalkKeys), tw), tx, y, ScreenArt.Muted);
        }

        private static void Option(ScreenArt art, string label, int x, int y, int w, bool picked)
        {
            art.Hud.DrawPanel(picked ? "panel_gold" : "panel", x, y - 3, w, 15);
            art.Centred(label, x + (w / 2), y, picked ? Ink : ScreenArt.Muted);
        }

        /// <summary>Breaks text into lines that fit a width, at spaces.</summary>
        private static List<string> Wrap(ScreenArt art, string text, int width)
        {
            List<string> lines = [];
            string line = "";
            foreach (
                string word in ScreenArt.Fit(text).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            )
            {
                string longer = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && art.Hud.Measure(longer) > width)
                {
                    lines.Add(line);
                    line = word;
                }
                else
                {
                    line = longer;
                }
            }

            if (line.Length > 0)
            {
                lines.Add(line);
            }

            return lines;
        }
    }
}
