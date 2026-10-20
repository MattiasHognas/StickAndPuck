using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Arena;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.Endings.Shots
{
    /// <summary>Semi-final: the handshake line at centre ice. Ours in the home kit glide one way, the other club in
    /// its away kit the other, gloves off, each right hand out to the man passing; the stands on their feet
    /// applauding both. The camera looks along the lines from a corner and drifts.</summary>
    internal sealed class SemiFinalShot(EndingKit kit) : EndingShot(kit)
    {
        private const int PerSide = 6;
        private const float Gap = 2.0f,
            Speed = 0.8f,
            Lane = 0.5f;

        public override void Update(World world, float time)
        {
            Crowd(world, CrowdPose.Stand, CrowdPose.Stand);
            world.Look.IceBand = world.Look.IceBand with { A = 0 };
            world.Look.Ice = Lighting.LightPresets.BrightIce;
            world.Look.Crowd = Color.White;
        }

        public override Camera3D Camera(float time)
        {
            float u = Ease(time / 9.5f);
            Vector3 target = new(0.6f - (1.2f * u), 0.8f, 0f);
            Vector3 from = new(6.5f - (1.2f * u), 4.4f, 8.2f - (0.8f * u));
            return Look(from, target, 34f);
        }

        public override IReadOnlyList<Figure> Cast(float time)
        {
            IReadOnlyList<PlayerData> ours = EndingKit.Skaters(Kit.Club);
            IReadOnlyList<PlayerData> theirs = EndingKit.Skaters(Kit.Other);
            List<Figure> cast = [];
            for (int i = 0; i < PerSide; i++)
            {
                if (i < ours.Count)
                {
                    float x = 1.0f - (i * Gap) + (Speed * time);
                    cast.Add(
                        new Figure(
                            Body.Skater,
                            EndingKit.Kit(Kit.Club, Side.Home, ours[i]),
                            "handshake",
                            time + (i * 0.13f),
                            new Vector3(x, 0f, -Lane),
                            MathF.PI / 2f,
                            i + 1
                        )
                    );
                }

                if (i < theirs.Count)
                {
                    float x = -1.0f + (i * Gap) - (Speed * time) + (Gap / 2f);
                    cast.Add(
                        new Figure(
                            Body.Skater,
                            EndingKit.Kit(Kit.Other, Side.Away, theirs[i]),
                            "handshake",
                            time + (i * 0.17f) + 0.1f,
                            new Vector3(x, 0f, Lane),
                            -MathF.PI / 2f,
                            i + 21
                        )
                    );
                }
            }

            return cast;
        }
    }
}
