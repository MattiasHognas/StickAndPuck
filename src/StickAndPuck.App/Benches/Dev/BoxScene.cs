using System.Numerics;
using System.Runtime.CompilerServices;
using StickAndPuck.App.Camera;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules;
using StickAndPuck.App.Rules.Dev;

namespace StickAndPuck.App.Benches.Dev;

/// <summary>SAP_SCENE=penaltybox: an all-AI exhibition with the camera lowered on the penalty boxes, as
/// the follow camera frames them with play at the near boards. SAP_BOX picks the script: "in" (the
/// default) blows the whistle a moment in and gives the home skater farthest from his box a minor,
/// and he skates there and sits; "two" also sends a second home skater and the away skater farthest
/// from his box; "out" puts a home skater in the box in play with a few seconds left, and he comes
/// out of the door. The benches' and boxes' draw time is printed as SAP lines.</summary>
public sealed class BoxScene : IScene
{
    /// <inheritdoc/>
    public string Name => "penaltybox";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
    {
        var world = MatchScenes.Exhibition(clubs, settings with { Benches = true }, 1200);
        BoxDemo.Attach(world, Env.Get("SAP_BOX") is { Length: > 0 } mode ? mode : "in");
        return world;
    }
}

/// <summary>The script and camera behind <see cref="BoxScene"/>.</summary>
internal sealed class BoxDemo(string mode)
{
    /// <summary>When the call comes (game seconds), once the puck is in play.</summary>
    const float CallAt = 9f;

    /// <summary>The time left on the penalty of the "out" script (s).</summary>
    const float OutAfter = 4f;

    static readonly ConditionalWeakTable<World, BoxDemo> Demos = [];

    bool called;
    double drawMs, drawMax;
    int frames;

    /// <summary>Gets the box scene's script for a world, if it runs one.</summary>
    public static Maybe<BoxDemo> Of(World world) => Demos.TryGetValue(world, out var demo) ? demo : Maybe<BoxDemo>.None;

    /// <summary>Runs the box scene's script in a world.</summary>
    public static void Attach(World world, string mode) => Demos.Add(world, new BoxDemo(mode));

    /// <summary>Frames the boxes, makes the call when it is time and tallies the draw time.</summary>
    /// <param name="world">The scene's world.</param>
    /// <param name="lastDrawMs">The time the last frame's bench and box passes took (ms).</param>
    public void Run(World world, double lastDrawMs)
    {
        world.Camera = BroadcastCamera.Lowered(0, 3.5f, BroadcastCamera.FovY);
        if (lastDrawMs > 0)
        {
            drawMs += lastDrawMs;
            drawMax = Math.Max(drawMax, lastDrawMs);
            frames++;
        }
        if (frames >= 300)
        {
            int boxed = world.Players.Count(p => !p.OnIce && Penalties.InBox(world, p.Id));
            Console.WriteLine($"SAP: benches and boxes draw {drawMs / frames:0.000} ms/frame avg, {drawMax:0.000} max over {frames} frames, {boxed} in the boxes");
            (drawMs, drawMax, frames) = (0, 0, 0);
        }
        if (called || world.Match.Phase != MatchPhase.Play || world.Time < CallAt) return;
        called = true;
        Console.WriteLine($"SAP: box {mode} at tick {world.Tick}");
        var home = Farthest(world, Side.Home);
        if (string.Equals(mode, "out", StringComparison.Ordinal))
        {
            Penalties.Give(world, home[0].Id, Infraction.Roughing, Severity.Minor);
            var m = world.Match;
            int i = m.Penalties.FindIndex(p => p.PlayerId == home[0].Id);
            if (i >= 0) m.Penalties[i] = m.Penalties[i] with { SecondsLeft = OutAfter };
            return;
        }
        MatchFlow.Whistle(world, MatchFlow.NearestDot(world.Puck.Position));
        Penalties.Give(world, home[0].Id, Infraction.Roughing, Severity.Minor);
        if (!string.Equals(mode, "two", StringComparison.Ordinal)) return;
        Penalties.Give(world, home[1].Id, Infraction.Roughing, Severity.Minor);
        Penalties.Give(world, Farthest(world, Side.Away)[0].Id, Infraction.Roughing, Severity.Minor);
    }

    /// <summary>Gets a club's skaters on the ice, farthest from its box door first, for a long skate in.</summary>
    static List<Player> Farthest(World world, Side side)
    {
        var door = Penalties.BoxDoor(side);
        return [.. world.OnIce(side).Where(p => !p.IsGoalie).OrderByDescending(p => Vector3.DistanceSquared(p.Position, door))];
    }
}
