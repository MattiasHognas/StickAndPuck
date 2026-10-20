using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Arena;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.Endings.Shots
{
    /// <summary>Champions: the squad gathered at centre ice in its home kit, the captain lifting the trophy
    /// (the SHL's bowl or the NHL's cup), the rest with arms and sticks up; the whole house on its feet
    /// cheering with the tifo up over the far centre stand, confetti and streamers in the club's colours and
    /// camera flashes in the stands; the camera circles slowly in from low on the near side.</summary>
    internal sealed class ChampionsShot(EndingKit kit) : EndingShot(kit)
    {
        /// <summary>Where the teammates stand around the captain (x, z), the near ones lower in the frame.</summary>
        private static readonly Vector2[] Spots =
        [
            new(-1.7f, -1.9f),
            new(1.7f, -1.95f),
            new(-3.4f, -1.6f),
            new(3.4f, -1.65f),
            new(-0.85f, -3.3f),
            new(0.9f, -3.35f),
            new(-2.6f, -3.2f),
            new(2.65f, -3.25f),
            new(-4.3f, -2.9f),
            new(4.35f, -2.95f),
        ];

        private const float CaptainZ = 2.6f;

        public override void Load(ArenaSystem arena)
        {
            arena.RaiseTifo(Kit.Club, CrowdSection.FarCentre);
        }

        public override void Update(World world, float time)
        {
            Crowd(world, CrowdPose.Cheer, CrowdPose.Sit);
            world.Crowd.Set(CrowdSection.FarLeft, CrowdPose.Wave, 0f);
            world.Crowd.Set(CrowdSection.LeftEnd, CrowdPose.Wave, 0f);
            world.Look.IceBand = world.Look.IceBand with { A = 0 };
            world.Look.Ice = Lighting.LightPresets.BrightIce;
            world.Look.Crowd = Color.White;
        }

        public override Camera3D Camera(float time)
        {
            float u = Ease(time / 9.5f);
            float angle = -0.38f + (0.66f * u);
            float distance = 10.5f - (2.2f * u);
            Vector3 target = new(0f, 1.6f, CaptainZ - 1.4f);
            Vector3 from =
                target
                + new Vector3(
                    MathF.Sin(angle) * distance,
                    0.8f + (0.3f * u),
                    MathF.Cos(angle) * distance
                );
            return Look(from, target, 36f);
        }

        public override IReadOnlyList<Figure> Cast(float time)
        {
            IReadOnlyList<PlayerData> squad = EndingKit.Skaters(Kit.Club);
            List<Figure> cast =
            [
                new Figure(
                    Body.Skater,
                    EndingKit.Kit(Kit.Club, Side.Home, squad[0]),
                    "trophy",
                    time,
                    new Vector3(0f, 0f, CaptainZ),
                    0f,
                    1
                ),
            ];
            cast.AddRange(
                Celebrants.Around(
                    Kit.Club,
                    Side.Home,
                    squad,
                    new Vector3(0f, 0f, CaptainZ),
                    Spots,
                    time,
                    2
                )
            );
            return cast;
        }

        public override void DrawProps(RenderContext render, float time, bool mask)
        {
            Figure captain = Cast(time)[0];
            Kit.Props.DrawTrophy(Kit.Club.League, TrophyAt(captain), captain.Heading, render, mask);
        }

        public override void DrawHud(World world, RenderContext render, float time)
        {
            EndingHud.DrawFlashes(time, 18, 120, 5);
            Color[] colours =
            [
                EndingHud.Snap(Kit.Club.Home.Primary),
                EndingHud.Snap(Kit.Club.Home.Secondary),
                new Color(0xf2, 0xc1, 0x4e, 0xff),
            ];
            Kit.Fx.DrawConfetti(time, colours, Math.Clamp(time / 1.2f, 0.15f, 1f));
        }
    }
}
