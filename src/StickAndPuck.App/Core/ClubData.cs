using StickAndPuck.App.AI.Tactics;

namespace StickAndPuck.App.Core
{
    /// <summary>A club as written in its data file under data/clubs/: names, kit colours and the roster.</summary>
    public sealed record ClubData
    {
        /// <summary>Gets the file-safe id, also used for kit atlas names (for example "frolunda").</summary>
        public string Id { get; init; } = "";

        /// <summary>Gets the full club name.</summary>
        public string Name { get; init; } = "";

        /// <summary>Gets the three-letter name for the scorebug.</summary>
        public string ShortName { get; init; } = "";

        /// <summary>Gets the home kit colours.</summary>
        public KitColours Home { get; init; } = new();

        /// <summary>Gets the away kit colours.</summary>
        public KitColours Away { get; init; } = new();

        /// <summary>Gets the match squad: 4 forward lines, 3 defence pairs and 2 goalies in a full club.</summary>
        public IReadOnlyList<PlayerData> Roster { get; init; } = [];

        /// <summary>Gets how the club plays: forecheck, zone entry and power play (src/AI/Tactics).</summary>
        public ClubTactics Tactics { get; init; } = new();
    }
}
