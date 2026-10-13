using StickAndPuck.App.Core;
using StickAndPuck.App.Screens;
using StickAndPuck.App.Season;

// the game keeps tuning knobs and sessions in statics, so tests take turns; each starts from the defaults
[assembly: NotInParallel]

namespace StickAndPuck.Tests.Support;

/// <summary>
/// Readies the game for tests: reads assets from the build output, clears every SAP_* variable but SAP_REPLAYTEST_PRINT so a
/// developer's shell never changes a result, and puts the game's static knobs back to their
/// defaults before each test, as each self-test once started in a fresh process.
/// </summary>
public static class GameSession
{
    /// <summary>Kept: the replay tests print new golden values with it after a deliberate gameplay change.</summary>
    const string PrintGoldens = "SAP_REPLAYTEST_PRINT";

    static readonly List<Action> Restore = [];

    /// <summary>Runs once before any test.</summary>
    [Before(TestSession)]
    public static void Start()
    {
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);
        foreach (var (name, _) in Env.Sap.Where(v => !string.Equals(v.Key, PrintGoldens, StringComparison.Ordinal)))
            Env.Set(name, "");

        Keep(() => PlayoffSession.Rules, v => PlayoffSession.Rules = v);
        Keep(() => ScreenFlow.Prefs, v => ScreenFlow.Prefs = v);
    }

    /// <summary>Runs before every test.</summary>
    [BeforeEvery(Test)]
    public static void Reset()
    {
        foreach (var restore in Restore) restore();
    }

    /// <summary>Remembers a static's value now and puts it back before each test.</summary>
    static void Keep<T>(Func<T> get, Action<T> set)
    {
        var start = get();
        Restore.Add(() => set(start));
    }
}
