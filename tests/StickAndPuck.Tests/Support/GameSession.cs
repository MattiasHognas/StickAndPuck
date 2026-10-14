using StickAndPuck.App.Core;

namespace StickAndPuck.Tests.Support;

/// <summary>
/// Readies the game for tests: reads assets from the build output and clears every SAP_* variable but
/// SAP_REPLAYTEST_PRINT so a developer's shell never changes a result. Both happen once, before any test
/// runs; after that no test changes the folder or a variable, and the sessions, screens and pads live on
/// each world's <see cref="AppState"/>, so the tests run side by side, each on a fresh one.
/// </summary>
public static class GameSession
{
    /// <summary>Kept: the replay tests print new golden values with it after a deliberate gameplay change.</summary>
    const string PrintGoldens = "SAP_REPLAYTEST_PRINT";

    /// <summary>Runs once before any test.</summary>
    [Before(TestSession)]
    public static void Start()
    {
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);
        foreach (var (name, _) in Env.Sap.Where(v => !string.Equals(v.Key, PrintGoldens, StringComparison.Ordinal)))
            Env.Set(name, "");
    }
}
