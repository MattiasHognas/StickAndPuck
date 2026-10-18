using StickAndPuck.App.Core;

namespace StickAndPuck.App.Screens
{
    /// <summary>One screen on <see cref="ScreenFlow"/>'s stack. Only the top screen gets the presses and draws.</summary>
    /// <param name="app">The game's state the screen shows and changes.</param>
    internal abstract class Screen(AppState app)
    {
        /// <summary>Gets the game's state: the sessions, the pads.</summary>
        protected AppState App => app;

        /// <summary>Gets the screens this screen is one of.</summary>
        protected ScreenFlow Flow => app.Screens;

        /// <summary>Gets a value indicating whether the rink behind is darkened.</summary>
        public virtual bool Dims => true;

        /// <summary>Handles this step's presses.</summary>
        public abstract void Update(World world, IReadOnlySet<MenuButton> press);

        /// <summary>Draws the screen.</summary>
        public abstract void Draw(ScreenArt art, World world);
    }
}
