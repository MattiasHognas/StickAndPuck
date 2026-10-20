using Raylib_cs;
using StickAndPuck.App.Arena;
using StickAndPuck.App.Camera;
using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Endings.Shots;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Season.Endings
{
    /// <summary>Plays an end-of-season scene (SAP_ENDING, <see cref="EndingScene"/>) through the game's own
    /// pipeline: the arena (or the shot's own set), the players' models in their kits with their clips, toon
    /// shading, outlines, palette snapping and dithering, then the title line in the game's font. It is the only
    /// system such a world runs (<see cref="Systems.For"/>): there is no match, so nothing is simulated, saved or
    /// recorded.</summary>
    internal sealed class EndingSystem : ISystem, IDisposable
    {
        private readonly ArenaSystem arena = new();
        private Maybe<EndingKit> kit;
        private Maybe<EndingShot> shot;
        private Material plain;
        private bool arenaStarted;
        private int divisor = 1;
        private float time;

        public bool RunsWhilePaused => true;

        public void Start(World world)
        {
            if (!world.Dev.Ending.TryGet(out EndingStage stage))
            {
                return;
            }

            EndingKit k = new(world, stage);
            k.Load();
            kit = k;
            EndingShot s = stage.Kind switch
            {
                EndingKind.Champions => new ChampionsShot(k),
                EndingKind.RunnersUp => new RunnersUpShot(k),
                EndingKind.SemiFinal => new SemiFinalShot(k),
                EndingKind.SeasonOver => new SeasonOverShot(k),
                EndingKind.Relegated => new RelegatedShot(k),
                EndingKind.Fired => new FiredShot(k),
                _ => new SeasonOverShot(k),
            };
            if (s.InArena)
            {
                arena.Start(world);
                _ = arena.Hidden.Add("Lanterns"); // the strings of bulbs over the stands only clutter these close shots
                arenaStarted = true;
            }

            s.Load(arena);
            shot = s;
            plain = LoadMaterialDefault();
            Update(world, 0f);
        }

        public void Update(World world, float dt)
        {
            if (!shot.TryGet(out EndingShot s))
            {
                return;
            }

            time = world.Time;
            s.Update(world, time);
            Camera3D camera = s.Camera(time);
            if (s.Divisor > 0)
            {
                divisor = s.Divisor;
                world.Camera = PixelGrid.Snap(camera, divisor);
            }
            else
            {
                (world.Camera, divisor) = PixelGrid.SnapScaled(camera, divisor);
            }

            world.App.Scale.Request(divisor);
        }

        /// <summary>Sets the toon shader's tones and light direction.</summary>
        public static void SetTones(Shader toon, Tones tones)
        {
            SetShaderValue(
                toon,
                GetShaderLocation(toon, "shadowTone"),
                tones.Shadow,
                ShaderUniformDataType.Vec3
            );
            SetShaderValue(
                toon,
                GetShaderLocation(toon, "midTone"),
                tones.Mid,
                ShaderUniformDataType.Vec3
            );
            SetShaderValue(
                toon,
                GetShaderLocation(toon, "fullTone"),
                tones.Full,
                ShaderUniformDataType.Vec3
            );
            SetShaderValue(
                toon,
                GetShaderLocation(toon, "lightDir"),
                tones.Light,
                ShaderUniformDataType.Vec3
            );
        }

        public void DrawScene(World world, RenderContext render)
        {
            if (!shot.TryGet(out EndingShot s))
            {
                return;
            }

            SetTones(render.Toon, s.Light(time));
            world.App.Dither.Apply(render.Motion.Camera(world), render.Toon, world.App.Scale);
            Shader unlit = OperatingSystem.IsBrowser() ? render.Unlit : plain.Shader;
            if (arenaStarted)
            {
                arena.DrawScene(world, render);
            }

            s.DrawSet(render, unlit, time);
            s.DrawCast(render, unlit, time);
        }

        public void DrawMask(World world, RenderContext render)
        {
            if (!shot.TryGet(out EndingShot s) || !kit.TryGet(out EndingKit k))
            {
                return;
            }

            if (arenaStarted)
            {
                arena.DrawMask(world, render); // boards and nets hide the outlines of what is behind them
            }

            k.Figures.DrawMask(s.Cast(time), render);
            s.DrawProps(render, time, mask: true);
        }

        public void DrawHud(World world, RenderContext render)
        {
            if (!shot.TryGet(out EndingShot s) || !kit.TryGet(out EndingKit k))
            {
                return;
            }

            s.DrawHud(world, render, time);
            EndingHud.DrawTitle(k.Hud, Text.Get(k.Stage.TitleKey), time);
        }

        public void Dispose()
        {
            if (shot.TryGet(out EndingShot s))
            {
                s.Dispose();
                UnloadMaterial(plain);
            }

            if (kit.TryGet(out EndingKit k))
            {
                k.Dispose();
            }

            if (arenaStarted)
            {
                arena.Dispose();
            }
        }
    }
}
