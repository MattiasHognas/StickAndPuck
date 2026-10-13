using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules;

namespace StickAndPuck.App.Season;

/// <summary>
/// SAP_SCENE=season: continues the saved season (or starts one) and plays the human's next game.
/// When the match ends the result is recorded, the round's other games are settled, the season is
/// saved and the table shows; start the game again for the next round. Until W11's start screen:
/// SAP_SEASON=new starts a new season over the save, SAP_CLUB=id picks the human's club for a new
/// season (default SAP_HOME, else the first club), SAP_SEED fixes the schedule, SAP_SAVE moves the
/// save file, SAP_PERIOD and SAP_OT shorten periods and overtime, SAP_HUMAN=0 lets the AI play both sides.
/// Once the regular season is over it plays the human's next playoff game instead (W16b).
/// </summary>
public sealed class SeasonScene : IScene
{
    /// <inheritdoc/>
    public string Name => "season";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var season = SeasonScenes.Open(clubs);
        bool human = !Env.Is("SAP_HUMAN", "0");
        if (SeasonSession.NextSetup(human ? InputDevice.KeyboardMouse : InputDevice.None) is not NextMatch.Play { Setup: var setup } || !SeasonSession.Playing.TryGet(out var game))
            return PlayoffScenes.Play(clubs, settings, human);

        Console.WriteLine($"SAP: season game {game.Number + 1} of {season.Games.Count}, round {game.Round + 1} of {season.Rounds}: {setup.Home.Name} vs {setup.Away.Name}");
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
        bool fresh = string.Equals(Env.Get("SAP_SEASON"), "new", StringComparison.OrdinalIgnoreCase);
        if (!fresh && SeasonSession.Continue(clubs).TryGet(out var saved))
        {
            Console.WriteLine($"SAP: season continued from {SeasonSession.SavePath}, {saved.Games.Count(g => g.Played)} of {saved.Games.Count} games played");
            return saved;
        }

        string club = Env.Get("SAP_CLUB") is { Length: > 0 } c ? c : Scenes.DefaultSetup(clubs).Home.Id;
        var seed = int.TryParse(Env.Get("SAP_SEED"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int s) ? Maybe.Some(s) : Maybe<int>.None;
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

    /// <summary>Reads a positive number of seconds from an environment variable, or None.</summary>
    public static Maybe<float> Seconds(string variable)
    {
        return float.TryParse(Env.Get(variable), NumberStyles.Float, CultureInfo.InvariantCulture, out float s) && s > 0 ? s : Maybe<float>.None;
    }
}
