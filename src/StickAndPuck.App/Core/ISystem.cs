namespace StickAndPuck.App.Core
{
    /// <summary>One game system. Game.cs holds the fixed list of systems and calls every one of them in
    /// order: <see cref="Update"/> at a fixed 60 Hz, then the three draw passes once per frame.
    /// A system reads the whole <see cref="World"/> but changes only its own part of it.</summary>
    public interface ISystem
    {
        /// <summary>Gets a value indicating whether this system keeps updating while the match is paused.</summary>
        bool RunsWhilePaused => false;

        /// <summary>Called once after the window is open and the scene has filled the world; load GPU resources here.</summary>
        void Start(World world) { }

        /// <summary>Advances the system by one fixed step.</summary>
        void Update(World world, float dt);

        /// <summary>Draws into the lit scene, inside the 3D camera.</summary>
        void DrawScene(World world, RenderContext render) { }

        /// <summary>Draws flat player ids for the outline pass, inside the 3D camera.</summary>
        void DrawMask(World world, RenderContext render) { }

        /// <summary>Draws 2D overlays at the native 480x270, after the post pass, so they are never palette-snapped or outlined.</summary>
        void DrawHud(World world, RenderContext render) { }
    }
}
