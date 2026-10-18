using StickAndPuck.App.Core;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens
{
    /// <summary>Pause, over a match: resume, settings, or quit to the start screen (a season game left early is not recorded).</summary>
    internal sealed class PauseScreen(AppState app) : MenuScreen(app)
    {
        private const int PanelW = 180;

        /// <summary>The panel's top, below the HUD's banners.</summary>
        private const int Top = 128;

        /// <inheritdoc/>
        public override IReadOnlyList<MenuItem> Items()
        {
            return
            [
                new MenuItem(Text.Get(K.Resume), Resume),
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

        private void Resume()
        {
            Flow.Clear();
        }
    }
}
