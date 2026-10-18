using StickAndPuck.App.Core;

namespace StickAndPuck.App.Replay
{
    /// <summary>What a replay needs to build its match again; the first line of the file after the magic.</summary>
    public sealed record ReplayHeader
    {
        /// <summary>The file format version.</summary>
        public const int CurrentVersion = 1;

        /// <summary>Gets the file format version.</summary>
        public int Version { get; init; } = CurrentVersion;

        /// <summary>Gets the match seed.</summary>
        public int Seed { get; init; }

        /// <summary>Gets how the match was built: "match" (started from the screens) or "scene" (SAP_SCENE).</summary>
        public string Kind { get; init; } = "match";

        /// <summary>Gets the SAP_SCENE name for a scene replay.</summary>
        public string Scene { get; init; } = "";

        /// <summary>Gets the SAP_* variables the match ran with; a replay sets them again.</summary>
        public IReadOnlyDictionary<string, string> Env { get; init; } =
            new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Gets the setup a screens match started from (None for a scene).</summary>
        public Maybe<ReplaySetup> Setup { get; init; }

        /// <summary>Gets the game mode a screens match was played in, by its type name ("" for a season game).</summary>
        public string Mode { get; init; } = "";

        /// <summary>Gets the screens' saved settings at the start of a screens match, as JSON.</summary>
        public string Prefs { get; init; } = "";

        /// <summary>Gets a hash of the two clubs' data, so a replay can warn when a club file changed since.</summary>
        public string Clubs { get; init; } = "";

        /// <summary>Gets where and when it was recorded (shown, never used to replay).</summary>
        public string Recorded { get; init; } = "";
    }
}
