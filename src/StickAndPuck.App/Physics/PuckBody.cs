using System.Numerics;
using System.Runtime.InteropServices;

namespace StickAndPuck.App.Physics
{
    /// <summary>A body the puck can bounce off: a standing cylinder.</summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly record struct PuckBody(int PlayerId, Vector3 Position, Vector3 Velocity);
}
