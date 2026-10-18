using StickAndPuck.App.Core;
using StickAndPuck.App.Screens;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Network
{
    /// <summary>Why a network match ended early (a desync, the opponent leaving, a lost connection), over the
    /// start screen; enter or back closes it.</summary>
    internal sealed class NetMessageScreen(AppState app, string message) : MenuScreen(app)
    {
        /// <inheritdoc/>
        public override IReadOnlyList<MenuItem> Items()
        {
            return [new MenuItem(Text.Get(K.Continue), Flow.Pop)];
        }

        /// <inheritdoc/>
        public override void Draw(ScreenArt art, World world)
        {
            int w = Math.Max(180, art.Hud.Measure(message.ToUpperInvariant()) + 30);
            (int x, int top) = art.Panel(
                message,
                w,
                5 + ScreenArt.Row + 2 + ScreenArt.Row + 8,
                100,
                "panel_red"
            );
            art.Menu(Items(), Selected, x, w, top);
        }
    }
}
