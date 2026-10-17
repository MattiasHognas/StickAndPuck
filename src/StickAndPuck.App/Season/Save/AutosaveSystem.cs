using System.Globalization;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season;

/// <summary>Writes the season save as soon as a season game's result is in, so quitting after the final keeps it.</summary>
internal sealed class AutosaveSystem : ISystem
{
    public bool RunsWhilePaused => true;

    public void Update(World world, float dt)
    {
        if (!world.App.Season.Unsaved || !world.App.Season.Current.TryGet(out var season)) return;
        SeasonSave.Write(season, world.App.Season.SavePath);
        world.App.Season.Unsaved = false;
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"SAP: season saved to {world.App.Season.SavePath}, round {Math.Min(season.CurrentRound + 1, season.Rounds)} of {season.Rounds} next"));
    }
}
