using System.Collections.Immutable;
using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Core;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Render
{
    /// <summary>The ring on the ice under each skater a person controls, with a notch pointing where they
    /// face. Player one's is gold and player two's is blue, so in player vs player each person finds
    /// their own. A dark rim keeps it readable on bright ice. Shown while
    /// <see cref="Settings.ControlMarker"/> is on. Drawn into the lit scene just above the ice, so
    /// skates and sticks cover it and the post pass snaps it to the palette like the puck's shadow.</summary>
    public static class Marker
    {
        /// <summary>The ring's inner and outer radius, in metres.</summary>
        private const float Inner = 0.5f,
            Outer = 0.66f;

        /// <summary>The dark rim around the ring, in metres.</summary>
        private const float Rim = 0.07f;

        /// <summary>How far the notch reaches past the ring, and its half width in radians.</summary>
        private const float NotchLength = 0.3f,
            NotchHalfAngle = 0.32f;

        /// <summary>Height above the ice: over the painted lines and the puck shadow, under anything standing.</summary>
        private const float Lift = 0.012f;

        private const int Segments = 28;

        /// <summary>Player one's colour then player two's, from the dusk palette.</summary>
        private static readonly ImmutableArray<Color> SlotColours =
        [
            new(0xf2, 0xc1, 0x4e, 255),
            new(0x7f, 0x9c, 0xae, 255),
        ];

        /// <summary>Draws a marker under every skater a person controls. Call inside the 3D camera.</summary>
        public static void Draw(World world, RenderContext render)
        {
            if (!world.Settings.ControlMarker)
            {
                return;
            }

            for (int slot = 0; slot < MatchSetup.Slots && slot < world.Setup.Inputs.Count; slot++)
            {
                if (world.Setup.Inputs[slot].Device == InputDevice.None)
                {
                    continue;
                }

                if (!world.Controlled(slot).TryGet(out Player p) || !p.OnIce)
                {
                    continue;
                }

                (Vector3 Position, float Heading) = render.Motion.Player(world, p);
                DrawOne(Position, Heading, Colour(slot));
            }
        }

        /// <summary>Gets a slot's marker colour.</summary>
        public static Color Colour(int slot)
        {
            return SlotColours[Math.Clamp(slot, 0, SlotColours.Length - 1)];
        }

        private static void DrawOne(Vector3 at, float heading, Color colour)
        {
            Vector3 centre = at with { Y = Lift };
            Color dark = PuckLook.ShadowColour;
            Ring(centre, Inner - Rim, Outer + Rim, dark);
            Notch(
                centre,
                heading,
                Outer + Rim + NotchLength + Rim,
                NotchHalfAngle + 0.12f,
                dark,
                0.001f
            );
            Ring(centre, Inner, Outer, colour, 0.002f);
            Notch(centre, heading, Outer + NotchLength, NotchHalfAngle, colour, 0.003f);
        }

        /// <summary>A flat ring on the ice, facing up.</summary>
        private static void Ring(
            Vector3 centre,
            float inner,
            float outer,
            Color colour,
            float lift = 0
        )
        {
            Vector3 c = centre with { Y = centre.Y + lift };
            for (int i = 0; i < Segments; i++)
            {
                float a0 = MathF.Tau * i / Segments,
                    a1 = MathF.Tau * (i + 1) / Segments;
                Vector3 i0 = c + (Dir(a0) * inner);
                Vector3 i1 = c + (Dir(a1) * inner);
                Vector3 o0 = c + (Dir(a0) * outer);
                Vector3 o1 = c + (Dir(a1) * outer);
                Up(i0, o0, o1, colour);
                Up(i0, o1, i1, colour);
            }
        }

        /// <summary>A small triangle pointing the way the skater faces, from the ring outward.</summary>
        private static void Notch(
            Vector3 centre,
            float heading,
            float reach,
            float halfAngle,
            Color colour,
            float lift
        )
        {
            Vector3 c = centre with { Y = centre.Y + lift };
            Vector3 tip = c + (Dir(heading) * reach);
            Vector3 left = c + (Dir(heading - halfAngle) * Inner);
            Vector3 right = c + (Dir(heading + halfAngle) * Inner);
            Up(left, right, tip, colour);
        }

        /// <summary>A direction on the ice for a heading: 0 faces +z, the same as <see cref="Player.Heading"/>.</summary>
        private static Vector3 Dir(float angle)
        {
            return new Vector3(MathF.Sin(angle), 0, MathF.Cos(angle));
        }

        /// <summary>Draws a triangle facing up whatever order its corners come in, since back faces are culled.</summary>
        private static void Up(Vector3 a, Vector3 b, Vector3 c, Color colour)
        {
            float y = Vector3.Cross(b - a, c - a).Y;
            if (y >= 0)
            {
                DrawTriangle3D(a, b, c, colour);
            }
            else
            {
                DrawTriangle3D(a, c, b, colour);
            }
        }
    }
}
