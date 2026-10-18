using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Hud;
using StickAndPuck.App.Rules.Penalties;
using static Raylib_cs.Raylib;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Momentum
{
    /// <summary>The momentum bar under the scorebug and any penalty rows: the boosted club's crest and a bar in its colour that drains with the boost.</summary>
    internal static class MomentumBar
    {
        /// <summary>Draws the bar.</summary>
        public static void Draw(World world, HudArt art, Side side, float level)
        {
            const int pad = 3,
                crest = 16,
                gap = 4,
                barH = 4;
            string label = Text.Get(K.Momentum);
            int x = art.BugPosition.X;
            // below the penalty box rows (20 px and a 1 px gap each), which share the spot under the scorebug
            int rows =
                world.Match.Penalties.Count
                + PenaltyCalls.Waiting(world).Count
                + PenaltyCalls.Misconducts(world).Count
                + PenaltyCalls.Delayed(world).Count
                + (Lines.GoaliePull.IsPulled(world, Side.Home) ? 1 : 0)
                + (Lines.GoaliePull.IsPulled(world, Side.Away) ? 1 : 0);
            int y = Math.Max(
                art.BugPosition.Y + art.BugSize.H + 3,
                HudSystem.PenaltyTop + (rows * 21) + (rows > 0 ? 2 : 0)
            );
            int w = pad + crest + gap + art.Measure(label) + pad + 1;
            int h = crest + (pad * 2);
            bool fresh =
                world.Time - MomentumMeter.State(world).Since[(int)side] < 1.5f
                && ((int)(world.Time * 6) % 2 == 0);
            art.DrawPanel(fresh ? "panel_gold" : "panel", x, y, w, h);
            art.DrawCrest(world.Setup.Club(side).Id, x + pad, y + pad);
            int tx = x + pad + crest + gap;
            art.DrawText(label, tx, y + pad - 2, fresh ? art.Text : art.Score);
            int barW = art.Measure(label);
            int by = y + h - pad - barH;
            DrawRectangle(tx, by, barW, barH, new Color(20, 18, 28, 255));
            DrawRectangle(
                tx,
                by,
                (int)MathF.Ceiling(barW * Math.Clamp(level, 0f, 1f)),
                barH,
                HudSystem.StripeColour(world, side)
            );
        }
    }
}
