namespace StickAndPuck.App.Core;

/// <summary>A side, or neither: a winner, a power play, a pending offside and the like, before one is decided.</summary>
public enum SideOrNone
{
    /// <summary>Neither side (the default).</summary>
    None,

    /// <summary>The home club.</summary>
    Home,

    /// <summary>The visiting club.</summary>
    Away,
}

/// <summary>Conversions between <see cref="Side"/> and <see cref="SideOrNone"/>.</summary>
public static class SideOrNoneExtensions
{
    /// <summary>Gets a side as a side-or-none.</summary>
    /// <param name="side">The side.</param>
    /// <returns>Home or Away.</returns>
    public static SideOrNone OrNone(this Side side) => side == Side.Home ? SideOrNone.Home : SideOrNone.Away;

    /// <summary>Gets the side, when there is one.</summary>
    /// <param name="value">The side or none.</param>
    /// <param name="side">The side, or Home when none.</param>
    /// <returns>True when a side is set.</returns>
    public static bool TryGetSide(this SideOrNone value, out Side side)
    {
        side = value == SideOrNone.Away ? Side.Away : Side.Home;
        return value != SideOrNone.None;
    }

    /// <summary>Tells whether a side is set.</summary>
    /// <param name="value">The side or none.</param>
    /// <returns>True for Home or Away.</returns>
    public static bool IsSet(this SideOrNone value) => value != SideOrNone.None;

    /// <summary>Tells whether the value is the given side.</summary>
    /// <param name="value">The side or none.</param>
    /// <param name="side">The side to test.</param>
    /// <returns>True when it is that side.</returns>
    public static bool Is(this SideOrNone value, Side side) => value == side.OrNone();
}
