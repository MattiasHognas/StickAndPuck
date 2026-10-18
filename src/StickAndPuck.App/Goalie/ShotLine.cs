using System.Runtime.InteropServices;

namespace StickAndPuck.App.Goalie
{
    /// <summary>Where a shot meets a goalie: the offset across the crease from the goalie's centre, the height, and when.</summary>
    /// <param name="Offset">Distance along z from the goalie's centre to the puck.</param>
    /// <param name="Height">Height of the puck above the ice.</param>
    /// <param name="Time">Seconds until the puck gets there.</param>
    [StructLayout(LayoutKind.Auto)]
    public readonly record struct ShotLine(float Offset, float Height, float Time);
}
