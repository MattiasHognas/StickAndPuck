using Raylib_cs;

namespace StickAndPuck.App.Render
{
    /// <summary>Access to material data that raylib-cs does not yet expose through spans.</summary>
    internal static class MaterialAccess
    {
        /// <summary>Gets the albedo map while the owning model or material remains loaded.</summary>
#pragma warning disable S6640 // Raylib interop requires pointer access here.
        public static unsafe ref MaterialMap Albedo(Material material)
        {
            return ref material.Maps[(int)MaterialMapIndex.Albedo];
        }
#pragma warning restore S6640
    }
}
