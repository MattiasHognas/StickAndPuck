using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players.Input;
using StickAndPuck.App.Players.Puck;
using StickAndPuck.App.Players.Puck.Dev;
using StickAndPuck.App.Render;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Players.Movement
{
    /// <summary>Draws every skater on the ice with skater.glb, its named clips and each club's kit atlas.
    /// Clips are baked with held poses, so playing them at 24 frames per second gives the stepped
    /// motion. One model is posed and drawn per player in turn. Tab swaps home and away kits (a dev key).</summary>
    internal sealed class SkaterRenderer : ISystem, IDisposable
    {
        /// <summary>The Blender scene draws players at 1.15.</summary>
        public const float Scale = 1.15f;
        private const float ClipFps = 24f;

        /// <summary>The shoot clip's back-swing, the last frame before the sweep: the held wind-up when skater_windup is missing.</summary>
        private const float ShootBackSwing = 5f;

        private readonly Dictionary<string, Texture2D> kits = [with(StringComparer.Ordinal)];
        private readonly Dictionary<int, (int Clip, float Frame)> poses = [];
        private Model model;
        private ModelAnimation[] clips = [];
        private bool swapKits;

        public void Start(World world)
        {
            model = ResourceLoader.Model(Assets.Skater);
            clips = ResourceLoader.Animations(Assets.Skater);
        }

        private static string ClipName(ModelAnimation a)
        {
            return a.NameToString();
        }

        private int Find(string name)
        {
            return Array.FindIndex(
                clips,
                a =>
                    string.Equals(ClipName(a), $"skater_{name}", StringComparison.OrdinalIgnoreCase)
            );
        }

        public void Update(World world, float dt)
        {
            if (KeyInput.Pressed(world.Settings, GameAction.Kit))
            {
                world.Settings = world.Settings with { SwapKits = !world.Settings.SwapKits };
            }
            swapKits = world.Settings.SwapKits; // the setting, which Tab flips in play
            if (OneTimerDemo.Of(world).TryGet(out OneTimerDemo demo))
            {
                demo.Run(world);
            }
            foreach (Player p in Skaters(world))
            {
                UpdatePose(world, p, dt);
            }
        }

        private void UpdatePose(World world, Player p, float dt)
        {
            (int clip, float frame) = poses.GetValueOrDefault(p.Id, (-1, 0f));
            if (TrySpecialPose(world, p, ref clip, ref frame, dt))
            {
                return;
            }

            int want =
                (p.Action == PlayerAction.Shoot)
                    ? Find("shoot")
                    : ((p.Speed > 0.4f) ? Find("skate") : Find("idle"));
            if (want != clip)
            {
                clip = want;
                frame = 0;
            }
            if (p.Action == PlayerAction.Shoot)
            {
                frame = p.ActionTime * ClipFps;
            }
            else
            {
                frame +=
                    dt
                    * ClipFps
                    * (
                        clip == Find("skate")
                            ? Math.Clamp(p.Speed / SkatingSystem.MaxSpeed, 0.6f, 1.4f)
                            : 1f
                    );
            }

            poses[p.Id] = (clip, frame);
        }

        private bool TrySpecialPose(World world, Player p, ref int clip, ref float frame, float dt)
        {
            return TryFightPose(world, p, ref frame)
                || TryCelebrationPose(world, p, ref frame)
                || TryFallenPose(p)
                || TryArmedPose(world, p)
                || TryFaceoffPose(world, p, ref clip, ref frame, dt)
                || TryFaceoffPose(world, p, ref clip, ref frame, dt);
        }

        private bool TryFightPose(World world, Player p, ref float frame)
        {
            if (
                p.Action == PlayerAction.Fight
                && Fights
                    .Fighting.Pose(world, p)
                    .TryGet(out (string Clip, float Seconds, bool Loop) fight)
                && Find(fight.Clip) is >= 0 and var fc
            )
            {
                // Fight clips come from the fight; loops wrap here, one-shots hold their last frame in Pose
                frame = fight.Seconds * ClipFps;
                if (fight.Loop)
                {
                    frame %= clips[fc].KeyFrameCount;
                }

                poses[p.Id] = (fc, frame);
                return true;
            }
            return false;
        }

        private bool TryCelebrationPose(World world, Player p, ref float frame)
        {
            if (
                Rules
                    .Match.Celebration.Pose(world, p)
                    .TryGet(out (string Clip, float Seconds, bool Loop) pose)
                && Find(pose.Clip) is >= 0 and var pc
            )
            {
                // The end-of-game celebration poses the winners and the losers the same way
                frame = pose.Seconds * ClipFps;
                if (pose.Loop)
                {
                    frame %= clips[pc].KeyFrameCount;
                }

                poses[p.Id] = (pc, frame);
                return true;
            }
            return false;
        }

        private bool TryFallenPose(Player p)
        {
            if (p.Action == PlayerAction.Fallen && Find("fall") is >= 0 and var down)
            {
                // A knocked-down skater plays the fall clip, which lies flat on the ice, and holds its last frame
                poses[p.Id] = (down, p.ActionTime * ClipFps);
                return true;
            }
            return false;
        }

        private bool TryArmedPose(World world, Player p)
        {
            if (
                p.Action != PlayerAction.Shoot
                && OneTimerArming.ArmedAt(world, p.Id).TryGet(out float armedAt)
                && WindUp() is >= 0 and var wind
            )
            {
                // An armed one-timer draws the stick back and up and holds it until the pass arrives; the strike goes on from the shoot clip's sweep
                float last =
                    wind == Find("windup") ? clips[wind].KeyFrameCount - 2 : ShootBackSwing; // the loader's very last sample is not the held pose
                poses[p.Id] = (wind, Math.Min((world.Time - armedAt) * ClipFps, last));
                return true;
            }
            return false;
        }

        private bool TryFaceoffPose(World world, Player p, ref int clip, ref float frame, float dt)
        {
            if (
                FaceoffPose.Clip(world, p).TryGet(out string stance)
                && Find(stance) is >= 0 and var set
            )
            {
                // Set for the draw until the puck drops: a crouch, the centres lowest over the dot; the held clip loops
                float held = set == clip ? frame + (dt * ClipFps) : 0f;
                poses[p.Id] = (set, held % (clips[set].KeyFrameCount - 1));
                return true;
            }
            return false;
        }

        /// <summary>The wind-up clip, or the shoot clip (held at its back-swing) when the model has none.</summary>
        private int WindUp()
        {
            return Find("windup") is >= 0 and var w ? w : Find("shoot");
        }

        private static IEnumerable<Player> Skaters(World world)
        {
            return world.Players.Where(p => p.OnIce && !p.IsGoalie);
        }

        private void Pose(Player p)
        {
            (int clip, float frame) = poses.GetValueOrDefault(p.Id, (-1, 0f));
            if (clip < 0)
            {
                return;
            }

            int count = clips[clip].KeyFrameCount;
            ModelPose.Apply(model, clips[clip], Math.Min((int)frame, count - 1) % count); // whole frames only: the poses are held, never blended
        }

        private Texture2D Kit(World world, Player p)
        {
            Side side = swapKits ? (p.Side == Side.Home ? Side.Away : Side.Home) : p.Side;
            string path = Assets.KitAtlas(world.Setup.Club(p.Side), side, "skater", p.Data.Number);
            if (!kits.TryGetValue(path, out Texture2D tex))
            {
                tex = Camera.FarTexture.Load(path); // mipmapped so stripes hold still at broadcast range
                kits[path] = tex;
            }
            return tex;
        }

        public void DrawScene(World world, RenderContext render)
        {
            foreach (Player p in Skaters(world))
            {
                Pose(p);
                Texture2D kit = Kit(world, p);
                for (int i = 0; i < model.MaterialCount; i++)
                {
                    model.MaterialsAsSpan()[i].Shader = render.Toon;
                    SetMaterialTexture(
                        ref model.MaterialsAsSpan()[i],
                        MaterialMapIndex.Albedo,
                        kit
                    );
                }
                (Vector3 Position, float Heading) = render.Motion.Player(world, p);
                DrawModelEx(
                    model,
                    Position,
                    Vector3.UnitY,
                    Heading * 180f / MathF.PI,
                    new Vector3(Scale),
                    Color.White
                );
            }
        }

        /// <summary>The same pose in one flat colour per player, for the outline pass.</summary>
        public void DrawMask(World world, RenderContext render)
        {
            foreach (Player p in Skaters(world))
            {
                Pose(p);
                for (int i = 0; i < model.MaterialCount; i++)
                {
                    model.MaterialsAsSpan()[i].Shader = render.Mask;
                }

                int id = p.MaskId;
                Color c = new(
                    (byte)(40 + (id * 37 % 200)),
                    (byte)(90 + (id * 59 % 160)),
                    (byte)(200 - (id * 23 % 150)),
                    (byte)255
                );
                (Vector3 Position, float Heading) = render.Motion.Player(world, p);
                DrawModelEx(
                    model,
                    Position,
                    Vector3.UnitY,
                    Heading * 180f / MathF.PI,
                    new Vector3(Scale),
                    c
                );
            }
        }

        public void Dispose()
        {
            foreach (Texture2D t in kits.Values)
            {
                UnloadTexture(t);
            }

            UnloadModel(model);
        }
    }
}
