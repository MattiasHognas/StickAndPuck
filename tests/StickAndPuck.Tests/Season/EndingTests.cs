using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Endings;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Season
{
    /// <summary>
    /// The end-of-season scenes (SAP_ENDING): the names it takes, a title line in both languages for every
    /// ending, the club and league it plays for, and that such a world runs only its own presentation.
    /// </summary>
    public sealed class EndingTests : CheckedTest
    {
        [Test]
        public void ParseTakesEveryEndingName()
        {
            foreach (
                (string name, EndingKind kind) in new[]
                {
                    ("champions", EndingKind.Champions),
                    ("runnersup", EndingKind.RunnersUp),
                    ("Runners-Up", EndingKind.RunnersUp),
                    ("semifinal", EndingKind.SemiFinal),
                    ("semi_final", EndingKind.SemiFinal),
                    ("seasonover", EndingKind.SeasonOver),
                    ("RELEGATED", EndingKind.Relegated),
                    ("fired", EndingKind.Fired),
                }
            )
            {
                Check(EndingStage.Parse(name).Is(k => k == kind), $"{name} is {kind}");
            }

            foreach (string bad in new[] { "", "3", "promoted", "champion" })
            {
                Check(!EndingStage.Parse(bad).HasValue, $"'{bad}' is no ending");
            }
        }

        [Test]
        public void EveryEndingHasATitleInBothLanguages()
        {
            IReadOnlyDictionary<string, string> en = Text.Read(Text.FileOf(Language.English));
            IReadOnlyDictionary<string, string> sv = Text.Read(Text.FileOf(Language.Swedish));
            HashSet<string> keys = [with(StringComparer.Ordinal)];
            foreach (EndingKind kind in Enum.GetValues<EndingKind>())
            {
                string key = new EndingStage(kind).TitleKey;
                Check(keys.Add(key), $"{kind} has its own title key {key}");
                Check(en.TryGetValue(key, out string e) && e.Length > 0, $"{key} in English");
                Check(sv.TryGetValue(key, out string s) && s.Length > 0, $"{key} in Swedish");
            }
        }

        [Test]
        [NotInParallel("SAP_ENDING")]
        public void SceneFollowsTheClubsLeagueAndRunsOnlyTheEnding()
        {
            try
            {
                Env.Set("SAP_ENDING", "champions");
                Env.Set("SAP_CLUB", "frolunda");
                World shl = new EndingScene().Create(
                    WorldBuilder.Clubs,
                    new Settings(),
                    new AppState()
                );
                Check(
                    string.Equals(shl.Setup.Home.Id, "frolunda", StringComparison.Ordinal),
                    $"the user's club is {shl.Setup.Home.Id}"
                );
                Check(
                    !string.Equals(shl.Setup.Away.Id, "frolunda", StringComparison.Ordinal)
                        && shl.Setup.Away.League == League.Shl,
                    $"the other club {shl.Setup.Away.Id} is another SHL club"
                );
                Check(
                    shl.Dev.Ending.TryGet(out EndingStage stage)
                        && stage.Kind == EndingKind.Champions,
                    "the world plays the champions ending"
                );
                IReadOnlyList<ISystem> systems = Systems.For(shl);
                Check(
                    systems.Count == 1 && systems[0] is EndingSystem,
                    $"an ending world runs only the ending ({systems.Count} system(s))"
                );
                Check(
                    Systems.For(new World(WorldBuilder.DefaultSetup, new Settings())).Count > 1,
                    "a match world still runs every system"
                );

                Env.Set("SAP_ENDING", "relegated");
                Env.Set("SAP_CLUB", "bruins");
                World nhl = new EndingScene().Create(
                    WorldBuilder.Clubs,
                    new Settings(),
                    new AppState()
                );
                Check(
                    nhl.League == League.Nhl && nhl.Setup.Away.League == League.Nhl,
                    $"an NHL club plays it in the NHL, against {nhl.Setup.Away.Id}"
                );

                Env.Set("SAP_ENDING", "promoted");
                bool threw = false;
                try
                {
                    _ = new EndingScene().Create(
                        WorldBuilder.Clubs,
                        new Settings(),
                        new AppState()
                    );
                }
                catch (InvalidOperationException)
                {
                    threw = true;
                }

                Check(threw, "an unknown ending stops with the list of endings");
            }
            finally
            {
                Env.Set("SAP_ENDING", "");
                Env.Set("SAP_CLUB", "");
            }
        }
    }
}
