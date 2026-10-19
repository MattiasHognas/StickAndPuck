using StickAndPuck.App.Core;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens
{
    /// <summary>"New game": every <see cref="IGameMode"/>, exhibition first. Picking one asks for its clubs, then starts it.</summary>
    internal sealed class ModeScreen(AppState app) : MenuScreen(app)
    {
        private const int PanelW = 200;

        private readonly IReadOnlyList<IGameMode> modes = GameModes.All(app);
        private readonly bool board = ArcadeBoardScreen.Any(app);

        /// <inheritdoc/>
        public override IReadOnlyList<MenuItem> Items()
        {
            List<MenuItem> items =
            [
                .. modes.Select(m => new MenuItem(m.Title, () => Choose(App, m))),
            ];
            if (!OperatingSystem.IsBrowser())
            {
                // online versus opens its lobby rather than team select; it sits after player vs player
                int versus = modes.ToList().FindIndex(m => m is Versus.VersusMode);
                items.Insert(
                    versus < 0 ? items.Count : versus + 1,
                    new MenuItem(Text.Get(K.OnlineVersus), () => Network.NetLobby.Open(App))
                );
            }

            if (board)
            {
                items.Add(
                    new MenuItem(
                        Text.Get(K.ArcadeBestRunsItem),
                        () => Flow.Push(new ArcadeBoardScreen(App))
                    )
                );
            }

            return items;
        }

        /// <inheritdoc/>
        public override void Draw(ScreenArt art, World world)
        {
            IReadOnlyList<MenuItem> items = Items();
            (int x, int top) = art.Panel(
                Text.Get(K.NewGame),
                PanelW,
                (items.Count * ScreenArt.Row) + 30,
                70
            );
            art.Menu(items, Selected, x, PanelW, top);
            art.Hint(Text.Get(K.HintPick));
        }

        /// <summary>Asks for the mode's clubs one at a time, then starts it.</summary>
        public static void Choose(AppState app, IGameMode mode)
        {
            List<ClubData> picks = [];
            void Next()
            {
                if (picks.Count >= Math.Clamp(mode.ClubPicks, 0, 2))
                {
                    app.Screens.PlayMode(mode, picks);
                    return;
                }
                int index = picks.Count;
                string heading = mode.PickHeading(app, index);
                app.Screens.Push(
                    new TeamSelectScreen(
                        app,
                        heading,
                        index > 0 ? picks[0] : Maybe.None<ClubData>(),
                        club =>
                        {
                            picks.RemoveRange(index, picks.Count - index); // back from a later pick and pick again
                            picks.Add(club);
                            Next();
                        },
                        RulePicks.Fixed(mode)
                    )
                );
            }

            Next();
        }
    }
}
