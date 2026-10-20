using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Render;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Season.Endings
{
    /// <summary>The things the endings' people hold, built from a few primitive meshes in flat palette colours
    /// and toon shaded like the players, each with its own outline: a generic trophy (the SHL's wide footed
    /// bowl with handles and a gilded band, the NHL's tall banded cup; neither copies a real trophy), the
    /// coach's cardboard box and the relegated player's hockey bag with two sticks strapped to it.</summary>
    internal sealed class EndingProps : IDisposable
    {
        /// <summary>The props' mask ids, clear of the figures'.</summary>
        public const int TrophyMask = 240,
            BoxMask = 241,
            BagMask = 242;

        private const float TrophyGrowShl = 1.3f,
            TrophyGrowNhl = 1.15f;

        private static readonly Color Silver = new(0xbd, 0xb9, 0xc4, 0xff);
        private static readonly Color DarkSilver = new(0x8f, 0x8a, 0x9c, 0xff);
        private static readonly Color Gold = new(0xf2, 0xc1, 0x4e, 0xff);
        private static readonly Color Cardboard = new(0xd2, 0xad, 0x7f, 0xff);
        private static readonly Color CardboardDark = new(0x9e, 0x65, 0x5c, 0xff);
        private static readonly Color Cream = new(0xef, 0xe0, 0xc2, 0xff);
        private static readonly Color Frame = new(0x2a, 0x22, 0x30, 0xff);
        private static readonly Color Bag = new(0x2a, 0x22, 0x30, 0xff);
        private static readonly Color Wood = new(0xd2, 0xad, 0x7f, 0xff);
        private static readonly Color Tape = new(0x21, 0x1b, 0x28, 0xff);

        private Mesh cube,
            cylinder,
            bowl;
        private Material material;
        private Shader own;
        private float grow = 1f;
        private bool loaded;

        /// <summary>A shape to draw: which mesh, how it is placed, its colour.</summary>
        [System.Runtime.InteropServices.StructLayout(
            System.Runtime.InteropServices.LayoutKind.Auto
        )]
        private readonly record struct Part(Mesh Mesh, Matrix4x4 Transform, Color Colour);

        public void Load()
        {
            cube = GenMeshCube(1f, 1f, 1f);
            cylinder = GenMeshCylinder(1f, 1f, 10);
            bowl = GenMeshHemiSphere(1f, 5, 12);
            material = LoadMaterialDefault();
            own = material.Shader;
            loaded = true;
        }

        /// <summary>The trophy, its origin between the hands that hold it, turned to a heading.</summary>
        public void DrawTrophy(
            League league,
            Vector3 at,
            float heading,
            RenderContext render,
            bool mask
        )
        {
            grow = league == League.Nhl ? TrophyGrowNhl : TrophyGrowShl; // a little larger than life, so it reads at 240x135
            Part[] parts = league == League.Nhl ? Cup(at, heading) : Bowl(at, heading);
            grow = 1f;
            Draw(parts, render, mask, TrophyMask);
        }

        /// <summary>The box held at the waist, its origin at its centre.</summary>
        public void DrawBox(Vector3 at, float heading, RenderContext render, bool mask)
        {
            Draw(
                [
                    Box(at, heading, new(0, 0, 0), new(0.46f, 0.30f, 0.36f), Cardboard),
                    Box(at, heading, new(0, 0.151f, 0), new(0.40f, 0.004f, 0.30f), CardboardDark), // the dark inside
                    Box(at, heading, new(0.08f, 0.22f, -0.06f), new(0.20f, 0.17f, 0.025f), Frame), // the team photo's frame
                    Cyl(at, heading, new(-0.12f, 0.12f, 0.05f), 0.035f, 0.30f, Cream, tilt: 0.35f), // a rolled tactics sheet
                ],
                render,
                mask,
                BoxMask
            );
        }

        /// <summary>The hockey bag on its strap, its origin at its centre, its length along the heading; stripe in a club colour.</summary>
        public void DrawBag(
            Vector3 at,
            float heading,
            Color stripe,
            Vector3 strapFrom,
            RenderContext render,
            bool mask
        )
        {
            Vector3 strapTo = at + Rotate(new Vector3(0.0f, 0.19f, 0.25f), heading);
            Vector3 mid = (strapFrom + strapTo) / 2f;
            Vector3 d = strapTo - strapFrom;
            Matrix4x4 strap = Raymath.MatrixMultiply(
                Raymath.MatrixMultiply(
                    Raymath.MatrixScale(0.04f, d.Length(), 0.04f),
                    Raymath.MatrixRotate(
                        Vector3.Normalize(
                            Vector3.Cross(Vector3.UnitY, d) + new Vector3(0, 0, 1e-4f)
                        ),
                        MathF.Acos(
                            Math.Clamp(Vector3.Dot(Vector3.UnitY, Vector3.Normalize(d)), -1f, 1f)
                        )
                    )
                ),
                Raymath.MatrixTranslate(mid.X, mid.Y, mid.Z)
            );
            Draw(
                [
                    Box(at, heading, new(0, 0, 0), new(0.36f, 0.38f, 0.86f), Bag),
                    Box(at, heading, new(0, 0.02f, 0), new(0.365f, 0.07f, 0.865f), stripe),
                    Box(
                        at,
                        heading,
                        new(0.20f, 0.30f, -0.30f),
                        new(0.035f, 0.035f, 1.45f),
                        Wood,
                        0.42f
                    ), // two sticks strapped down the side, poking up out of the back
                    Box(
                        at,
                        heading,
                        new(0.20f, 0.52f, -0.78f),
                        new(0.04f, 0.04f, 0.18f),
                        Tape,
                        0.42f
                    ),
                    Box(
                        at,
                        heading,
                        new(0.15f, 0.33f, -0.25f),
                        new(0.035f, 0.035f, 1.45f),
                        Tape,
                        0.36f
                    ),
                    new Part(cube, strap, Tape),
                ],
                render,
                mask,
                BagMask
            );
        }

        /// <summary>The SHL bowl, held up by its handles: the hands at its origin, the bowl over them.</summary>
        private Part[] Bowl(Vector3 at, float heading)
        {
            return
            [
                Dome(at, heading, new(0, 0.30f, 0), 0.27f, 0.24f, Silver),
                Cyl(at, heading, new(0, 0.275f, 0), 0.275f, 0.045f, Gold),
                Cyl(at, heading, new(0, 0.302f, 0), 0.262f, 0.012f, Silver),
                Cyl(at, heading, new(0, 0f, 0), 0.045f, 0.15f, DarkSilver),
                Cyl(at, heading, new(0, -0.02f, 0), 0.07f, 0.03f, Silver),
                Cyl(at, heading, new(0, -0.11f, 0), 0.13f, 0.06f, Silver),
                Cyl(at, heading, new(0, -0.15f, 0), 0.15f, 0.025f, Gold),
                Box(at, heading, new(0.31f, 0.10f, 0), new(0.035f, 0.24f, 0.035f), Silver),
                Box(at, heading, new(0.28f, 0.205f, 0), new(0.07f, 0.03f, 0.035f), Silver),
                Box(at, heading, new(0.28f, -0.005f, 0), new(0.07f, 0.03f, 0.035f), Silver),
                Box(at, heading, new(-0.31f, 0.10f, 0), new(0.035f, 0.24f, 0.035f), Silver),
                Box(at, heading, new(-0.28f, 0.205f, 0), new(0.07f, 0.03f, 0.035f), Silver),
                Box(at, heading, new(-0.28f, -0.005f, 0), new(0.07f, 0.03f, 0.035f), Silver),
            ];
        }

        /// <summary>The NHL cup, lifted by its base: the hands at its origin, the tiers and the bowl rising over them.</summary>
        private Part[] Cup(Vector3 at, float heading)
        {
            return
            [
                Cyl(at, heading, new(0, -0.10f, 0), 0.245f, 0.20f, Silver),
                Cyl(at, heading, new(0, -0.17f, 0), 0.25f, 0.025f, DarkSilver),
                Cyl(at, heading, new(0, -0.03f, 0), 0.25f, 0.025f, DarkSilver),
                Cyl(at, heading, new(0, -0.205f, 0), 0.255f, 0.02f, Gold),
                Cyl(at, heading, new(0, 0.10f, 0), 0.21f, 0.20f, Silver),
                Cyl(at, heading, new(0, 0.10f, 0), 0.215f, 0.025f, DarkSilver),
                Cyl(at, heading, new(0, 0.36f, 0), 0.17f, 0.32f, Silver),
                Cyl(at, heading, new(0, 0.26f, 0), 0.175f, 0.03f, Gold),
                Cyl(at, heading, new(0, 0.46f, 0), 0.175f, 0.03f, Gold),
                Cyl(at, heading, new(0, 0.55f, 0), 0.11f, 0.06f, Silver),
                Cyl(at, heading, new(0, 0.63f, 0), 0.05f, 0.10f, DarkSilver),
                Dome(at, heading, new(0, 0.82f, 0), 0.15f, 0.14f, Silver),
                Cyl(at, heading, new(0, 0.81f, 0), 0.155f, 0.03f, Gold),
            ];
        }

        private static Vector3 Rotate(Vector3 local, float heading)
        {
            (float sin, float cos) = MathF.SinCos(heading);
            return new Vector3(
                (local.X * cos) + (local.Z * sin),
                local.Y,
                (-local.X * sin) + (local.Z * cos)
            );
        }

        private Matrix4x4 Place(Vector3 at, float heading, Vector3 local, Matrix4x4 shape)
        {
            Vector3 p = at + Rotate(local * grow, heading);
            return Raymath.MatrixMultiply(
                Raymath.MatrixMultiply(
                    Raymath.MatrixMultiply(shape, Raymath.MatrixScale(grow, grow, grow)),
                    Raymath.MatrixRotate(Vector3.UnitY, heading)
                ),
                Raymath.MatrixTranslate(p.X, p.Y, p.Z)
            );
        }

        /// <summary>A box centred on a point, its length tipped up toward the back by a pitch in radians.</summary>
        private Part Box(
            Vector3 at,
            float heading,
            Vector3 centre,
            Vector3 size,
            Color colour,
            float pitch = 0f
        )
        {
            return new Part(
                cube,
                Place(
                    at,
                    heading,
                    centre,
                    Raymath.MatrixMultiply(
                        Raymath.MatrixScale(size.X, size.Y, size.Z),
                        Raymath.MatrixRotateX(pitch)
                    )
                ),
                colour
            );
        }

        /// <summary>An upright cylinder centred on a point, or leaning over toward +x by a tilt in radians.</summary>
        private Part Cyl(
            Vector3 at,
            float heading,
            Vector3 centre,
            float radius,
            float height,
            Color colour,
            float tilt = 0f
        )
        {
            Matrix4x4 shape = Raymath.MatrixMultiply(
                Raymath.MatrixMultiply(
                    Raymath.MatrixTranslate(0, -0.5f, 0),
                    Raymath.MatrixScale(radius, height, radius)
                ),
                Raymath.MatrixRotateZ(-tilt)
            );
            return new Part(cylinder, Place(at, heading, centre, shape), colour);
        }

        /// <summary>A bowl: the hemisphere turned over, its rim at the point, this deep.</summary>
        private Part Dome(
            Vector3 at,
            float heading,
            Vector3 rim,
            float radius,
            float depth,
            Color colour
        )
        {
            Matrix4x4 shape = Raymath.MatrixMultiply(
                Raymath.MatrixScale(radius, depth, radius),
                Raymath.MatrixRotateX(MathF.PI)
            );
            return new Part(bowl, Place(at, heading, rim, shape), colour);
        }

        private void Draw(Part[] parts, RenderContext render, bool mask, int maskId)
        {
            if (!loaded)
            {
                return;
            }

            material.Shader = mask ? render.Mask : render.Toon;
            Rlgl.DisableBackfaceCulling(); // the bowls are open shells
            foreach (Part p in parts)
            {
                MaterialAccess.Albedo(material).Color = mask ? MaskIds.For(maskId) : p.Colour;
                DrawMesh(p.Mesh, material, p.Transform);
            }

            Rlgl.EnableBackfaceCulling();
        }

        public void Dispose()
        {
            if (!loaded)
            {
                return;
            }

            UnloadMesh(cube);
            UnloadMesh(cylinder);
            UnloadMesh(bowl);
            material.Shader = own; // the toon and mask shaders belong to the pixel frame
            UnloadMaterial(material);
            loaded = false;
        }
    }
}
