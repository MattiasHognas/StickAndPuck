using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season;

/// <summary>Writes the season save as soon as a season game's result is in, so quitting after the final keeps it.</summary>
internal sealed class AutosaveSystem : ISystem
{
    public bool RunsWhilePaused => true;

    public void Update(World world, float dt)
    {
        if (!SeasonSession.Unsaved || !SeasonSession.Current.TryGet(out var season)) return;
        SeasonSave.Write(season, SeasonSession.SavePath);
        SeasonSession.Unsaved = false;
        Console.WriteLine($"SAP: season saved to {SeasonSession.SavePath}, round {Math.Min(season.CurrentRound + 1, season.Rounds)} of {season.Rounds} next");
    }
}
