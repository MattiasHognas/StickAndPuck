using System.Numerics;
using System.Runtime.InteropServices;

namespace StickAndPuck.App.Season.Endings
{
    /// <summary>One posed person in an ending: model, atlas, clip and the moment in it, where he stands and
    /// faces (0 faces the camera, +z), and his mask id for the outline pass.</summary>
    /// <param name="Body">The model.</param>
    /// <param name="Atlas">The 64x64 kit atlas.</param>
    /// <param name="Clip">The clip's name without the model prefix (celebrate, trophy, carry_box).</param>
    /// <param name="Seconds">How far into the clip, in seconds; loops wrap.</param>
    /// <param name="Position">On the ice or the pavement, metres.</param>
    /// <param name="Heading">Radians; 0 faces +z.</param>
    /// <param name="MaskId">1 to 255, its own outline.</param>
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct Figure(
        Body Body,
        string Atlas,
        string Clip,
        float Seconds,
        Vector3 Position,
        float Heading,
        int MaskId
    )
    {
        /// <summary>Gets a point in the figure's own frame (x its left, y up, z the way it faces) in the world.</summary>
        public Vector3 At(Vector3 local)
        {
            (float sin, float cos) = MathF.SinCos(Heading);
            return Position
                + new Vector3(
                    (local.X * cos) + (local.Z * sin),
                    local.Y,
                    (-local.X * sin) + (local.Z * cos)
                );
        }
    }
}
