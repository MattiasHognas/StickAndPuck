using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Core;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Render;

/// <summary>
/// W30: the ring on the ice under each skater a person controls, with a notch pointing where they
/// face. Player one's is gold and player two's is blue, so in player vs player each person finds
/// their own. A dark rim keeps it readable on bright ice. Shown while
/// <see cref="Settings.ControlMarker"/> is on. Drawn into the lit scene just above the ice, so
/// skates and sticks cover it and the post pass snaps it to the palette like the puck's shadow.
/// </summary>
public static class Marker
{
    /// <summary>The ring's inner and outer radius, in metres.</summary>
    const float Inner = 0.5f, Outer = 0.66f;

    /// <summary>The dark rim around the ring, in metres.</summary>
    const float Rim = 0.07f;

    /// <summary>How far the notch reaches past the ring, and its half width in radians.</summary>
    const float NotchLength = 0.3f, NotchHalfAngle = 0.32f;

    /// <summary>Height above the ice: over the painted lines and the puck shadow, under anything standing.</summary>
    const float Lift = 0.012f;

    const int Segments = 28;

    /// <summary>Player one's colour then player two's, from the dusk palette.</summary>
    static readonly Color[] SlotColours = [new(0xf2, 0xc1, 0x4e, 255), new(0x7f, 0x9c, 0xae, 255)];

    /// <summary>Draws a marker under every skater a person controls. Call inside the 3D camera.</summary>
    /// <param name="world">The match state.</param>
    public static void Draw(World world)
    {
        if (!world.Settings.ControlMarker) return;
        for (int slot = 0; slot < MatchSetup.Slots && slot < world.Setup.Inputs.Count; slot++)
        {
            if (world.Setup.Inputs[slot].Device == InputDevice.None) continue;
            if (world.Controlled(slot) is not { OnIce: true } p) continue;
            DrawOne(p.Position, p.Heading, Colour(slot));
        }
    }

    /// <summary>Gets a slot's marker colour.</summary>
    /// <param name="slot">The input slot, 0 or 1.</param>
    /// <returns>The colour.</returns>
    public static Color Colour(int slot)
    {
        return SlotColours[Math.Clamp(slot, 0, SlotColours.Length - 1)];
    }

    static void DrawOne(Vector3 at, float heading, Color colour)
    {
        var centre = at with { Y = Lift };
        var dark = PuckLook.ShadowColour;
        Ring(centre, Inner - Rim, Outer + Rim, dark);
        Notch(centre, heading, Outer + Rim + NotchLength + Rim, NotchHalfAngle + 0.12f, dark, 0.001f);
        Ring(centre, Inner, Outer, colour, 0.002f);
        Notch(centre, heading, Outer + NotchLength, NotchHalfAngle, colour, 0.003f);
    }

    /// <summary>A flat ring on the ice, facing up.</summary>
    static void Ring(Vector3 centre, float inner, float outer, Color colour, float lift = 0)
    {
        var c = centre with { Y = centre.Y + lift };
        for (int i = 0; i < Segments; i++)
        {
            float a0 = MathF.Tau * i / Segments, a1 = MathF.Tau * (i + 1) / Segments;
            var i0 = c + Dir(a0) * inner;
            var i1 = c + Dir(a1) * inner;
            var o0 = c + Dir(a0) * outer;
            var o1 = c + Dir(a1) * outer;
            Up(i0, o0, o1, colour);
            Up(i0, o1, i1, colour);
        }
    }

    /// <summary>A small triangle pointing the way the skater faces, from the ring outward.</summary>
    static void Notch(Vector3 centre, float heading, float reach, float halfAngle, Color colour, float lift)
    {
        var c = centre with { Y = centre.Y + lift };
        var tip = c + Dir(heading) * reach;
        var left = c + Dir(heading - halfAngle) * Inner;
        var right = c + Dir(heading + halfAngle) * Inner;
        Up(left, right, tip, colour);
    }

    /// <summary>A direction on the ice for a heading: 0 faces +z, the same as <see cref="Player.Heading"/>.</summary>
    static Vector3 Dir(float angle)
    {
        return new Vector3(MathF.Sin(angle), 0, MathF.Cos(angle));
    }

    /// <summary>Draws a triangle facing up whatever order its corners come in, since back faces are culled.</summary>
    static void Up(Vector3 a, Vector3 b, Vector3 c, Color colour)
    {
        float y = Vector3.Cross(b - a, c - a).Y;
        if (y >= 0) DrawTriangle3D(a, b, c, colour);
        else DrawTriangle3D(a, c, b, colour);
    }
}
