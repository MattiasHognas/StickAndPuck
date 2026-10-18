using System.Runtime.CompilerServices;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Players.Puck
{
    /// <summary>Who has a one-timer armed, read-only for drawing: <see cref="PuckHandlingSystem"/> shares its
    /// table of armed skaters (player id to the time they armed) per world, and the skater renderer poses
    /// the wind-up from it. Reading it changes nothing in the match.</summary>
    internal static class OneTimerArming
    {
        private static readonly ConditionalWeakTable<
            World,
            IReadOnlyDictionary<int, float>
        > Tables = [];

        /// <summary>Shares a world's table of armed skaters; the system keeps writing it.</summary>
        public static void Share(World world, IReadOnlyDictionary<int, float> armed)
        {
            Tables.AddOrUpdate(world, armed);
        }

        /// <summary>Gets when a skater armed a one-timer on the pass still on its way, or None when they have not.</summary>
        public static Maybe<float> ArmedAt(World world, int playerId)
        {
            return (
                !Tables.TryGetValue(world, out IReadOnlyDictionary<int, float> armed)
                || world.Puck.Carrier >= 0
                || !world.Puck.Live
            )
                ? Maybe.None<float>()
                : (armed.TryGetValue(playerId, out float at) ? at : Maybe.None<float>());
        }

        /// <summary>Gets a value indicating whether a skater has a one-timer armed on the pass still on its way.</summary>
        public static bool Armed(World world, int playerId)
        {
            return ArmedAt(world, playerId).HasValue;
        }
    }
}
