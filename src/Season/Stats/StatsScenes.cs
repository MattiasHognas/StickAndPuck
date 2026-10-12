namespace StickAndPuck.Season;

/// <summary>Shared setup for the stats screenshots and checks.</summary>
internal static class StatsScenes
{
    /// <summary>
    /// When the season in <see cref="SeasonSession"/> has nothing played, settles the human's next
    /// games on paper too (the rest of each round follows), so a leaders page has lines to show.
    /// </summary>
    /// <param name="rounds">How many rounds to settle.</param>
    /// <returns>The rounds settled.</returns>
    public static int SettleSomeRounds(int rounds)
    {
        if (SeasonSession.Current is not { } season || season.Games.Any(g => g.Played)) return 0;
        var byId = SeasonSession.Clubs.ToDictionary(c => c.Id, StringComparer.Ordinal);
        int settled = 0;
        for (int r = 0; r < rounds && SeasonSession.NextSetup() != null; r++)
        {
            var game = SeasonSession.Playing!;
            SeasonSession.Finish(QuickSim.Play(byId[game.Home], byId[game.Away], season.GameSeed(game.Number)));
            settled++;
        }
        season.Stats.CatchUp(season, SeasonSession.Clubs);
        if (settled > 0) SeasonSave.Write(season, SeasonSession.SavePath);
        SeasonSession.Unsaved = false;
        return settled;
    }
}
