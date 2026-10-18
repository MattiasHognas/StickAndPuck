using StickAndPuck.App.Core;
using StickAndPuck.App.Screens;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Network
{
    /// <summary>The network match's banners over the HUD: which club this machine plays for the first seconds,
    /// "waiting for the opponent" while a step waits for their input, and "press again to leave" after
    /// one press of escape. Draws only; the lockstep itself runs in <see cref="NetPlay"/>, before each step.</summary>
    internal sealed class NetworkSystem : ISystem, IDisposable
    {
        /// <summary>Seconds of match time the "you play" line shows.</summary>
        private const float IntroSeconds = 5;

        private Maybe<ScreenArt> art;

        /// <inheritdoc/>
        public bool RunsWhilePaused => true;

        /// <inheritdoc/>
        public void Start(World world)
        {
            if (world.App.Network.Owns(world))
            {
                art = ScreenArt.Load();
            }
        }

        /// <inheritdoc/>
        public void Update(World world, float dt)
        {
            // the banners draw from the network state; nothing in the match changes here
        }

        /// <inheritdoc/>
        public void DrawHud(World world, RenderContext render)
        {
            NetPlay net = world.App.Network;
            if (!art.TryGet(out ScreenArt loaded) || !net.Owns(world))
            {
                return;
            }

            const int y = Game.Height - 40;
            if (net.LeavePrompt)
            {
                Banner(loaded, Text.Get(K.NetLeaveAgain), y);
            }
            else if (net.Waiting)
            {
                Banner(loaded, Text.Get(K.NetWaiting), y);
            }
            else if (world.Time < IntroSeconds && net.Local.TryGet(out Side side))
            {
                Banner(loaded, Text.Format(K.NetOnline, world.Setup.Club(side).Name), y);
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (art.TryGet(out ScreenArt loaded))
            {
                loaded.Unload();
                art = Maybe.None<ScreenArt>();
            }
        }

        private static void Banner(ScreenArt art, string text, int y)
        {
            int w = art.Hud.Measure(ScreenArt.Fit(text)) + 24;
            art.Hud.DrawPanel("panel", (Game.Width - w) / 2, y - 4, w, ScreenArt.Row + 6);
            art.Centred(text, Game.Width / 2, y, art.Gold);
        }
    }
}
