using StickAndPuck.App.Core;

namespace StickAndPuck.App.Screens
{
    /// <summary>Finds the game modes.</summary>
    internal static class GameModes
    {
        /// <summary>Gets every mode available now, in list order.</summary>
        public static IReadOnlyList<IGameMode> All(AppState app)
        {
            return
            [
                .. DiscoveryRegistry
                    .Modes()
                    .Where(m => m.Available(app))
                    .OrderBy(m => m.Order)
                    .ThenBy(m => m.Title, StringComparer.Ordinal),
            ];
        }

        /// <summary>Gets a fresh mode by its type name, as a replay names it.</summary>
        public static Maybe<IGameMode> Named(string typeName)
        {
            return DiscoveryRegistry
                .Modes()
                .FirstOrNone(mode =>
                    string.Equals(mode.GetType().Name, typeName, StringComparison.Ordinal)
                );
        }
    }
}
