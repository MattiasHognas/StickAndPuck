using System.Numerics;

namespace StickAndPuck.App.Core
{
    /// <summary>Finds scenes and builds the default match setup.</summary>
    public static class Scenes
    {
        /// <summary>The scene used when SAP_SCENE is not set.</summary>
        public const string Default = "start";

        /// <summary>Gets every scene in the game, by name.</summary>
        public static IReadOnlyDictionary<string, IScene> All()
        {
            return DiscoveryRegistry
                .Scenes()
                .ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Builds the world for the scene named by SAP_SCENE, or the default scene, running with the game's state.</summary>
        public static World CreateFromEnvironment(
            IReadOnlyList<ClubData> clubs,
            Settings settings,
            AppState app
        )
        {
            string name =
                Env.Get("SAP_SCENE") is { Length: > 0 } s ? s
                : Env.Get("SAP_ENDING").Length > 0 ? Season.Endings.EndingScene.SceneName // SAP_ENDING alone jumps straight to that scene
                : Default;
            IReadOnlyDictionary<string, IScene> all = All();
            if (!all.TryGetValue(name, out IScene scene))
            {
                throw new InvalidOperationException(
                    $"SAP_SCENE={name} is not a scene. Scenes: {string.Join(", ", all.Keys.Order(StringComparer.Ordinal))}"
                );
            }

            Console.WriteLine($"SAP: scene {scene.Name}");
            World world = scene.Create(clubs, settings, app);
            world.App = app;
            return world;
        }

        /// <summary>The default exhibition: SAP_HOME and SAP_AWAY pick clubs by id (an NHL club id from
        /// data/clubs/nhl/ works too), otherwise the first two
        /// clubs (or the one club twice), with keyboard and mouse on the home club.</summary>
        public static MatchSetup DefaultSetup(IReadOnlyList<ClubData> clubs)
        {
            if (clubs.Count == 0)
            {
                throw new InvalidOperationException("no clubs loaded from data/clubs/{league}/");
            }

            ClubData home = Pick(clubs, "SAP_HOME").Or(clubs[0]);
            ClubData away = Pick(clubs, "SAP_AWAY").TryGet(out ClubData picked)
                ? picked
                : clubs.FirstOrDefault(c => c != home) ?? home;
            return new MatchSetup
            {
                Home = home,
                Away = away,
                Rivalry = Rivals.Are(home.Id, away.Id),
            };
        }

        private static Maybe<ClubData> Pick(IReadOnlyList<ClubData> clubs, string variable)
        {
            string id = Env.Get(variable);
            return id.Length == 0
                ? Maybe.None<ClubData>()
                : (Maybe<ClubData>)(
                    clubs.FirstOrDefault(c =>
                        string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase)
                    )
                    ?? Clubs
                        .LoadNhl()
                        .FirstOrDefault(c =>
                            string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase)
                        )
                    ?? Clubs
                        .LoadAll()
                        .FirstOrDefault(c =>
                            string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase)
                        ) // an SHL id while the menus list the NHL clubs
                    ?? throw new InvalidOperationException($"{variable}={id} is not a club id")
                );
        }

        /// <summary>Puts each club's starters on the ice at the opening faceoff: centre, wings, two defence and
        /// a goalie per side. Home starts at -x. Missing positions are filled from the rest of the roster.</summary>
        public static void AddStarters(World world, int skatersPerSide = 5)
        {
            foreach (Side side in new[] { Side.Home, Side.Away })
            {
                float end = side == Side.Home ? -1 : 1;
                IReadOnlyList<PlayerData> roster = world.Setup.Club(side).Roster;
                HashSet<PlayerData> used = [];
                Maybe<PlayerData> Take(PlayingPosition pos)
                {
                    return roster.FirstOrNone(p => p.Position == pos && used.Add(p));
                }

                Maybe<PlayerData> Any()
                {
                    return roster.FirstOrNone(p => p.Position != PlayingPosition.G && used.Add(p));
                }

                Vector3[] spots =
                [
                    new(-0.8f, 0, 0),
                    new(-1.5f, 0, -6.5f),
                    new(-1.5f, 0, 6.5f),
                    new(-9.5f, 0, -3.5f),
                    new(-9.5f, 0, 3.5f),
                ];
                PlayingPosition[] positions =
                [
                    PlayingPosition.C,
                    PlayingPosition.LW,
                    PlayingPosition.RW,
                    PlayingPosition.D,
                    PlayingPosition.D,
                ];
                for (int i = 0; i < Math.Min(skatersPerSide, spots.Length); i++)
                {
                    if (!Take(positions[i]).TryGet(out PlayerData data) && !Any().TryGet(out data))
                    {
                        break;
                    }

                    Player p = world.AddPlayer(side, data, spots[i] with { X = spots[i].X * -end });
                    p.Heading = end < 0 ? MathF.PI / 2 : -MathF.PI / 2;
                }
                if (Take(PlayingPosition.G).TryGet(out PlayerData goalie))
                {
                    Player g = world.AddPlayer(
                        side,
                        goalie,
                        new Vector3(end * (RinkSpec.GoalLineX - 1f), 0, 0)
                    );
                    g.Heading = end < 0 ? MathF.PI / 2 : -MathF.PI / 2;
                }
            }
        }
    }
}
