using System.Globalization;
using System.Numerics;
using StickAndPuck.App.Camera;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Rules.Dev;

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
        world.Dev.Match.Set(new MatchDemo());
        world.Switches.MatchLine = true;
        return world;
    }
}

/// <summary>Shared setup for the match scenes.</summary>
internal static class MatchScenes
{
    /// <summary>Builds an exhibition between the default clubs with both starting lineups and begins the match flow.</summary>
    public static World Exhibition(IReadOnlyList<ClubData> clubs, Settings settings, float periodSeconds)
    {
        if (float.TryParse(Env.Get("SAP_PERIOD"), NumberStyles.Float, CultureInfo.InvariantCulture, out float s) && s > 0)
            periodSeconds = s;
        var setup = Scenes.DefaultSetup(clubs) with { PeriodSeconds = periodSeconds };
        var world = new World(setup, settings);
        Scenes.AddStarters(world);
        MatchFlow.Begin(world);
        return world;
    }
}

/// <summary>The shot loop and camera behind <see cref="MatchDemoScene"/>.</summary>
internal sealed class MatchDemo
{
    readonly Random dice = new(7);
    float shotAt = float.NegativeInfinity;
    float cameraX;
    Side next = Side.Home;

    /// <summary>Fires the next shot a moment after the puck drops or settles, and pans the camera to the puck.</summary>
    /// <param name="world">The match state.</param>
    public static void Update(World world)
    {
        if (world.Dev.Match.TryGet(out var demo)) demo.Run(world);
    }

    void Run(World world)
    {
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

    void Shoot(World world, Side shooter)
    {
        int end = -FaceoffFormation.DefendedEnd(world.Match, shooter);
        var from = new Vector3(end * (RinkSpec.GoalLineX - 6 - dice.NextSingle() * 8), 0, (dice.NextSingle() * 2 - 1) * 6);
        // aimed at the corners of the net, so some beat the goalie
        float aim = (0.55f + dice.NextSingle() * 0.3f) * (dice.Next(2) * 2 - 1);
        var target = new Vector3(end * RinkSpec.GoalLineX, 0, aim);
        var pk = world.Puck;
        pk.Carrier = -1;
        pk.LastTouch = world.OnIce(shooter).FirstOrDefault(p => !p.IsGoalie)?.Id ?? -1;
        pk.Position = from;
        pk.Velocity = Vector3.Normalize(target - from) * (28 + dice.NextSingle() * 10);
    }

    void Pan(World world)
    {
        float goal = Math.Clamp(world.Puck.Position.X, -17f, 17f);
        cameraX += (goal - cameraX) * 0.06f;
        float x = MathF.Round(cameraX * 4) / 4;
        var cam = BroadcastCamera.Straight();
        world.Camera = cam with { Position = cam.Position + new Vector3(x, 0, 0), Target = cam.Target + new Vector3(x, 0, 0) };
    }
}

/// <summary>
/// SAP_SCENE=celebration: the end of a decided game, to watch W40's celebration. Both clubs are AI,
/// the game starts in the last seconds of the third period (SAP_CLOCK, default 4) with the home club
/// a goal up (SAP_WINNER=away puts the away club ahead), so the horn sounds at once, the winners
/// celebrate, the losers slump, then the final and, with the screens, the summary.
/// </summary>
public sealed class CelebrationScene : IScene
{
    /// <inheritdoc/>
    public string Name => "celebration";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        float clock = float.TryParse(Env.Get("SAP_CLOCK"), NumberStyles.Float, CultureInfo.InvariantCulture, out float c) && c > 0 ? c : 4;
        var setup = Scenes.DefaultSetup(clubs) with
        {
            PeriodSeconds = 60,
            Inputs = [new InputSlot(InputDevice.None, Side.Home), new InputSlot(InputDevice.None, Side.Away)],
        };
        var world = new World(setup, settings);
        Scenes.AddStarters(world);
        MatchFlow.Begin(world);
        bool away = Env.Is("SAP_WINNER", "away");
        world.Match.Period = setup.Periods;
        world.Match.Clock = clock;
        world.Match.Score[0] = away ? 2 : 3;
        world.Match.Score[1] = away ? 3 : 2;
        return world;
    }
}
