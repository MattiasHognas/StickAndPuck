using System.Globalization;
using Raylib_cs;
using StickAndPuck.Core;
using StickAndPuck.Rules;
using static Raylib_cs.Raylib;

namespace StickAndPuck.AI;

/// <summary>
/// SAP_SCENE=aimatch: a full exhibition with every skater on both sides played by the AI, under
/// W7a's match flow with the gameplay camera. SAP_PERIOD=seconds shortens the periods.
/// SAP_SMART_HOME and SAP_SMART_AWAY (0 to 100) set every skater's smart passes trait for a club,
/// to see a smarter club pass better; a line at the top shows each club's completed passes, shots
/// and checks. SAP_AI_HUMAN=1 puts the keyboard on the home centre, like SAP_SCENE=match.
/// </summary>
public sealed class AiMatchScene : IScene
{
    /// <inheritdoc/>
    public string Name => "aimatch";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        float period = float.TryParse(Environment.GetEnvironmentVariable("SAP_PERIOD"), NumberStyles.Float, CultureInfo.InvariantCulture, out float s) && s > 0 ? s : 20 * 60;
        var world = new World(Scenes.DefaultSetup(clubs) with { PeriodSeconds = period }, settings);
        Scenes.AddStarters(world);
        DecisionScenes.SetSmartPasses(world, Side.Home, Env("SAP_SMART_HOME"));
        DecisionScenes.SetSmartPasses(world, Side.Away, Env("SAP_SMART_AWAY"));
        if (string.Equals(Environment.GetEnvironmentVariable("SAP_AI_HUMAN"), "1", StringComparison.Ordinal)
            && world.OnIce(Side.Home).FirstOrDefault(p => p.Data.Position == PlayingPosition.C) is { } centre)
            centre.Slot = 0;
        MatchFlow.Begin(world);
        DecisionScenes.ShowLine = true;
        return world;
    }

    static int? Env(string name)
    {
        return int.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? Math.Clamp(v, 0, 100) : null;
    }
}

/// <summary>Helpers shared by the decision scenes and the self-test.</summary>
internal static class DecisionScenes
{
    /// <summary>Gets or sets a value indicating whether the pass, shot and check line is drawn.</summary>
    public static bool ShowLine { get; set; }

    /// <summary>Gives every skater of a club the same smart passes trait.</summary>
    /// <param name="world">The match, before it starts.</param>
    /// <param name="side">The club.</param>
    /// <param name="smart">The trait, or null to leave the roster's own.</param>
    public static void SetSmartPasses(World world, Side side, int? smart)
    {
        if (smart is not { } value) return;
        foreach (var p in world.Players.ToList())
        {
            if (p.Side != side || p.IsGoalie) continue;
            var data = p.Data with { Traits = p.Data.Traits with { SmartPasses = value } };
            world.Players[p.Id] = new Player { Id = p.Id, Side = p.Side, Data = data, Position = p.Position, Heading = p.Heading, OnIce = p.OnIce, Slot = p.Slot };
        }
    }

    /// <summary>Draws each club's passes, shots and checks.</summary>
    /// <param name="world">The match.</param>
    public static void DrawLine(World world)
    {
        if (!ShowLine) return;
        var h = Decisions.Tally(world, Side.Home);
        var a = Decisions.Tally(world, Side.Away);
        string text = $"{world.Setup.Home.ShortName} PASS {h.Completed}/{h.Passes} SH {h.Shots} HIT {h.Checks}   "
            + $"{world.Setup.Away.ShortName} PASS {a.Completed}/{a.Passes} SH {a.Shots} HIT {a.Checks}";
        DrawText(text, 6, 258, 10, new Color(0xef, 0xe0, 0xc2, 255));
    }
}
