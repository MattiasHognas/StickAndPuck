using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Core;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Arena;

/// <summary>
/// The flat diagonal light bands across the ice, like the Blender render: pairs of a wide and a
/// thin band, rising to the right on screen, clipped to the rounded rink so none spill past the
/// boards. Built once, drawn on the ice plane with the depth test but no depth write, so a goalie or
/// puck drawn earlier stays in front and the markings drawn later z-fight nothing.
/// </summary>
internal static class IceBands
{
    const float Y = 0f;                   // on the ice plane; the sheet writes no depth while the bands go down
    const float Slope = -0.73f;           // x change per metre of z: about 36 degrees off the short axis
    static readonly List<Vector3[]> Polygons = [];

    /// <summary>Builds the band polygons once.</summary>
    public static void Build()
    {
        Polygons.Clear();
        var rink = RinkOutline(8);
        for (float x = -42f; x < 44f; x += 12f)
        {
            Add(rink, x, 0.9f);
            Add(rink, x + 1.6f, 0.3f);
        }
    }

    static void Add(List<Vector2> rink, float x, float width)
    {
        float h = RinkSpec.HalfWidth + 1f;
        var band = new List<Vector2>
        {
            new(x - h * Slope - width / 2, -h), new(x - h * Slope + width / 2, -h),
            new(x + h * Slope + width / 2, h), new(x + h * Slope - width / 2, h),
        };
        var clipped = Clip(band, rink);
        if (clipped.Count >= 3) Polygons.Add(clipped.Select(p => new Vector3(p.X, Y, p.Y)).ToArray());
    }

    /// <summary>The ice outline as a convex polygon, counter-clockwise in (x, z).</summary>
    static List<Vector2> RinkOutline(int segmentsPerCorner)
    {
        float r = RinkSpec.CornerRadius, cx = RinkSpec.HalfLength - r, cz = RinkSpec.HalfWidth - r;
        var pts = new List<Vector2>();
        (float X, float Z, float A0)[] corners = [(cx, cz, 0f), (-cx, cz, 90f), (-cx, -cz, 180f), (cx, -cz, 270f)];
        foreach (var (x, z, a0) in corners)
        {
            for (int i = 0; i <= segmentsPerCorner; i++)
            {
                float a = (a0 + 90f * i / segmentsPerCorner) * MathF.PI / 180f;
                pts.Add(new Vector2(x + r * MathF.Cos(a), z + r * MathF.Sin(a)));
            }
        }

        return pts;
    }

    /// <summary>Sutherland-Hodgman: a polygon clipped to a convex counter-clockwise one.</summary>
    static List<Vector2> Clip(List<Vector2> subject, List<Vector2> clip)
    {
        var output = subject;
        for (int i = 0; i < clip.Count && output.Count > 0; i++)
        {
            var a = clip[i]; var b = clip[(i + 1) % clip.Count];
            float Side(Vector2 p) => (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
            var input = output; output = [];
            for (int j = 0; j < input.Count; j++)
            {
                var p = input[j]; var q = input[(j + 1) % input.Count];
                float sp = Side(p), sq = Side(q);
                if (sp >= 0) output.Add(p);
                if (sp >= 0 != sq >= 0) output.Add(p + (q - p) * (sp / (sp - sq)));
            }
        }
        return output;
    }

    /// <summary>Draws the bands in one flat colour. The caller has turned depth writes off.</summary>
    /// <param name="tone">The band colour; alpha 0 skips them.</param>
    public static void Draw(Color tone)
    {
        if (tone.A == 0) return;
        Rlgl.DrawRenderBatchActive();
        Rlgl.DisableBackfaceCulling();
        foreach (var poly in Polygons)
        {
            for (int i = 1; i + 1 < poly.Length; i++)
                DrawTriangle3D(poly[0], poly[i], poly[i + 1], tone);
        }

        Rlgl.DrawRenderBatchActive();
        Rlgl.EnableBackfaceCulling();
    }
}
