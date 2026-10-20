using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Arena;
using StickAndPuck.App.Core;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Season.Endings.Shots
{
    /// <summary>Relegated: outside the arena at night in the rain (art/scene/exterior.glb). The name board over
    /// the doors has its bulbs out but for a couple of letters, one flickering. A player in his own clothes
    /// (puffer jacket in the club colour, jeans, the club beanie) comes out with his hockey bag on his shoulder,
    /// head down, and walks off past the street lamp, warmer for a moment as he passes under it. The camera
    /// starts on the board and turns to follow him.</summary>
    internal sealed class RelegatedShot(EndingKit kit) : EndingShot(kit)
    {
        private static readonly Color Sky = new(0x21, 0x1b, 0x28, 0xff);
        private static readonly Vector3 From = new(-0.2f, 0f, -3.8f),
            To = new(7.8f, 0f, 2.3f);

        private const float Walk = 9.4f,
            ClipRate = 1.35f;

        private static readonly Tones Night = new(
            new(0.30f, 0.29f, 0.44f),
            new(0.48f, 0.47f, 0.66f),
            new(0.70f, 0.68f, 0.88f),
            new(0.35f, 0.8f, 0.45f)
        );
        private static readonly Tones UnderLamp = new(
            new(0.44f, 0.36f, 0.44f),
            new(0.82f, 0.70f, 0.64f),
            new(1.15f, 1.0f, 0.80f),
            new(-0.1f, 1f, 0.25f)
        );
        private static readonly Tones NearLamp = new(
            new(0.36f, 0.32f, 0.44f),
            new(0.64f, 0.58f, 0.64f),
            new(0.92f, 0.84f, 0.84f),
            new(-0.1f, 1f, 0.25f)
        );

        private readonly ExteriorSet set = new();

        public override bool InArena => false;

        public override void Load(ArenaSystem arena)
        {
            set.Load(Kit.Club, Kit.Hud);
        }

        public override void Update(World world, float time)
        {
            set.PaintSign(Kit.Club, Kit.Hud, time);
        }

        public override Tones Light(float time)
        {
            return Night;
        }

        private static Vector3 Walker(float time)
        {
            return Vector3.Lerp(From, To, time / Walk); // and on out of the picture
        }

        public override Camera3D Camera(float time)
        {
            Vector3 p = Walker(time);
            float u = Ease((time - 0.5f) / 3.5f);
            Vector3 board = new(-3.5f, 4.4f, -8f);
            Vector3 target = Vector3.Lerp(board, p + new Vector3(0.4f, 1.5f, 0f), u);
            Vector3 wide = new(3.2f, 1.75f, 13.2f);
            Vector3 close = p + new Vector3(-1.2f, 0.9f, 6.4f); // a step behind the lamp, framing him from the knees up and the lamp over him
            return Look(Vector3.Lerp(wide, close, u), target, 34f);
        }

        public override IReadOnlyList<Figure> Cast(float time)
        {
            Vector3 d = To - From;
            float heading = MathF.Atan2(d.X, d.Z);
            return
            [
                new Figure(
                    Body.Staff,
                    EndingKit.Street(Kit.Club),
                    "carry_bag",
                    time * ClipRate,
                    Walker(time),
                    heading,
                    1
                ),
            ];
        }

        /// <summary>How lit the walker is by the lamp: 2 under it, 1 near, 0 in the dark.</summary>
        private int LampLevel(Vector3 at)
        {
            float d = Vector2.Distance(
                new Vector2(at.X, at.Z),
                new Vector2(set.Lamp.X, set.Lamp.Z)
            );
            return d < 2.0f ? 2
                : d < 3.4f ? 1
                : 0;
        }

        public override void DrawSet(RenderContext render, Shader unlit, float time)
        {
            Rlgl.DrawRenderBatchActive();
            BeginShaderMode(unlit);
            Rlgl.Begin(DrawMode.Quads);
            Rlgl.Color4ub(Sky.R, Sky.G, Sky.B, 255);
            Rlgl.Vertex3f(-120f, -2f, -70f);
            Rlgl.Vertex3f(120f, -2f, -70f);
            Rlgl.Vertex3f(120f, 80f, -70f);
            Rlgl.Vertex3f(-120f, 80f, -70f);
            Rlgl.End();
            Rlgl.DrawRenderBatchActive();
            EndShaderMode();
            set.DrawScene(render);
        }

        public override void DrawCast(RenderContext render, Shader unlit, float time)
        {
            Figure walker = Cast(time)[0];
            Tones lit = LampLevel(walker.Position) switch
            {
                2 => UnderLamp,
                1 => NearLamp,
                _ => NearLamp, // the street keeps him a little lit, so he never sinks into the dark
            };
            EndingSystem.SetTones(render.Toon, lit);
            Kit.Figures.DrawScene(walker, render);
            DrawProps(render, time, mask: false);
            EndingSystem.SetTones(render.Toon, Night);
            set.DrawLampLight(unlit);
        }

        public override void DrawProps(RenderContext render, float time, bool mask)
        {
            Figure walker = Cast(time)[0];
            float bob = WalkBob(walker.Seconds);
            Vector3 bag = walker.At(new Vector3(-0.34f, 0.95f + bob, -0.08f));
            Vector3 hand = walker.At(new Vector3(-0.2f, 1.49f + bob, 0.18f));
            Kit.Props.DrawBag(
                bag,
                walker.Heading,
                EndingHud.Snap(Kit.Club.Home.Primary),
                hand,
                render,
                mask
            );
        }

        public override void DrawHud(World world, RenderContext render, float time)
        {
            Camera3D cam = render.Motion.Camera(world);
            Vector2 lamp = GetWorldToScreenEx(set.Lamp, cam, Game.Width, Game.Height);
            Vector2 ground = GetWorldToScreenEx(
                set.Lamp with
                {
                    Y = 0f,
                },
                cam,
                Game.Width,
                Game.Height
            );
            Kit.Fx.DrawRain(time, lamp, ground, 34f);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                set.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
