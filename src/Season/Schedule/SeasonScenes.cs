using System.Globalization;
using StickAndPuck.Core;
using StickAndPuck.Rules;

namespace StickAndPuck.Season;

/// <summary>
/// SAP_SCENE=season: continues the saved season (or starts one) and plays the human's next game.
/// When the match ends the result is recorded, the round's other games are settled, the season is
/// saved and the table shows; start the game again for the next round. Until W11's start screen:
/// SAP_SEASON=new starts a new season over the save, SAP_CLUB=id picks the human's club for a new
/// season (default SAP_HOME, else the first club), SAP_SEED fixes the schedule, SAP_SAVE moves the
/// save file, SAP_PERIOD and SAP_OT shorten periods and overtime, SAP_HUMAN=0 lets the AI play both sides.
/// </summary>
public sealed class SeasonScene : IScene
{
    /// <inheritdoc/>
    public string Name => "season";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var season = SeasonScenes.Open(clubs);
        bool human = !string.Equals(Environment.GetEnvironmentVariable("SAP_HUMAN"), "0", StringComparison.Ordinal);
        var setup = SeasonSession.NextSetup(human ? InputDevice.KeyboardMouse : InputDevice.None);
        if (setup == null)
        {
            Console.WriteLine("SAP: the season is over; showing the final table (W16b starts the playoffs from here)");
            return SeasonScenes.TableWorld(clubs, settings);
        }

        var game = SeasonSession.Playing!;
        Console.WriteLine($"SAP: season game {game.Number + 1} of {season.Games.Count}, round {game.Round + 1} of {season.Rounds}: {setup.Home.Name} vs {setup.Away.Name}");
        if (SeasonScenes.Seconds("SAP_PERIOD") is { } period) setup = setup with { PeriodSeconds = period };
        if (SeasonScenes.Seconds("SAP_OT") is { } ot) OvertimeRules.PeriodSeconds = ot;
        var world = new World(setup, settings);
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
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        SeasonScenes.Open(clubs);
        return SeasonScenes.TableWorld(clubs, settings);
    }
}

/// <summary>Shared setup for the season scenes.</summary>
internal static class SeasonScenes
{
    /// <summary>Continues the save, or starts a new season when there is none or SAP_SEASON=new.</summary>
    public static SeasonState Open(IReadOnlyList<ClubData> clubs)
    {
        bool fresh = string.Equals(Environment.GetEnvironmentVariable("SAP_SEASON"), "new", StringComparison.OrdinalIgnoreCase);
        if (!fresh && SeasonSession.Continue(clubs) is { } saved)
        {
            Console.WriteLine($"SAP: season continued from {SeasonSession.SavePath}, {saved.Games.Count(g => g.Played)} of {saved.Games.Count} games played");
            return saved;
        }

        string club = Environment.GetEnvironmentVariable("SAP_CLUB") is { Length: > 0 } c ? c : Scenes.DefaultSetup(clubs).Home.Id;
        int? seed = int.TryParse(Environment.GetEnvironmentVariable("SAP_SEED"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int s) ? s : null;
        var season = SeasonSession.StartNew(clubs, club, seed);
        Console.WriteLine($"SAP: new season for {club}, {season.Games.Count} games in {season.Rounds} rounds, saved to {SeasonSession.SavePath}");
        return season;
    }

    /// <summary>The starting lineups standing still, with the table on top.</summary>
    public static World TableWorld(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var world = new World(Scenes.DefaultSetup(clubs), settings);
        Scenes.AddStarters(world);
        ScheduleSystem.ShowTable = true;
        return world;
    }

    /// <summary>Reads a positive number of seconds from an environment variable.</summary>
    public static float? Seconds(string variable)
    {
        return float.TryParse(Environment.GetEnvironmentVariable(variable), NumberStyles.Float, CultureInfo.InvariantCulture, out float s) && s > 0 ? s : null;
    }
}
