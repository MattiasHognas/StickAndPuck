using StickAndPuck.App.Core;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens
{
    /// <summary>A yes or no question; yes runs the action, no goes back.</summary>
    internal sealed class ConfirmScreen(AppState app, string question, Action yes) : MenuScreen(app)
    {
        /// <inheritdoc/>
        public override IReadOnlyList<MenuItem> Items()
        {
            return [new MenuItem(Text.Get(K.No), Flow.Pop), new MenuItem(Text.Get(K.Yes), yes)];
        }

        /// <inheritdoc/>
        public override void Draw(ScreenArt art, World world)
        {
            int w = Math.Max(160, art.Hud.Measure(question.ToUpperInvariant()) + 30);
            (int x, int top) = art.Panel(
                question,
                w,
                5 + ScreenArt.Row + 2 + (2 * ScreenArt.Row) + 8,
                100,
                "panel_red"
            );
            art.Menu(Items(), Selected, x, w, top);
        }
    }
}
