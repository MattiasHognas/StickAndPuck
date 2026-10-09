using System.Globalization;
using System.Numerics;
using StickAndPuck.Camera;
using StickAndPuck.Core;

namespace StickAndPuck.Rules;

/// <summary>
/// SAP_SCENE=match: a full exhibition between SAP_HOME and SAP_AWAY with both starting lineups,
/// the human on the home centre, the intro, three periods and the clock. SAP_PERIOD=seconds
/// shortens the periods; SAP_MATCH_DEBUG=1 shows a plain score and clock line until W10's scorebug.
/// </summary>
public sealed class MatchScene : IScene
{
    /// <inheritdoc/>
    public string Name => "match";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var world = MatchScenes.Exhibition(clubs, settings, 20 * 60);
        if (world.OnIce(Side.Home).FirstOrDefault(p => p.Data.Position == PlayingPosition.C) is { } centre) centre.Slot = 0;
        return world;
    }
}

/// <summary>
/// SAP_SCENE=matchdemo: the match flow on its own with 25-second periods (or SAP_PERIOD) and no
/// human. Whenever the puck is in play the scene fires a shot at the attacking net of each club in
/// turn, so goals, saves, frozen pucks, period ends and the final all happen in a minute or two.
/// The score and clock line is on.
/// </summary>
public sealed class MatchDemoScene : IScene
{
    /// <inheritdoc/>
    public string Name => "matchdemo";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var world = MatchScenes.Exhibition(clubs, settings, 25);
        MatchDemo.Enabled = true;
        MatchRules.ShowLine = true;
        return world;
    }
}

/// <summary>Shared setup for the match scenes.</summary>
internal static class MatchScenes
{
    /// <summary>Builds an exhibition between the default clubs with both starting lineups and begins the match flow.</summary>
    public static World Exhibition(IReadOnlyList<ClubData> clubs, Settings settings, float periodSeconds)
    {
        if (float.TryParse(Environment.GetEnvironmentVariable("SAP_PERIOD"), NumberStyles.Float, CultureInfo.InvariantCulture, out float s) && s > 0)
            periodSeconds = s;
        var setup = Scenes.DefaultSetup(clubs) with { PeriodSeconds = periodSeconds };
        var world = new World(setup, settings);
        Scenes.AddStarters(world);
        MatchFlow.Begin(world);
        return world;
    }
}

/// <summary>The shot loop and camera behind <see cref="MatchDemoScene"/>.</summary>
internal static class MatchDemo
{
    static readonly Random Dice = new(7);
    static float shotAt = float.NegativeInfinity;
    static float cameraX;
    static Side next = Side.Home;

    /// <summary>Gets or sets a value indicating whether the loop runs.</summary>
    public static bool Enabled { get; set; }

    /// <summary>Fires the next shot a moment after the puck drops or settles, and pans the camera to the puck.</summary>
    /// <param name="world">The match state.</param>
    public static void Update(World world)
    {
        if (!Enabled) return;
        Pan(world);
        var pk = world.Puck;
        if (world.Match.Phase != MatchPhase.Play) { shotAt = world.Time; return; }
        float since = world.Time - shotAt;
        if (since < 1.2f || (since < 4f && pk.Carrier < 0 && pk.Velocity.Length() > 1f)) return;
        if (pk.Carrier >= 0 && world.Players[pk.Carrier].IsGoalie) return;
        Shoot(world, next);
        next = next == Side.Home ? Side.Away : Side.Home;
        shotAt = world.Time;
    }

    static void Shoot(World world, Side shooter)
    {
        int end = -FaceoffFormation.DefendedEnd(world.Match, shooter);
        var from = new Vector3(end * (RinkSpec.GoalLineX - 6 - Dice.NextSingle() * 8), 0, (Dice.NextSingle() * 2 - 1) * 6);
        // aimed at the corners of the net, so some beat the goalie
        float aim = (0.55f + Dice.NextSingle() * 0.3f) * (Dice.Next(2) * 2 - 1);
        var target = new Vector3(end * RinkSpec.GoalLineX, 0, aim);
        var pk = world.Puck;
        pk.Carrier = -1;
        pk.LastTouch = world.OnIce(shooter).FirstOrDefault(p => !p.IsGoalie)?.Id ?? -1;
        pk.Position = from;
        pk.Velocity = Vector3.Normalize(target - from) * (28 + Dice.NextSingle() * 10);
    }

    static void Pan(World world)
    {
        float goal = Math.Clamp(world.Puck.Position.X, -17f, 17f);
        cameraX += (goal - cameraX) * 0.06f;
        float x = MathF.Round(cameraX * 4) / 4;
        var cam = BroadcastCamera.Straight();
        world.Camera = cam with { Position = cam.Position + new Vector3(x, 0, 0), Target = cam.Target + new Vector3(x, 0, 0) };
    }
}
