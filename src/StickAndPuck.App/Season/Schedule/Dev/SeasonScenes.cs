using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules;

namespace StickAndPuck.App.Season.Dev;

/// <summary>SAP_SCENE=season: continues the saved season (or starts one) and plays the human's next game.
/// When the match ends the result is recorded, the round's other games are settled, the season is
/// saved and the table shows; start the game again for the next round. Without the start screen:
/// SAP_SEASON=new starts a new season over the save, SAP_CLUB=id picks the human's club for a new
/// season (default SAP_HOME, else the first club), SAP_SEED fixes the schedule, SAP_SAVE moves the
/// save file, SAP_PERIOD and SAP_OT shorten periods and overtime, SAP_HUMAN=0 lets the AI play both sides.
/// Once the regular season is over it plays the human's next playoff game instead.</summary>
public sealed class SeasonScene : IScene
{
    /// <inheritdoc/>
    public string Name => "season";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
    {
        var season = SeasonScenes.Open(app, clubs);
        bool human = !Env.Is("SAP_HUMAN", "0");
        if (app.Season.NextSetup(human ? InputDevice.KeyboardMouse : InputDevice.None) is not NextMatch.Play { Setup: var setup } || !app.Season.Playing.TryGet(out var game))
            return PlayoffScenes.Play(app, clubs, settings, human);

        Console.WriteLine($"SAP: season game {game.Number + 1} of {season.Games.Count}, round {game.Round + 1} of {season.Rounds}: {setup.Home.Name} vs {setup.Away.Name}");
        if (SeasonScenes.Seconds("SAP_PERIOD").TryGet(out float period)) setup = setup with { PeriodSeconds = period };
        var world = new World(setup, settings);
        if (SeasonScenes.Seconds("SAP_OT").TryGet(out float ot)) world.Switches.OvertimeSeconds = ot;
        Scenes.AddStarters(world);
        var side = setup.Inputs[0].Side;
        if (human && world.OnIce(side).FirstOrDefault(p => p.Data.Position == PlayingPosition.C) is { } centre) centre.Slot = 0;
        MatchFlow.Begin(world);
        return world;
    }
}

/// <summary>SAP_SCENE=seasontable: the saved season's table (or a new season's) over the rink, nothing played.</summary>
public sealed class SeasonTableScene : IScene
{
    /// <inheritdoc/>
    public string Name => "seasontable";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
    {
        SeasonScenes.Open(app, clubs);
        return SeasonScenes.TableWorld(clubs, settings);
    }
}

/// <summary>Shared setup for the season scenes.</summary>
internal static class SeasonScenes
{
    /// <summary>Continues the save, or starts a new season when there is none or SAP_SEASON=new.</summary>
    public static SeasonState Open(AppState app, IReadOnlyList<ClubData> clubs)
    {
        bool fresh = string.Equals(Env.Get("SAP_SEASON"), "new", StringComparison.OrdinalIgnoreCase);
        if (!fresh && app.Season.Continue(clubs).TryGet(out var saved))
        {
            Console.WriteLine($"SAP: season continued from {app.Season.SavePath}, {saved.Games.Count(g => g.Played)} of {saved.Games.Count} games played");
            return saved;
        }

        string club = Env.Get("SAP_CLUB") is { Length: > 0 } c ? c : Scenes.DefaultSetup(clubs).Home.Id;
        var seed = int.TryParse(Env.Get("SAP_SEED"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int s) ? Maybe.Some(s) : Maybe<int>.None;
        var season = app.Season.StartNew(clubs, club, seed);
        Console.WriteLine($"SAP: new season for {club}, {season.Games.Count} games in {season.Rounds} rounds, saved to {app.Season.SavePath}");
        return season;
    }

    /// <summary>The starting lineups standing still, with the table on top.</summary>
    public static World TableWorld(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var world = new World(Scenes.DefaultSetup(clubs), settings);
        Scenes.AddStarters(world);
        world.Switches.ScheduleTable = true;
        return world;
    }

    /// <summary>Reads a positive number of seconds from an environment variable, or None.</summary>
    public static Maybe<float> Seconds(string variable)
    {
        return float.TryParse(Env.Get(variable), NumberStyles.Float, CultureInfo.InvariantCulture, out float s) && s > 0 ? s : Maybe<float>.None;
    }
}
