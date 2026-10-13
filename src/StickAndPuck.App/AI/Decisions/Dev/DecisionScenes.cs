using System.Globalization;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.AI.Dev;

/// <summary>SAP_SCENE=aimatch: a full exhibition with every skater on both sides played by the AI, under
/// the match flow with the gameplay camera. SAP_PERIOD=seconds shortens the periods.
/// SAP_IQ_HOME and SAP_IQ_AWAY (0 to 100) set every skater's smart passes trait for a club,
/// to see a smarter club pass better; a line at the top shows each club's completed passes, shots
/// and checks. SAP_AI_HUMAN=1 puts the keyboard on the home centre, like SAP_SCENE=match.</summary>
public sealed class AiMatchScene : IScene
{
    /// <inheritdoc/>
    public string Name => "aimatch";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        float period = float.TryParse(Env.Get("SAP_PERIOD"), NumberStyles.Float, CultureInfo.InvariantCulture, out float s) && s > 0 ? s : 20 * 60;
        var world = new World(Scenes.DefaultSetup(clubs) with { PeriodSeconds = period }, settings);
        Scenes.AddStarters(world);
        DecisionScenes.SetHockeyIq(world, Side.Home, Iq("SAP_IQ_HOME"));
        DecisionScenes.SetHockeyIq(world, Side.Away, Iq("SAP_IQ_AWAY"));
        if (Env.Is("SAP_AI_HUMAN", "1")
            && world.OnIce(Side.Home).FirstOrDefault(p => p.Data.Position == PlayingPosition.C) is { } centre)
        {
            centre.Slot = 0;
        }

        MatchFlow.Begin(world);
        world.Switches.DecisionLine = true;
        return world;
    }

    static Maybe<int> Iq(string name)
    {
        return Env.Int(name).TryGet(out int v) ? Math.Clamp(v, 0, 100) : Maybe<int>.None;
    }
}

/// <summary>Helpers shared by the decision scenes and the self-test.</summary>
internal static class DecisionScenes
{

    /// <summary>Gives every skater of a club the same hockey IQ.</summary>
    public static void SetHockeyIq(World world, Side side, Maybe<int> smart)
    {
        if (!smart.TryGet(out int value)) return;
        foreach (var p in world.Players.ToList())
        {
            if (p.Side != side || p.IsGoalie) continue;
            var data = p.Data with { Traits = p.Data.Traits with { HockeyIq = value } };
            world.Players[p.Id] = new Player { Id = p.Id, Side = p.Side, Data = data, Position = p.Position, Heading = p.Heading, OnIce = p.OnIce, Slot = p.Slot };
        }
    }

    /// <summary>Draws each club's passes, shots and checks.</summary>
    public static void DrawLine(World world)
    {
        if (!world.Switches.DecisionLine) return;
        var h = Decisions.Tally(world, Side.Home);
        var a = Decisions.Tally(world, Side.Away);
        string text = $"{world.Setup.Home.ShortName} PASS {h.Completed}/{h.Passes} SH {h.Shots} HIT {h.Checks}   "
            + $"{world.Setup.Away.ShortName} PASS {a.Completed}/{a.Passes} SH {a.Shots} HIT {a.Checks}";
        DrawText(text, 6, 258, 10, new Color(0xef, 0xe0, 0xc2, 255));
    }
}
