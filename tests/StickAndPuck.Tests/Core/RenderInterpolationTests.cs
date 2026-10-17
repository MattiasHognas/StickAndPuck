using System.Globalization;
using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Render;
using StickAndPuck.App.Replays;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests;

/// <summary>Display sampling smooths fixed steps without changing authoritative state.</summary>
public sealed class RenderInterpolationTests : CheckedTest
{
    [Test]
    public void FasterDisplaysGetIntermediatePosesWithoutChangingChecksums()
    {
        foreach (int refresh in new[] { 30, 60, 120, 144 })
        {
            var world = new WorldBuilder().Build();
            var player = world.Players[0];
            player.Position = Vector3.Zero;
            var clock = new SimClock();
            var motion = new RenderInterpolation();
            int intermediate = 0, ticks = 0;
            float previous = -1;
            for (int frame = 0; frame < refresh * 2; frame++)
            {
                int steps = clock.Steps(1f / refresh, lockstep: false, fast: false);
                for (int step = 0; step < steps; step++)
                {
                    motion.Capture(world);
                    player.Position += new Vector3(0.1f, 0, 0);
                    world.Puck.Position = player.Position;
                    ticks++;
                }
                motion.Alpha = clock.Alpha;
                ulong checksum = WorldChecksum.Of(world);
                var shown = motion.Player(world, player).Position;
                Check(shown.X >= previous - 0.0001f && shown.X <= player.Position.X, "display motion is monotonic and never predicts ahead");
                if (shown.X > player.Position.X - 0.0999f && shown.X < player.Position.X - 0.0001f) intermediate++;
                _ = motion.Puck(world);
                _ = motion.Camera(world);
                Check(WorldChecksum.Of(world) == checksum, "sampling leaves simulation and checksums untouched");
                previous = shown.X;
            }
            Check(Math.Abs(ticks - 120) <= 1, string.Create(CultureInfo.InvariantCulture, $"{refresh} Hz rendering retains 60 Hz simulation"));
            if (refresh > 60) Check(intermediate > 0, string.Create(CultureInfo.InvariantCulture, $"{refresh} Hz rendering shows intermediate poses"));
        }
    }

    [Test]
    public void HeadingUsesShortestArcAndRelatedObjectsShareTheFraction()
    {
        var world = new WorldBuilder().Build();
        var player = world.Players[0];
        var referee = StickAndPuck.App.Referee.Referee.Of(world);
        player.Position = world.Puck.Position = referee.Position = Vector3.Zero;
        player.Heading = MathF.PI - 0.1f;
        var motion = new RenderInterpolation();
        motion.Capture(world);
        player.Position = world.Puck.Position = referee.Position = Vector3.UnitX;
        player.Heading = -MathF.PI + 0.1f;
        motion.Alpha = 0.5f;
        var pose = motion.Player(world, player);
        Check(Vector3.Distance(pose.Position, Vector3.UnitX * 0.5f) < 0.0001f, "skater uses intermediate transform");
        Check(MathF.Abs(pose.Heading - MathF.PI) < 0.0001f, "heading crosses the wrap by the short arc");
        Check(motion.Puck(world) == pose.Position && motion.Referee(referee).Position == pose.Position, "puck and referee use the same time fraction");
    }

    [Test]
    public void DiscontinuitiesAndCapturesShowCurrentState()
    {
        var world = new WorldBuilder().Build();
        var player = world.Players[0];
        player.Position = Vector3.Zero;
        var motion = new RenderInterpolation();
        motion.Capture(world);
        motion.Alpha = 0.25f;
        player.Position = Vector3.UnitX * 10;
        Check(motion.Player(world, player).Position == player.Position, "teleport snaps");
        player.Position = Vector3.UnitX;
        world.Match.Paused = true;
        Check(motion.Player(world, player).Position == player.Position, "pause holds the latest pose");
        world.Match.Paused = false;
        world.ReplayPlaying = true;
        Check(motion.Player(world, player).Position == player.Position, "goal replay transition cuts");
        world.ReplayPlaying = false;
        motion.Alpha = 1;
        Check(motion.Player(world, player).Position == player.Position, "captures and fast-forward show exact current pose");
        motion.Alpha = 0.25f;
        player.OnIce = !player.OnIce;
        Check(motion.Player(world, player).Position == player.Position, "line changes snap newly visible players");
        motion.Reset();
        Check(motion.Player(world, player).Position == player.Position, "new scene has no stale history");
    }

    [Test]
    public void CameraInterpolatesOnPixelGridAndSnapsCuts()
    {
        var world = new WorldBuilder().Build();
        world.Camera = new Camera3D { Position = new Vector3(0, 20, 20), Target = Vector3.Zero, Up = Vector3.UnitY, FovY = 45 };
        var motion = new RenderInterpolation();
        motion.Capture(world);
        var camera = world.Camera;
        camera.Position += Vector3.UnitX;
        camera.Target += Vector3.UnitX;
        world.Camera = camera;
        motion.Alpha = 0.5f;
        var shown = motion.Camera(world);
        Check(shown.Position.X > 0 && shown.Position.X < 1, "camera pans between ticks");
        Check(Vector3.Distance(shown.Target - shown.Position, camera.Target - camera.Position) < 0.0001f, "pixel snapping preserves view direction");
        camera.Position += Vector3.UnitX * 10;
        world.Camera = camera;
        Check(motion.Camera(world).Position == camera.Position, "large camera cut snaps");
    }
}
