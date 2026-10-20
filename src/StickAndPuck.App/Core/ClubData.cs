using System.Text.Json.Serialization;
using StickAndPuck.App.AI.Tactics;

namespace StickAndPuck.App.Core
{
    /// <summary>A club as written in its data file under data/clubs/{league}/: names, kit colours and the roster.</summary>
    public sealed record ClubData
    {
        /// <summary>Gets the file-safe id, also used for kit atlas names (for example "frolunda").</summary>
        public string Id { get; init; } = "";

        /// <summary>Gets the full club name.</summary>
        public string Name { get; init; } = "";

        /// <summary>Gets the three-letter name for the scorebug.</summary>
        public string ShortName { get; init; } = "";

        /// <summary>Gets the league the club plays in. SHL (the default) is left out of the written JSON so
        /// SHL club output, and the replay club fingerprint, stays unchanged.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public League League { get; init; } = League.Shl;

        /// <summary>Gets the conference (for example "Western" in the NHL); none when the league has none, and
        /// then left out of the written JSON.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Maybe<string> Conference { get; init; }

        /// <summary>Gets the division (for example "Central" in the NHL); none when the league has none, and
        /// then left out of the written JSON.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Maybe<string> Division { get; init; }

        /// <summary>Gets the home kit colours.</summary>
        public KitColours Home { get; init; } = new();

        /// <summary>Gets the away kit colours.</summary>
        public KitColours Away { get; init; } = new();

        /// <summary>Gets the supporters' slogans for the tifo, scarves and banners (A19); none for a club file
        /// without them, and then left out of the written JSON.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Maybe<ClubSupporters> Supporters { get; init; }

        /// <summary>Gets the match squad: 4 forward lines, 3 defence pairs and 2 goalies in a full club.</summary>
        public IReadOnlyList<PlayerData> Roster { get; init; } = [];

        /// <summary>Gets the spare skaters (W86), typically an extra forward and a seventh defenceman: they dress
        /// only when a player of the <see cref="Roster"/> is out (<see cref="Season.Injuries.AbsenceTable.Dressed"/>),
        /// stepping into his slot. Empty for a club file without them, and then left out of the written JSON
        /// (<see cref="Clubs.ToJson"/>), so the replay club fingerprint of the 20 stays as it was.</summary>
        public IReadOnlyList<PlayerData> Spares { get; init; } = [];

        /// <summary>Gets every player of the club: the roster, then the spares.</summary>
        public IEnumerable<PlayerData> Everyone()
        {
            return Roster.Concat(Spares);
        }

        /// <summary>Gets how the club plays: forecheck, zone entry and power play (src/AI/Tactics).</summary>
        public ClubTactics Tactics { get; init; } = new();
    }
}
