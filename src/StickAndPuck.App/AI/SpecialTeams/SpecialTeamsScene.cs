using System.Globalization;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.AI;

/// <summary>
/// SAP_SCENE=specialteams: an all-AI match with a skater of the away club in the box for good, so
/// home plays a power play the whole first period, with each skater's spot drawn on the ice and the shapes and clears
/// on the bottom line. SAP_ST_KILL=home puts the home club a man short instead. SAP_ST_WAIT=1 plays
/// off screen until the power play has held its shape in the zone for a couple of seconds, for a
/// headless screenshot (SAP_SCREENSHOT=1) of the shape.
/// </summary>
public sealed class SpecialTeamsScene : IScene
{
    /// <inheritdoc/>
    public string Name => "specialteams";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var kill = string.Equals(Env.Get("SAP_ST_KILL"), "home", StringComparison.OrdinalIgnoreCase) ? Side.Home : Side.Away;
        var world = SpecialTeamsSetup.ShortHanded(clubs, settings, kill);
        SpecialTeamsSystem.ShowSpots = true;
        if (Env.Is("SAP_ST_WAIT", "1")) Wait(world, kill == Side.Home ? Side.Away : Side.Home);
        return world;
    }

    /// <summary>Plays the match off screen until the power play has been in its shape for two seconds, or two minutes have gone.</summary>
    static void Wait(World world, Side pp)
    {
        int seed = int.TryParse(Env.Get("SAP_SEED"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int s) ? s : 1;
        var systems = SpecialTeamsSetup.Systems(world, seed, special: true, rules: true);
        float inShape = 0;
        for (int i = 0; i < 120 / Game.Step && inShape < 2f; i++)
        {
            world.Events.Clear();
            foreach (var system in systems) system.Update(world, Game.Step);
            world.Time += Game.Step; world.Tick++;
            inShape = SpecialTeams.FormationOf(world, pp) != Formation.None ? inShape + Game.Step : 0;
        }
    }
}
