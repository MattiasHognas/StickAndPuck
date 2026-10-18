namespace StickAndPuck.App.Core
{
    /// <summary>Conversions between <see cref="Side"/> and <see cref="SideOrNone"/>.</summary>
    public static class SideOrNoneExtensions
    {
        /// <summary>Gets a side as a side-or-none.</summary>
        public static SideOrNone OrNone(this Side side)
        {
            return side == Side.Home ? SideOrNone.Home : SideOrNone.Away;
        }

        /// <summary>Gets the side, when there is one.</summary>
        public static bool TryGetSide(this SideOrNone value, out Side side)
        {
            side = value == SideOrNone.Away ? Side.Away : Side.Home;
            return value != SideOrNone.None;
        }

        /// <summary>Tells whether a side is set.</summary>
        public static bool IsSet(this SideOrNone value)
        {
            return value != SideOrNone.None;
        }

        /// <summary>Tells whether the value is the given side.</summary>
        public static bool Is(this SideOrNone value, Side side)
        {
            return value == side.OrNone();
        }
    }
}
