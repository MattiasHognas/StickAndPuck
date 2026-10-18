using System.Globalization;

namespace StickAndPuck.App.Core
{
    /// <summary>One match seed, split into one random stream per system, so a match with the same seed and
    /// the same inputs plays the same everywhere. A system's stream depends only on the match seed and
    /// the stream's name, never on how many numbers another system drew. Seed 0 is "no match seed":
    /// each stream then starts from its fixed fallback, the seeds the test scenes were tuned on.</summary>
    public static class Seeds
    {
        /// <summary>Gets the seed from SAP_SEED, or 0 when it is not set.</summary>
        public static int Environment =>
            int.TryParse(
                Env.Get("SAP_SEED"),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int s
            )
                ? s
                : 0;

        /// <summary>Gets a fresh match seed for a match nobody seeded: SAP_SEED when set, otherwise a random one (never 0).</summary>
        public static int NewMatch()
        {
            int env = Environment;
            return env != 0 ? env : Random.Shared.Next(1, int.MaxValue);
        }

        /// <summary>Gets the seed of one system's stream.</summary>
        public static int For(int matchSeed, string stream, int fallback)
        {
            if (matchSeed == 0)
            {
                return fallback;
            }

            // FNV-1a over the name (string.GetHashCode changes every run), then a SplitMix64 finish
            ulong h = 14695981039346656037UL;
            foreach (char c in stream)
            {
                h ^= c;
                h *= 1099511628211UL;
            }
            ulong z = h ^ (uint)matchSeed;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            return (int)(z & 0x7FFFFFFF);
        }

        /// <summary>Gets one system's dice.</summary>
        public static Random Dice(int matchSeed, string stream, int fallback)
        {
#pragma warning disable S2245 // gameplay dice, seeded on purpose so matches replay
            return new Random(For(matchSeed, stream, fallback));
#pragma warning restore S2245
        }
    }
}
