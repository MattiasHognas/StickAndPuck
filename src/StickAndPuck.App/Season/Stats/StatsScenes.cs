using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season;

/// <summary>Shared setup for the stats screenshots and checks.</summary>
internal static class StatsScenes
{
    /// <summary>When the season in <see cref="SeasonSession"/> has nothing played, settles the human's next
    /// games on paper too (the rest of each round follows), so a leaders page has lines to show.</summary>
    public static int SettleSomeRounds(AppState app, int rounds)
    {
        if (!app.Season.Current.TryGet(out var season) || season.Games.Any(g => g.Played)) return 0;
        var byId = app.Season.Clubs.ToDictionary(c => c.Id, StringComparer.Ordinal);
        int settled = 0;
        for (int r = 0; r < rounds && app.Season.NextSetup() is NextMatch.Play && app.Season.Playing.TryGet(out var game); r++)
        {
            app.Season.Finish(QuickSim.Play(byId[game.Home], byId[game.Away], season.GameSeed(game.Number)));
            settled++;
        }
        season.Stats.CatchUp(season, app.Season.Clubs);
        if (settled > 0) SeasonSave.Write(season, app.Season.SavePath);
        app.Season.Unsaved = false;
        return settled;
    }
}
