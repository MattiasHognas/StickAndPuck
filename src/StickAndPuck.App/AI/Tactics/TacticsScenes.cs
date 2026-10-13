using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules;

namespace StickAndPuck.App.AI;

/// <summary>
/// SAP_SCENE=tactics: an all-AI exhibition with each club's tactics and its dumps and entries on
/// a line at the bottom. SAP_HOME and SAP_AWAY pick the clubs (default Frölunda's 2-1-2 against
/// Luleå's trap); SAP_TACTICS_HOME and SAP_TACTICS_AWAY override a club's forecheck
/// (balanced, aggressive, trap). SAP_PERIOD=seconds shortens the periods.
/// </summary>
public sealed class TacticsScene : IScene
{
    /// <inheritdoc/>
    public string Name => "tactics";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        ClubData Pick(string variable, string fallback, ClubData other)
        {
            string id = Env.Get(variable) is { Length: > 0 } v ? v : fallback;
            return clubs.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase)) ?? clubs.FirstOrDefault(c => c != other) ?? clubs[0];
        }
        var setup = Scenes.DefaultSetup(clubs);
        var home = Pick("SAP_HOME", "frolunda", setup.Away);
        var away = Pick("SAP_AWAY", "lulea", home);
        home = home with { Tactics = Override(home.Tactics, "SAP_TACTICS_HOME") };
        away = away with { Tactics = Override(away.Tactics, "SAP_TACTICS_AWAY") };
        float period = float.TryParse(Env.Get("SAP_PERIOD"), NumberStyles.Float, CultureInfo.InvariantCulture, out float s) && s > 0 ? s : 20 * 60;
        var world = new World(setup with { Home = home, Away = away, PeriodSeconds = period }, settings);
        Scenes.AddStarters(world);
        MatchFlow.Begin(world);
        world.Switches.TacticsLine = true;
        return world;
    }

    static ClubTactics Override(ClubTactics tactics, string variable)
    {
        return Enum.TryParse<Forecheck>(Env.Get(variable), ignoreCase: true, out var f) ? tactics with { Forecheck = f } : tactics;
    }
}
