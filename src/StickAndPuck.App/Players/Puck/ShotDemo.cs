using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Players;

/// <summary>
/// SAP_SCENE=shotdemo: a skater in the slot shoots at the away net over and over, a tap, then a
/// half hold, then a full wind-up, so the shot height and the airborne puck's shadow can be seen
/// and recorded. The puck is dead, so goals do not count and no replay cuts in.
/// </summary>
public sealed class ShotDemoScene : IScene
{
    /// <inheritdoc/>
    public string Name => "shotdemo";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var world = new World(Scenes.DefaultSetup(clubs), settings);
        ShotDemo.Setup(world);
        return world;
    }
}

/// <summary>The script behind <see cref="ShotDemoScene"/>, driven from <see cref="PuckHandlingSystem"/>.</summary>
internal static class ShotDemo
{
    /// <summary>Steps shoot is held in each round: a tap, a half hold, a full wind-up.</summary>
    static readonly int[] Holds = [2, 30, 66];

    static Player? shooter;
    static float start;
    static int round;

    /// <summary>Gets a value indicating whether the demo runs.</summary>
    public static bool Enabled { get; private set; }

    /// <summary>Gets the hold, in steps, of the current round.</summary>
    public static int Hold => Holds[round % Holds.Length];

    /// <summary>Adds the shooter.</summary>
    /// <param name="world">The world to fill.</param>
    public static void Setup(World world)
    {
        var data = world.Setup.Home.Roster.FirstOrDefault(p => p.Position != PlayingPosition.G) ?? new PlayerData { Position = PlayingPosition.C };
        shooter = world.AddPlayer(Side.Home, data, Vector3.Zero);
        Enabled = true;
        round = -1;
        Reset(world);
    }

    static void Reset(World world)
    {
        round++;
        var p = shooter!;
        p.Position = new Vector3(RinkSpec.GoalLineX - 10f, 0, round % 2 == 0 ? 2f : -2f);
        p.Velocity = Vector3.Zero;
        p.Heading = MathF.PI / 2;
        p.Command = default;
        p.Begin(PlayerAction.None);
        var pk = world.Puck;
        pk.Carrier = p.Id;
        pk.Position = PuckHandling.Blade(p);
        pk.Velocity = Vector3.Zero;
        pk.Live = false;   // a dead puck scores no goal, so no replay cuts in between shots
        start = world.Time;
    }

    /// <summary>Writes this step's commands.</summary>
    /// <param name="world">The match state.</param>
    public static void Update(World world)
    {
        if (!Enabled) return;
        int step = (int)MathF.Round((world.Time - start) / Game.Step);
        const int press = 30;
        shooter!.Command = new PlayerCommand { Shoot = step >= press && step < press + Hold };   // standing still, shooting the way they face
        if (step > press + Hold + 120) Reset(world);
    }
}
