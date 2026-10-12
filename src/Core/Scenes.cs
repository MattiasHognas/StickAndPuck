using System.Numerics;

namespace StickAndPuck.Core;

/// <summary>
/// A starting setup the game can boot straight into with SAP_SCENE=name, for example a faceoff
/// or a shot on goal. Any class implementing this with a parameterless constructor is found
/// automatically, so a task adds a scene in its own folder without touching Core.
/// </summary>
public interface IScene
{
    /// <summary>Gets the name used in SAP_SCENE.</summary>
    string Name { get; }

    /// <summary>Builds the world: the match setup, the players and the puck.</summary>
    /// <param name="clubs">Every club from data/clubs/.</param>
    /// <param name="settings">Player options.</param>
    /// <returns>The world to run.</returns>
    World Create(IReadOnlyList<ClubData> clubs, Settings settings);
}

/// <summary>Finds scenes and builds the default match setup.</summary>
public static class Scenes
{
    /// <summary>The scene used when SAP_SCENE is not set.</summary>
    public const string Default = "start";

    /// <summary>Gets every scene in the game, by name.</summary>
    /// <returns>The scenes.</returns>
    public static IReadOnlyDictionary<string, IScene> All()
    {
        return typeof(Scenes).Assembly.GetTypes()
            .Where(t => typeof(IScene).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false }
                && t.GetConstructor(Type.EmptyTypes) != null)
            .Select(t => (IScene)Activator.CreateInstance(t)!)
            .ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Builds the world for the scene named by SAP_SCENE, or the default scene.</summary>
    /// <param name="clubs">Every club from data/clubs/.</param>
    /// <param name="settings">Player options.</param>
    /// <returns>The world to run.</returns>
    public static World CreateFromEnvironment(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        string name = Environment.GetEnvironmentVariable("SAP_SCENE") is { Length: > 0 } s ? s : Default;
        var all = All();
        if (!all.TryGetValue(name, out var scene))
            throw new InvalidOperationException($"SAP_SCENE={name} is not a scene. Scenes: {string.Join(", ", all.Keys.Order(StringComparer.Ordinal))}");
        Console.WriteLine($"SAP: scene {scene.Name}");
        return scene.Create(clubs, settings);
    }

    /// <summary>
    /// The default exhibition: SAP_HOME and SAP_AWAY pick clubs by id, otherwise the first two
    /// clubs (or the one club twice), with keyboard and mouse on the home club.
    /// </summary>
    /// <param name="clubs">Every club from data/clubs/.</param>
    /// <returns>The setup.</returns>
    public static MatchSetup DefaultSetup(IReadOnlyList<ClubData> clubs)
    {
        if (clubs.Count == 0) throw new InvalidOperationException($"no club files in {Assets.ClubFolder}");
        var home = Pick(clubs, "SAP_HOME") ?? clubs[0];
        var away = Pick(clubs, "SAP_AWAY") ?? clubs.FirstOrDefault(c => c != home) ?? home;
        return new MatchSetup { Home = home, Away = away };
    }

    static ClubData? Pick(IReadOnlyList<ClubData> clubs, string variable)
    {
        string? id = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrEmpty(id)) return null;
        return clubs.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"{variable}={id} is not a club id");
    }

    /// <summary>
    /// Puts each club's starters on the ice at the opening faceoff: centre, wings, two defence and
    /// a goalie per side. Home starts at -x. Missing positions are filled from the rest of the roster.
    /// </summary>
    /// <param name="world">The world to fill.</param>
    /// <param name="skatersPerSide">Skaters per club, 5 in regulation and 3 in overtime.</param>
    public static void AddStarters(World world, int skatersPerSide = 5)
    {
        foreach (var side in new[] { Side.Home, Side.Away })
        {
            float end = side == Side.Home ? -1 : 1;
            var roster = world.Setup.Club(side).Roster;
            var used = new HashSet<PlayerData>();
            PlayerData? Take(PlayingPosition pos) => roster.FirstOrDefault(p => p.Position == pos && used.Add(p));
            PlayerData? Any() => roster.FirstOrDefault(p => p.Position != PlayingPosition.G && used.Add(p));

            Vector3[] spots = [new(-0.8f, 0, 0), new(-1.5f, 0, -6.5f), new(-1.5f, 0, 6.5f), new(-9.5f, 0, -3.5f), new(-9.5f, 0, 3.5f)];
            PlayingPosition[] positions = [PlayingPosition.C, PlayingPosition.LW, PlayingPosition.RW, PlayingPosition.D, PlayingPosition.D];
            for (int i = 0; i < Math.Min(skatersPerSide, spots.Length); i++)
            {
                var data = Take(positions[i]) ?? Any();
                if (data == null) break;
                var p = world.AddPlayer(side, data, spots[i] with { X = spots[i].X * -end });
                p.Heading = end < 0 ? MathF.PI / 2 : -MathF.PI / 2;
            }
            if (Take(PlayingPosition.G) is { } goalie)
            {
                var g = world.AddPlayer(side, goalie, new Vector3(end * (RinkSpec.GoalLineX - 1f), 0, 0));
                g.Heading = end < 0 ? MathF.PI / 2 : -MathF.PI / 2;
            }
        }
    }
}
