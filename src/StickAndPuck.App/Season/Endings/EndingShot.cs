using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Arena;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.Endings
{
    /// <summary>One ending's script: where the camera is, who stands where doing what, what the arena (if any)
    /// shows, at each moment. Presentation only; time is seconds since the scene started.</summary>
    internal abstract class EndingShot(EndingKit kit) : IDisposable
    {
        protected EndingKit Kit { get; } = kit;

        /// <summary>Gets a value indicating whether the shot plays in the arena (else it draws its own set).</summary>
        public virtual bool InArena => true;

        /// <summary>Loads what only this shot needs; the arena is already started when <see cref="InArena"/>.</summary>
        public virtual void Load(ArenaSystem arena) { }

        /// <summary>Sets the crowd, the arena tones and anything else for this moment.</summary>
        public virtual void Update(World world, float time) { }

        /// <summary>The camera at a moment, before it is snapped to the pixel grid.</summary>
        public abstract Camera3D Camera(float time);

        /// <summary>The light at a moment.</summary>
        public virtual Tones Light(float time)
        {
            return Tones.Arena;
        }

        /// <summary>The people at a moment.</summary>
        public abstract IReadOnlyList<Figure> Cast(float time);

        /// <summary>Draws the shot's own set before the people (inside the 3D camera, after the arena).</summary>
        public virtual void DrawSet(RenderContext render, Shader unlit, float time) { }

        /// <summary>Draws the people and what they hold; by default every figure in the shot's light.</summary>
        public virtual void DrawCast(RenderContext render, Shader unlit, float time)
        {
            Kit.Figures.DrawScene(Cast(time), render);
            DrawProps(render, time, mask: false);
        }

        /// <summary>Draws the props, lit or in the mask pass.</summary>
        public virtual void DrawProps(RenderContext render, float time, bool mask) { }

        /// <summary>Draws the HUD's effects (the title goes on top of them).</summary>
        public virtual void DrawHud(World world, RenderContext render, float time) { }

        /// <summary>Frees what <see cref="Load"/> loaded.</summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>Frees the shot's own resources when <paramref name="disposing"/>.</summary>
        protected virtual void Dispose(bool disposing) { }

        /// <summary>Eases 0 to 1 with a smooth start and end.</summary>
        protected static float Ease(float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return t * t * (3f - (2f * t));
        }

        /// <summary>A camera looking from a point at a point.</summary>
        protected static Camera3D Look(Vector3 from, Vector3 at, float fovY)
        {
            return new Camera3D
            {
                Position = from,
                Target = at,
                Up = Vector3.UnitY,
                FovY = fovY,
                Projection = CameraProjection.Perspective,
            };
        }

        /// <summary>Sets every crowd section's pose, the visitors' corner to its own.</summary>
        protected static void Crowd(World world, CrowdPose home, CrowdPose visitors)
        {
            foreach (CrowdSection s in Enum.GetValues<CrowdSection>())
            {
                world.Crowd.Set(s, CrowdFans.Of(s) == Side.Away ? visitors : home, 0f);
            }
        }

        /// <summary>The trophy's place between the captain's hands for his clip moment (art/scripts/anim.py
        /// TROPHY_HANDS: hands 0.10 m forward, 1.88 or 2.02 m up for the two poses held ten frames each).</summary>
        protected static Vector3 TrophyAt(Figure captain)
        {
            int pose = (int)(captain.Seconds * 24f) % 20 / 10;
            float up = pose == 0 ? 1.88f : 2.02f;
            return captain.At(
                new Vector3(
                    0f,
                    up * Players.Movement.SkaterRenderer.Scale,
                    0.10f * Players.Movement.SkaterRenderer.Scale
                )
            );
        }

        /// <summary>The staff walk's bob for its clip moment (anim.py carry_walk: eight poses held three frames).</summary>
        protected static float WalkBob(float seconds)
        {
            int i = (int)(seconds * 24f) / 3 % 8;
            return (-0.045f + (0.02f * MathF.Abs(MathF.Sin(2f * MathF.PI * i / 8f))))
                * Players.Movement.SkaterRenderer.Scale;
        }
    }
}
