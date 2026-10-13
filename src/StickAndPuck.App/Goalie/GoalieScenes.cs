using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Camera;
using StickAndPuck.App.Core;
using StickAndPuck.App.Physics;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Goalie;

/// <summary>
/// SAP_SCENE=goalie: the away goalie in the +x net faces a loop of shots from the slot and the
/// circles, aimed anywhere on the net, with a tally of saves, rebounds and goals. The camera sits
/// over that end. SAP_GOALIE_REFLEXES=0..100 overrides the goalie's reflexes to compare; the top line
/// shows all of the goalie's traits.
/// </summary>
public sealed class GoalieScene : IScene
{
    /// <inheritdoc/>
    public string Name => "goalie";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var world = new World(Scenes.DefaultSetup(clubs), settings);
        var data = world.Setup.Away.Roster.FirstOrDefault(p => p.Position == PlayingPosition.G) ?? new PlayerData { Position = PlayingPosition.G };
        if (int.TryParse(Environment.GetEnvironmentVariable("SAP_GOALIE_REFLEXES"), System.Globalization.CultureInfo.InvariantCulture, out int reflexes))
            data = data with { Traits = data.Traits with { GoalieReflexes = reflexes } };
        var g = world.AddPlayer(Side.Away, data, new Vector3(RinkSpec.GoalLineX - 1f, 0, 0));
        g.Heading = -MathF.PI / 2;
        // a home skater on the bench, clear of the puck, so the goals have someone to credit and play the horn
        var skater = world.Setup.Home.Roster.FirstOrDefault(p => p.Position != PlayingPosition.G) ?? new PlayerData { Position = PlayingPosition.C };
        world.AddPlayer(Side.Home, skater, Vector3.Zero).OnIce = false;
        world.Puck.Position = new Vector3(14, 0, 0);
        GoalieDemo.Enabled = true;
        return world;
    }
}

/// <summary>The shot loop and tally behind <see cref="GoalieScene"/>.</summary>
internal static class GoalieDemo
{
    static readonly Random Dice = new(11);
    static float shotAt = float.NegativeInfinity;
    static bool scored;
    static int shots, saves, rebounds, goals;

    /// <summary>Gets or sets a value indicating whether the loop runs.</summary>
    public static bool Enabled { get; set; }

    /// <summary>Starts the next shot when the last one is done, and counts goals.</summary>
    /// <param name="world">The match state.</param>
    public static void Update(World world)
    {
        if (!Enabled) return;
        var cam = BroadcastCamera.Straight();
        world.Camera = cam with { Position = cam.Position + new Vector3(17, 0, 0), Target = cam.Target + new Vector3(17, 0, 0) };
        var pk = world.Puck;
        if (!scored && PuckMotion.FullyOverGoalLine(pk.Position, 1)) { scored = true; goals++; }
        if (pk.Carrier >= 0) return;
        float since = world.Time - shotAt;
        if (since < 3.5f && (since < 1.4f || pk.Velocity.Length() > 0.8f)) return;
        Shoot(world, Dice);
        shots++;
        scored = false;
        shotAt = world.Time;
    }

    /// <summary>Puts the puck somewhere in the attacking zone and fires it at a random point on the +x net.</summary>
    /// <param name="world">The match state.</param>
    /// <param name="dice">Where the shots come from and go.</param>
    public static void Shoot(World world, Random dice)
    {
        var from = new Vector3(RinkSpec.GoalLineX - 6 - dice.NextSingle() * 10, 0, (dice.NextSingle() * 2 - 1) * 7);
        var target = new Vector3(RinkSpec.GoalLineX, 0.05f + dice.NextSingle() * 1.05f, (dice.NextSingle() * 2 - 1) * 0.85f);
        Fire(world, from, target, 18 + dice.NextSingle() * 16);
    }

    /// <summary>Fires the puck from a spot on the ice so it reaches a target point at a given speed, allowing for gravity.</summary>
    /// <param name="world">The match state.</param>
    /// <param name="from">Where the puck starts, on the ice.</param>
    /// <param name="target">Where it should arrive.</param>
    /// <param name="speed">Speed over the ice.</param>
    public static void Fire(World world, Vector3 from, Vector3 target, float speed)
    {
        var flat = new Vector3(target.X - from.X, 0, target.Z - from.Z);
        float t = flat.Length() / speed;
        float lift = target.Y < 0.06f ? 0 : (target.Y + 0.5f * PuckTuning.Gravity * t * t) / t;
        var pk = world.Puck;
        pk.Carrier = -1;
        pk.Live = true;
        pk.Position = from;
        pk.Velocity = Vector3.Normalize(flat) * speed + new Vector3(0, lift, 0);
    }

    /// <summary>Counts a save.</summary>
    /// <param name="caught">Whether the goalie held it.</param>
    public static void Saved(bool caught)
    {
        if (!Enabled) return;
        saves++;
        if (!caught) rebounds++;
    }

    /// <summary>Draws the tally in the corner.</summary>
    /// <param name="world">The match state.</param>
    public static void DrawHud(World world)
    {
        if (!Enabled) return;
        var g = world.Players.Find(p => p.IsGoalie);
        var t = g?.Data.Traits;
        string who = g == null || t == null ? "" : $"{g.Data.LastName.ToUpperInvariant()} REF {t.GoalieReflexes} POS {t.HockeyIq} GLV {t.GoalieGlove} BLK {t.GoalieBlocker} REB {t.GoalieReboundControl} PH {t.GoaliePuckHandling}";
        DrawText(who, 6, 6, 10, new Color(0xef, 0xe0, 0xc2, 255));
        DrawText($"SHOTS {shots}  SAVES {saves}  REBOUNDS {rebounds}  GOALS {goals}", 6, 18, 10, new Color(0xef, 0xe0, 0xc2, 255));
    }
}
