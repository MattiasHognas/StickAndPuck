using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens
{
    /// <summary>Pause, over a match: resume, a human club's time-out at a stoppage, settings, or quit to the start screen (a season game left early is not recorded).</summary>
    internal sealed class PauseScreen(AppState app) : MenuScreen(app)
    {
        private const int PanelW = 180;

        /// <summary>The panel's top, below the HUD's banners.</summary>
        private const int Top = 128;

        /// <summary>The match under the menu, seen on the last update; the time-out rows need it.</summary>
        private Maybe<World> match;

        /// <inheritdoc/>
        public override IReadOnlyList<MenuItem> Items()
        {
            return
            [
                new MenuItem(Text.Get(K.Resume), Resume),
                .. TimeOutItems(),
                new MenuItem(Text.Get(K.Settings), () => Flow.Push(new SettingsScreen(App))),
                new MenuItem(
                    Text.Get(K.QuitToMenu),
                    () =>
                        Flow.Push(
                            new ConfirmScreen(App, Text.Get(K.LeaveMatch), () => Flow.ToMenus())
                        )
                ),
            ];
        }

        /// <inheritdoc/>
        public override void Update(World world, IReadOnlySet<MenuButton> press)
        {
            match = world;
            if (press.Contains(MenuButton.Pause) && !press.Contains(MenuButton.Back))
            {
                Resume(); // P or start resumes; escape is back, which resumes too
            }
            else
            {
                base.Update(world, press);
            }
        }

        /// <inheritdoc/>
        protected override void Back()
        {
            Resume();
        }

        /// <inheritdoc/>
        public override void Draw(ScreenArt art, World world)
        {
            IReadOnlyList<MenuItem> items = Items();
            (int x, int top) = art.Panel(
                Text.Get(K.Paused),
                PanelW,
                (items.Count * ScreenArt.Row) + 30,
                Top
            );
            art.Menu(items, Selected, x, PanelW, top);
        }

        /// <summary>A row per human club that may call its time-out now (with the club's short name when both clubs are human).</summary>
        private List<MenuItem> TimeOutItems()
        {
            if (!match.TryGet(out World world))
            {
                return [];
            }

            List<Side> humans =
            [
                .. new[] { Side.Home, Side.Away }.Where(side =>
                    world.Players.Exists(p => p.Side == side && p.Slot >= 0)
                ),
            ];
            return
            [
                .. humans
                    .Where(side => TimeOuts.CanCall(world, side))
                    .Select(side => new MenuItem(
                        humans.Count > 1
                            ? Text.Format(K.CallTimeOut, world.Setup.Club(side).ShortName)
                            : Text.Get(K.TimeOut),
                        () =>
                        {
                            _ = TimeOuts.Call(world, side);
                            Resume();
                        }
                    )),
            ];
        }

        private void Resume()
        {
            Flow.Clear();
        }
    }
}
