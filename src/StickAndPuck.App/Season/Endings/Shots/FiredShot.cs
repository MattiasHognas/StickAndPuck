using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Arena;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.Endings.Shots
{
    /// <summary>Fired: the dressing room in the club's colours (A20's backdrop on the wall behind), the coach in his
    /// suit walking across it with his things in a cardboard box, turning to the lit doorway and going out through
    /// it as the room's lights go down; the door swings shut on the light.</summary>
    internal sealed class FiredShot(EndingKit kit) : EndingShot(kit)
    {
        private static readonly Vector3 Eye = new(0f, 1.49f, 10f);
        private const float Wall = 14f;

        /// <summary>The walk: across the room, a turn to the door, out through it.</summary>
        private const float CrossEnd = 5.6f,
            TurnEnd = 6.1f,
            OutEnd = 8.6f,
            ClipRate = 1.25f;

        private static readonly Vector3 Start = new(-3.0f, 0f, 1.2f),
            Corner = new(3.0f, 0f, 1.0f),
            Door = new(3.6f, 0f, -3.45f);

        private readonly RoomWall room = new();

        public override bool InArena => false;

        public override int Divisor => 2;

        public override void Load(ArenaSystem arena)
        {
            if (Kit.Art.TryGet(out Screens.ScreenArt art))
            {
                room.Load(art, Kit.Club);
            }
        }

        public override Camera3D Camera(float time)
        {
            return Look(Eye, Eye with { Z = 0f }, 30f);
        }

        private static float RoomLight(float time)
        {
            return time < 6.4f ? 1f
                : time < 7.0f ? 0.8f
                : time < 7.6f ? 0.6f
                : 0.42f;
        }

        public override Tones Light(float time)
        {
            return Tones.Arena.Times(RoomLight(time));
        }

        public override IReadOnlyList<Figure> Cast(float time)
        {
            if (time >= OutEnd)
            {
                return [];
            }

            Vector3 at;
            float heading;
            if (time < CrossEnd)
            {
                at = Vector3.Lerp(Start, Corner, time / CrossEnd);
                heading = MathF.PI / 2f;
            }
            else if (time < TurnEnd)
            {
                at = Corner;
                heading = time < (CrossEnd + TurnEnd) / 2f ? MathF.PI * 0.75f : MathF.PI * 0.92f; // a stepped turn
            }
            else
            {
                at = Vector3.Lerp(Corner, Door, (time - TurnEnd) / (OutEnd - TurnEnd));
                heading = MathF.Atan2(Door.X - Corner.X, Door.Z - Corner.Z);
            }

            return
            [
                new Figure(
                    Body.Staff,
                    Benches.BenchStaffArt.Coach,
                    "carry_box",
                    time * ClipRate,
                    at,
                    heading,
                    1
                ),
            ];
        }

        public override void DrawSet(RenderContext render, Shader unlit, float time)
        {
            Camera3D cam = Camera(time);
            float shut = Math.Clamp((time - OutEnd - 0.1f) / 0.5f, 0f, 1f);
            float open = 1f - (MathF.Floor(shut * 3f) / 3f);
            float doorX = Door.X * Wall / (Eye.Z - Door.Z);
            room.Draw(
                cam,
                Wall,
                RoomLight(time),
                new Vector3(doorX, 0f, Eye.Z - Wall),
                open,
                unlit
            );
        }

        public override void DrawProps(RenderContext render, float time, bool mask)
        {
            IReadOnlyList<Figure> cast = Cast(time);
            if (cast.Count == 0)
            {
                return;
            }

            Figure coach = cast[0];
            float bob = WalkBob(coach.Seconds);
            Kit.Props.DrawBox(
                coach.At(new Vector3(0f, 1.12f + bob, 0.36f)),
                coach.Heading,
                render,
                mask
            );
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                room.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
