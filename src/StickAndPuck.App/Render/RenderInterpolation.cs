using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Render;

/// <summary>Previous fixed-step transforms, sampled only by drawing. Never writes to the world.</summary>
public sealed class RenderInterpolation
{
    readonly Dictionary<Player, (Vector3 Position, float Heading, bool OnIce)> players = [];
    Maybe<World> source;
    Vector3 puck;
    Camera3D camera;
    Maybe<(Referee.Referee Actor, Vector3 Position, float Heading)> referee;
    MatchPhase phase;
    bool replay;
    int scale;

    /// <summary>Gets or sets the remainder of the simulation accumulator, from zero to one.</summary>
    public float Alpha { get; set; } = 1;

    /// <summary>Drops history on scene changes.</summary>
    public void Reset()
    {
        source = Maybe<World>.None;
        players.Clear();
        referee = Maybe<(Referee.Referee, Vector3, float)>.None;
        Alpha = 1;
    }

    /// <summary>Remembers the world immediately before each simulation step, including catch-up steps.</summary>
    public void Capture(World world)
    {
        source = world;
        players.Clear();
        foreach (var p in world.Players) players[p] = (p.Position, p.Heading, p.OnIce);
        puck = world.Puck.Position;
        camera = world.Camera;
        phase = world.Match.Phase;
        replay = world.ReplayPlaying;
        scale = world.App.Scale.Divisor;
        referee = world.Referee.TryGet(out var r) ? (r, r.Position, r.Heading) : Maybe<(Referee.Referee, Vector3, float)>.None;
    }

    bool Blending(World world) => Alpha < 1 && source.TryGet(out var captured) && ReferenceEquals(captured, world)
        && !world.Match.Paused && phase == world.Match.Phase && replay == world.ReplayPlaying
        && scale == world.App.Scale.Divisor;

    /// <summary>Samples a player's position and shortest heading arc; new players and teleports snap.</summary>
    public (Vector3 Position, float Heading) Player(World world, Player player)
    {
        if (Blending(world) && players.TryGetValue(player, out var old) && old.OnIce == player.OnIce)
            return Pose(old.Position, old.Heading, player.Position, player.Heading);
        return (player.Position, player.Heading);
    }

    /// <summary>Samples the puck and its shadow from the same display position.</summary>
    public Vector3 Puck(World world) => Blending(world) && Near(puck, world.Puck.Position)
        ? Vector3.Lerp(puck, world.Puck.Position, Alpha) : world.Puck.Position;

    /// <summary>Samples the referee without creating referee state for scenes that have none.</summary>
    public (Vector3 Position, float Heading) Referee(Referee.Referee actor)
    {
        if (source.TryGet(out var world) && Blending(world) && referee.TryGet(out var old) && ReferenceEquals(old.Actor, actor))
            return Pose(old.Position, old.Heading, actor.Position, actor.Heading);
        return (actor.Position, actor.Heading);
    }

    /// <summary>Samples camera motion, preserving pixel snapping and cutting directly across large changes.</summary>
    public Camera3D Camera(World world)
    {
        var current = world.Camera;
        if (!Blending(world) || camera.Projection != current.Projection || !Near(camera.Position, current.Position)
            || !Near(camera.Target, current.Target) || Vector3.Dot(camera.Up, current.Up) < 0.99f)
        {
            return current;
        }
        current.Position = Vector3.Lerp(camera.Position, current.Position, Alpha);
        current.Target = Vector3.Lerp(camera.Target, current.Target, Alpha);
        current.FovY = camera.FovY + ((current.FovY - camera.FovY) * Alpha);
        return App.Camera.PixelGrid.Snap(current, world.App.Scale.Divisor);
    }

    // A puck at 60 m/s travels one metre per tick. Larger jumps are resets, not motion to smear.
    static bool Near(Vector3 before, Vector3 after) => Vector3.DistanceSquared(before, after) <= 4;

    (Vector3 Position, float Heading) Pose(Vector3 before, float heading, Vector3 after, float facing)
    {
        if (!Near(before, after)) return (after, facing);
        float turn = MathF.IEEERemainder(facing - heading, MathF.Tau);
        return (Vector3.Lerp(before, after, Alpha), heading + (turn * Alpha));
    }
}
