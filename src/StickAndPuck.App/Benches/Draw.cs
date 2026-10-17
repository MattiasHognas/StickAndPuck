using System.Numerics;
using Raylib_cs;

namespace StickAndPuck.App.Benches;

internal sealed unsafe partial class BenchRenderer
{
    /// <summary>One model to draw: its baked pose, where, which way, in which kit and mask colour.</summary>
    readonly record struct Draw(int Model, int Clip, int Frame, Vector3 At, float Heading, Texture2D Atlas, Color Mask);
}
