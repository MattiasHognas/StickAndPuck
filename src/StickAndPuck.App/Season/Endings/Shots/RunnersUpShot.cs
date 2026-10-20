using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Arena;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.Endings.Shots
{
    /// <summary>Runners-up: low on the ice near the camera one of ours is down on a knee, gloves on his stick,
    /// head hung; behind him the other club piles together in its away kit round its captain and the trophy,
    /// under a little of its confetti. The home stands sit quiet; only the visitors' corner is up. The camera
    /// eases in on him.</summary>
    internal sealed class RunnersUpShot(EndingKit kit) : EndingShot(kit)
    {
        private static readonly Vector3 Pile = new(2.6f, 0f, -3.6f);

        private static readonly Vector2[] Spots =
        [
            new(-1.3f, -0.85f),
            new(1.3f, -0.9f),
            new(-2.5f, -0.6f),
            new(2.55f, -0.65f),
            new(-0.65f, -2.0f),
            new(0.7f, -2.05f),
            new(-1.95f, -1.9f),
            new(2.0f, -1.95f),
        ];

        public override void Update(World world, float time)
        {
            Crowd(world, CrowdPose.Sit, CrowdPose.Cheer);
            world.Look.IceBand = world.Look.IceBand with { A = 0 };
            world.Look.Ice = Lighting.LightPresets.DimIce;
            world.Look.Crowd = new Color(0xb0, 0xa8, 0xb8, 0xff);
        }

        public override Tones Light(float time)
        {
            return Tones.Late;
        }

        public override Camera3D Camera(float time)
        {
            float u = Ease(time / 9.5f);
            Vector3 from = new(-0.7f + (0.4f * u), 1.75f, 9.4f - (1.2f * u));
            Vector3 target = new(0.6f, 0.9f, 0f);
            return Look(from, target, 34f);
        }

        public override IReadOnlyList<Figure> Cast(float time)
        {
            IReadOnlyList<PlayerData> ours = EndingKit.Skaters(Kit.Club);
            IReadOnlyList<PlayerData> theirs = EndingKit.Skaters(Kit.Other);
            PlayerData beaten = ours[Math.Min(1, ours.Count - 1)];
            List<Figure> cast =
            [
                new Figure(
                    Body.Skater,
                    EndingKit.Kit(Kit.Club, Side.Home, beaten),
                    "kneel",
                    time,
                    new Vector3(-1.5f, 0f, 3.3f),
                    1.2f,
                    1
                ),
                new Figure(
                    Body.Skater,
                    EndingKit.Kit(Kit.Other, Side.Away, theirs[0]),
                    "trophy",
                    time + 0.2f,
                    Pile,
                    0f,
                    2
                ),
            ];
            cast.AddRange(Celebrants.Around(Kit.Other, Side.Away, theirs, Pile, Spots, time, 3));
            return cast;
        }

        public override void DrawProps(RenderContext render, float time, bool mask)
        {
            Figure captain = Cast(time)[1];
            Kit.Props.DrawTrophy(
                Kit.Other.League,
                TrophyAt(captain),
                captain.Heading,
                render,
                mask
            );
        }

        public override void DrawHud(World world, RenderContext render, float time)
        {
            Color[] colours =
            [
                EndingHud.Snap(Kit.Other.Home.Primary),
                EndingHud.Snap(Kit.Other.Home.Secondary),
            ];
            Kit.Fx.DrawConfetti(time, colours, 0.3f);
        }
    }
}
