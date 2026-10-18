using System.Globalization;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.Arcade.Dev
{
    /// <summary>Shared setup for the arcade scenes.</summary>
    internal static class ArcadeScenes
    {
        /// <summary>Continues the save, or starts a new run when there is none or SAP_ARCADE=new.</summary>
        public static ArcadeRun Open(AppState app, IReadOnlyList<ClubData> clubs)
        {
            bool fresh = string.Equals(
                Env.Get("SAP_ARCADE"),
                "new",
                StringComparison.OrdinalIgnoreCase
            );
            if (!fresh && app.Arcade.Continue(clubs).TryGet(out ArcadeRun saved))
            {
                Console.WriteLine(
                    $"SAP: arcade run continued from {app.Arcade.SavePath}, {saved.Games.Count} game(s) played"
                );
                return saved;
            }

            string club = Env.Get("SAP_CLUB") is { Length: > 0 } c
                ? c
                : Scenes.DefaultSetup(clubs).Home.Id;
            ArcadeRule rule = Env.Get("SAP_ARCADE_RULE") switch
            {
                "" or "lives" or "3" => ArcadeRule.ThreeLives,
                "suddendeath" or "1" => ArcadeRule.SuddenDeath,
                var other => throw new InvalidOperationException(
                    $"SAP_ARCADE_RULE={other}: use suddendeath or lives"
                ),
            };
            Maybe<int> seed = int.TryParse(
                Env.Get("SAP_SEED"),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int s
            )
                ? Maybe.Some(s)
                : Maybe.None<int>();
            ArcadeRun run = app.Arcade.StartNew(clubs, club, rule, seed);
            Console.WriteLine(
                $"SAP: new arcade run for {club} ({rule}), {run.Opponents.Count} clubs to beat, saved to {app.Arcade.SavePath}"
            );
            return run;
        }

        /// <summary>The starting lineups standing still, with the arcade panel on top.</summary>
        public static World SummaryWorld(IReadOnlyList<ClubData> clubs, Settings settings)
        {
            World world = new(Scenes.DefaultSetup(clubs), settings);
            Scenes.AddStarters(world);
            world.Switches.ArcadeSummary = true;
            return world;
        }
    }
}
