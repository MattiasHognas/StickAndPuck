using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players.Movement;
using StickAndPuck.App.Render;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Season.Endings
{
    /// <summary>Draws the endings' people with the game's own models, clips and kit atlases, the way the
    /// skater renderer does: toon shaded in the scene pass, one flat id each in the mask pass, whole held
    /// frames at 24 per second, scaled as on the ice.</summary>
    internal sealed class FigureRenderer : IDisposable
    {
        private const float ClipFps = 24f;

        private readonly Dictionary<string, Texture2D> atlases = [with(StringComparer.Ordinal)];
        private readonly Model[] models = new Model[2];
        private readonly ModelAnimation[][] clips =
        [
            [],
            [],
        ];
        private bool loaded;

        /// <summary>Loads both models and their clips; the bench slump drops its stick, which would poke through the boards.</summary>
        public void Load()
        {
            string[] paths = [Assets.Skater, Benches.BenchStaffArt.Model];
            for (int m = 0; m < models.Length; m++)
            {
                models[m] = ResourceLoader.Model(paths[m]);
                clips[m] = ResourceLoader.Animations(paths[m]);
            }

            Unarm(Body.Skater, "slump");
            loaded = true;
        }

        private static string Prefix(Body body)
        {
            return body == Body.Skater ? "skater" : "staff";
        }

        private int Find(Body body, string clip)
        {
            return Array.FindIndex(
                clips[(int)body],
                a =>
                    string.Equals(
                        a.NameToString(),
                        $"{Prefix(body)}_{clip}",
                        StringComparison.OrdinalIgnoreCase
                    )
            );
        }

        /// <summary>Folds the stick bone into the glove in every frame of one clip.</summary>
#pragma warning disable S6640 // Raylib interop requires pointer access here.
        private unsafe void Unarm(Body body, string clip)
#pragma warning restore S6640
        {
            int c = Find(body, clip);
            ModelSkeleton skeleton = models[(int)body].Skeleton;
            int bone = -1;
            for (int i = 0; i < skeleton.BoneCount; i++)
            {
                if (
                    string.Equals(
                        skeleton.BonesAsSpan()[i].NameToString(),
                        "stick",
                        StringComparison.Ordinal
                    )
                )
                {
                    bone = i;
                }
            }

            if (c < 0 || bone < 0)
            {
                return;
            }

            ModelAnimation anim = clips[(int)body][c];
            for (int k = 0; k < anim.KeyFrameCount; k++)
            {
                anim.KeyframePoses[k][bone].Scale = new Vector3(0.001f);
            }
        }

        /// <summary>Poses the model for a figure; false when the clip is missing.</summary>
        private bool Pose(Figure f, out Model model)
        {
            model = models[(int)f.Body];
            int c = Find(f.Body, f.Clip);
            if (c < 0)
            {
                return false;
            }

            ModelAnimation anim = clips[(int)f.Body][c];
            int count = Math.Max(1, anim.KeyFrameCount - 1); // the loader's very last sample repeats the first pose of a loop
            ModelPose.Apply(model, anim, (int)(Math.Max(0f, f.Seconds) * ClipFps) % count);
            return true;
        }

        private Texture2D Atlas(string path)
        {
            if (!atlases.TryGetValue(path, out Texture2D tex))
            {
                tex = Camera.FarTexture.Load(path);
                atlases[path] = tex;
            }

            return tex;
        }

        /// <summary>Draws the figures lit, each in its own atlas.</summary>
        public void DrawScene(IEnumerable<Figure> figures, RenderContext render)
        {
            if (!loaded)
            {
                return;
            }

            foreach (Figure f in figures)
            {
                DrawOne(f, render.Toon, Color.White, Atlas(f.Atlas));
            }
        }

        /// <summary>Draws one figure lit; the caller may have set its own tones on the toon shader first.</summary>
        public void DrawScene(Figure f, RenderContext render)
        {
            if (loaded)
            {
                DrawOne(f, render.Toon, Color.White, Atlas(f.Atlas));
            }
        }

        /// <summary>Draws the figures in their mask ids.</summary>
        public void DrawMask(IEnumerable<Figure> figures, RenderContext render)
        {
            if (!loaded)
            {
                return;
            }

            foreach (Figure f in figures)
            {
                DrawOne(f, render.Mask, MaskIds.For(f.MaskId), default);
            }
        }

        private void DrawOne(Figure f, Shader shader, Color tint, Texture2D atlas)
        {
            if (!Pose(f, out Model model))
            {
                return;
            }

            for (int i = 0; i < model.MaterialCount; i++)
            {
                model.MaterialsAsSpan()[i].Shader = shader;
                if (atlas.Id != 0)
                {
                    SetMaterialTexture(
                        ref model.MaterialsAsSpan()[i],
                        MaterialMapIndex.Albedo,
                        atlas
                    );
                }
            }

            DrawModelEx(
                model,
                f.Position,
                Vector3.UnitY,
                f.Heading * 180f / MathF.PI,
                new Vector3(SkaterRenderer.Scale),
                tint
            );
        }

        public void Dispose()
        {
            if (!loaded)
            {
                return;
            }

            foreach (Texture2D t in atlases.Values)
            {
                UnloadTexture(t);
            }

            foreach (Model m in models)
            {
                UnloadModel(m);
            }

            loaded = false;
        }
    }
}
