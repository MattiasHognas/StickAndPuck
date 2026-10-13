using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules;

namespace StickAndPuck.App.Season;

/// <summary>
/// SAP_SCENE=arcade: continues the saved arcade run (or starts one) and plays the human's next game.
/// When the match ends the result is recorded and saved, and the run's panel shows; start the game
/// again for the next game. A finished run shows its summary. Until W11's start screen:
/// SAP_ARCADE=new starts a new run over the save, SAP_ARCADE_RULE=suddendeath or lives (default lives),
/// SAP_CLUB=id picks the human's club (default SAP_HOME, else the first club), SAP_SEED fixes the
/// opponent order, SAP_ARCADE_SAVE moves the save file, SAP_PERIOD and SAP_OT shorten periods and
/// overtime, SAP_HUMAN=0 lets the AI play both sides.
/// </summary>
public sealed class ArcadeScene : IScene
{
    /// <inheritdoc/>
    public string Name => "arcade";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var run = ArcadeScenes.Open(clubs);
        bool human = !Env.Is("SAP_HUMAN", "0");
        if (ArcadeSession.NextSetup(human ? InputDevice.KeyboardMouse : InputDevice.None) is not NextMatch.Play { Setup: var setup })
        {
            Console.WriteLine($"SAP: the arcade run is over ({(run.Champion ? "champions" : $"{run.Stage} club(s) beaten")}); SAP_ARCADE=new starts another");
            return ArcadeScenes.SummaryWorld(clubs, settings);
        }

        Console.WriteLine($"SAP: arcade stage {run.Stage + 1} of {run.Opponents.Count}, {run.LivesLeft} of {run.StartLives} lives: {setup.Home.Name} vs {setup.Away.Name}");
        if (SeasonScenes.Seconds("SAP_PERIOD").TryGet(out float period)) setup = setup with { PeriodSeconds = period };
        if (SeasonScenes.Seconds("SAP_OT").TryGet(out float ot)) OvertimeRules.PeriodSeconds = ot;
        var world = new World(setup, settings);
        Scenes.AddStarters(world);
        var side = setup.Inputs[0].Side;
        if (human && world.OnIce(side).FirstOrDefault(p => p.Data.Position == PlayingPosition.C) is { } centre) centre.Slot = 0;
        MatchFlow.Begin(world);
        return world;
    }
}

/// <summary>SAP_SCENE=arcadesummary: the saved arcade run's panel (or a new run's) over the rink, nothing played.</summary>
public sealed class ArcadeSummaryScene : IScene
{
    /// <inheritdoc/>
    public string Name => "arcadesummary";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        ArcadeScenes.Open(clubs);
        return ArcadeScenes.SummaryWorld(clubs, settings);
    }
}

/// <summary>Shared setup for the arcade scenes.</summary>
internal static class ArcadeScenes
{
    /// <summary>Continues the save, or starts a new run when there is none or SAP_ARCADE=new.</summary>
    public static ArcadeRun Open(IReadOnlyList<ClubData> clubs)
    {
        bool fresh = string.Equals(Env.Get("SAP_ARCADE"), "new", StringComparison.OrdinalIgnoreCase);
        if (!fresh && ArcadeSession.Continue(clubs).TryGet(out var saved))
        {
            Console.WriteLine($"SAP: arcade run continued from {ArcadeSession.SavePath}, {saved.Games.Count} game(s) played");
            return saved;
        }

        string club = Env.Get("SAP_CLUB") is { Length: > 0 } c ? c : Scenes.DefaultSetup(clubs).Home.Id;
        var rule = (Env.Get("SAP_ARCADE_RULE")) switch
        {
            "" or "lives" or "3" => ArcadeRule.ThreeLives,
            "suddendeath" or "1" => ArcadeRule.SuddenDeath,
            var other => throw new InvalidOperationException($"SAP_ARCADE_RULE={other}: use suddendeath or lives"),
        };
        var seed = int.TryParse(Env.Get("SAP_SEED"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int s) ? Maybe.Some(s) : Maybe<int>.None;
        var run = ArcadeSession.StartNew(clubs, club, rule, seed);
        Console.WriteLine($"SAP: new arcade run for {club} ({rule}), {run.Opponents.Count} clubs to beat, saved to {ArcadeSession.SavePath}");
        return run;
    }

    /// <summary>The starting lineups standing still, with the arcade panel on top.</summary>
    public static World SummaryWorld(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var world = new World(Scenes.DefaultSetup(clubs), settings);
        Scenes.AddStarters(world);
        ArcadeSystem.ShowSummary = true;
        return world;
    }
}
