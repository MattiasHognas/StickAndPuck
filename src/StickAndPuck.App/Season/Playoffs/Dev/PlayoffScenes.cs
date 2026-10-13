using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules;

namespace StickAndPuck.App.Season.Dev;

/// <summary>SAP_SCENE=playoffs: a playoff test setup. It uses its own save, playoffs-test.json next to the
/// season save (SAP_SAVE overrides), so the real season is never touched: it starts a season there
/// if there is none (SAP_SEASON=new, SAP_CLUB, SAP_SEED as in the season scene), settles the rest
/// of the regular season on paper, human's games included, and plays the human's next playoff game.
/// Start it again for the next game; when the human's club is out the bracket plays to a champion.
/// SAP_PERIOD, SAP_OT and SAP_HUMAN=0 work as in the season scene.</summary>
public sealed class PlayoffScene : IScene
{
    /// <inheritdoc/>
    public string Name => "playoffs";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        PlayoffScenes.OpenTestSeason(clubs);
        return PlayoffScenes.Play(clubs, settings, PlayoffScenes.Human);
    }
}

/// <summary>SAP_SCENE=playoffbracket: the playoff test save's bracket over the rink, nothing played.</summary>
public sealed class PlayoffBracketScene : IScene
{
    /// <inheritdoc/>
    public string Name => "playoffbracket";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        PlayoffScenes.OpenTestSeason(clubs);
        PlayoffSession.Open();
        if (PlayoffSession.Current.TryGet(out var playoffs)) PlayoffSystem.Log(playoffs);
        return PlayoffScenes.BracketWorld(clubs, settings);
    }
}

/// <summary>Shared setup for the playoff scenes; SAP_SCENE=season also ends up here once the regular season is over.</summary>
internal static class PlayoffScenes
{
    /// <summary>Gets a value indicating whether a human plays (SAP_HUMAN=0 lets the AI play both sides).</summary>
    public static bool Human => !Env.Is("SAP_HUMAN", "0");

    /// <summary>Plays the human's next playoff game of the season in <see cref="SeasonSession"/>, starting the
    /// playoffs if they have not started; shows the bracket when the human's club has no game left.</summary>
    public static World Play(IReadOnlyList<ClubData> clubs, Settings settings, bool human)
    {
        // SAP_OT shortens the playoff overtime periods too, for watching one; the setup below carries it
        if (SeasonScenes.Seconds("SAP_OT").TryGet(out float overtime))
            PlayoffSession.Rules = PlayoffSession.Rules with { Overtime = PlayoffSession.Rules.Overtime with { PeriodSeconds = overtime } };
        var opened = PlayoffSession.Open();
        var next = PlayoffSession.NextSetup(human ? InputDevice.KeyboardMouse : InputDevice.None);
        if (!opened.TryGet(out var playoffs))
        {
            Console.WriteLine("SAP: the regular season is not over yet");
            return BracketWorld(clubs, settings);
        }
        if (next is not NextMatch.Play { Setup: var setup } || !PlayoffSession.Playing.TryGet(out var series))
        {
            Console.WriteLine("SAP: the human's club has no playoff game left; showing the bracket");
            PlayoffSystem.Log(playoffs);
            return BracketWorld(clubs, settings);
        }

        Console.WriteLine($"SAP: playoff {playoffs.Current.Name} game {series.Games.Count + 1}: {setup.Home.Name} vs {setup.Away.Name}, series {series.High} {series.HighWins}-{series.LowWins} {series.Low}");
        if (SeasonScenes.Seconds("SAP_PERIOD").TryGet(out float period)) setup = setup with { PeriodSeconds = period };
        var world = new World(setup, settings);
        if (SeasonScenes.Seconds("SAP_OT").TryGet(out float ot)) world.Switches.OvertimeSeconds = ot;
        Scenes.AddStarters(world);
        var side = setup.Inputs[0].Side;
        if (human && world.OnIce(side).FirstOrDefault(p => p.Data.Position == PlayingPosition.C) is { } centre) centre.Slot = 0;
        MatchFlow.Begin(world);
        return world;
    }

    /// <summary>The starting lineups standing still, with the bracket on top.</summary>
    public static World BracketWorld(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var world = new World(Scenes.DefaultSetup(clubs), settings);
        Scenes.AddStarters(world);
        world.Switches.PlayoffBracket = true;
        return world;
    }

    /// <summary>Opens (or starts) the playoff test season and settles whatever is left of its regular season.</summary>
    public static void OpenTestSeason(IReadOnlyList<ClubData> clubs)
    {
        string path = Env.Get("SAP_SAVE") is { Length: > 0 }
            ? SeasonSave.DefaultPath
            : Path.Combine(Path.GetDirectoryName(SeasonSave.DefaultPath) ?? "", "playoffs-test.json");
        bool fresh = string.Equals(Env.Get("SAP_SEASON"), "new", StringComparison.OrdinalIgnoreCase);
        var season = !fresh && SeasonSession.Continue(clubs, path).TryGet(out var saved) ? saved : StartTestSeason(clubs, path);
        int settled = SettleSeason();
        if (settled > 0) SeasonSave.Write(season, path);
        int place = season.Table.ToList().FindIndex(r => string.Equals(r.ClubId, season.UserClub, StringComparison.Ordinal)) + 1;
        Console.WriteLine($"SAP: playoff test season in {path}: {season.UserClub} finished {place} of {season.Clubs.Count}{(settled > 0 ? $", {settled} of the human's games settled on paper" : "")}");
    }

    /// <summary>Starts the playoff test season at its own save (SAP_CLUB, SAP_SEED as in the season scene).</summary>
    static SeasonState StartTestSeason(IReadOnlyList<ClubData> clubs, string path)
    {
        string club = Env.Get("SAP_CLUB") is { Length: > 0 } c ? c : Scenes.DefaultSetup(clubs).Home.Id;
        var seed = int.TryParse(Env.Get("SAP_SEED"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int s) ? Maybe.Some(s) : Maybe<int>.None;
        return SeasonSession.StartNew(clubs, club, seed, path);
    }

    /// <summary>Settles the human's remaining regular season games on paper, and with them the rest of the season.</summary>
    public static int SettleSeason()
    {
        int count = 0;
        while (SeasonSession.NextSetup(InputDevice.None) is NextMatch.Play { Setup: var setup } && SeasonSession.Playing.TryGet(out var game) && SeasonSession.Current.TryGet(out var season))
        {
            SeasonSession.Finish(QuickSim.Play(setup.Home, setup.Away, season.GameSeed(game.Number)));
            count++;
        }
        SeasonSession.Unsaved = false;
        return count;
    }
}
