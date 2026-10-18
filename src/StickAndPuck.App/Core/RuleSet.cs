using System.Text.Json.Serialization;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Core
{
    /// <summary>Every game rule as a switch, so a league is a preset and a player can turn any rule off.
    /// A match carries its resolved set on <see cref="MatchSetup.Rules"/>; systems read
    /// <c language="C#">world.Setup.Rules.X</c> and never a preset name. Presets: <see cref="Shl"/> (the default),
    /// <see cref="Arcade"/> and <see cref="Nhl"/>. A player's changes are a <see cref="RuleChoice"/>
    /// saved in settings.json and resolved once when a match starts; the arcade entries always play
    /// <see cref="Arcade"/> as it stands, and a season keeps the set it was started with in its save.
    /// The rows on the rules page, with their keys, are <see cref="RuleRows.All"/>.</summary>
    public sealed record RuleSet
    {
        /// <summary>The SHL: the rules the game was built on.</summary>
        public static readonly RuleSet Shl = new();

        /// <summary>Arcade: no offside, no icing, a lenient referee and the game's original penalty and fight rules.</summary>
        public static readonly RuleSet Arcade = new()
        {
            Preset = RulePreset.Arcade,
            Offside = false,
            Icing = IcingCall.Off,
            Strictness = RefereeStrictness.Lenient,
            PenaltiesCarryIntoOvertime = false,
            StackPenalties = false,
            PenaltySeverity = PenaltySeverity.MinorsOnly,
            Interference = false,
            Holding = false,
            HighSticking = false,
            CrossChecking = false,
            Boarding = false,
            Roughing = false,
            FightPenalty = FightPenalty.LoserMinor,
            IcingNoChange = false,
            Intermission = IntermissionLength.Quick,
        };

        /// <summary>The NHL: SHL rules with home choosing the shootout order, nobody shooting twice until every skater has, a fight major without ejection,
        /// 4 on 4 coincidental minors, 2 points for any win, regulation wins first on equal points and 2-2-1-1-1 playoff home ice.</summary>
        public static readonly RuleSet Nhl = new()
        {
            Preset = RulePreset.Nhl,
            ShootoutFirst = ShootoutFirst.HomeChooses,
            ShootoutRepeat = false,
            FightPenalty = FightPenalty.Major,
            CoincidentalMinors = CoincidentalMinors.FourOnFour,
            TablePoints = TablePoints.TwoTwoOne,
            Tiebreak = TableTiebreak.RegulationWins,
            PlayoffHomeIce = PlayoffHomeIce.TwoTwoOneOneOne,
        };

        /// <summary>Gets the preset this set started from.</summary>
        public RulePreset Preset { get; init; } = RulePreset.Shl;

        /// <summary>Gets a value indicating whether the player changed a rule of the preset (shown as CUSTOM).</summary>
        public bool Custom { get; init; }

        /// <summary>Gets a value indicating whether offside is called.</summary>
        public bool Offside { get; init; } = true;

        /// <summary>Gets how icing is called.</summary>
        public IcingCall Icing { get; init; } = IcingCall.Hybrid;

        /// <summary>Gets a value indicating whether a club may pull its goalie for an extra attacker.</summary>
        public bool GoaliePull { get; init; } = true;

        /// <summary>Gets how a tie is settled in a season, exhibition, versus or arcade game.</summary>
        public OvertimeStyle Overtime { get; init; } = OvertimeStyle.ThreeOnThree;

        /// <summary>Gets how a tie is settled in a playoff game.</summary>
        public OvertimeStyle PlayoffOvertime { get; init; } = OvertimeStyle.SuddenDeath;

        /// <summary>Gets a value indicating whether the referee calls penalties.</summary>
        public bool Penalties { get; init; } = true;

        /// <summary>Gets how readily the referee calls a foul.</summary>
        public RefereeStrictness Strictness { get; init; } = RefereeStrictness.Normal;

        /// <summary>Gets a value indicating whether fights can start.</summary>
        public bool Fights { get; init; } = true;

        /// <summary>Gets the shooters per club before the shootout goes to sudden death.</summary>
        public int ShootoutShooters { get; init; } = 3;

        /// <summary>Gets who shoots first in the shootout.</summary>
        public ShootoutFirst ShootoutFirst { get; init; } = ShootoutFirst.Away;

        /// <summary>Gets a value indicating whether the shootout's opening shooters may shoot again in sudden death
        /// (SHL and Arcade); off, nobody shoots twice until every skater of the club has (NHL).</summary>
        public bool ShootoutRepeat { get; init; } = true;

        /// <summary>Gets a value indicating whether penalties still running at the end of regulation carry into overtime.</summary>
        public bool PenaltiesCarryIntoOvertime { get; init; } = true;

        /// <summary>Gets a value indicating whether a third penalty waits in the box instead of being waved off.</summary>
        public bool StackPenalties { get; init; } = true;

        /// <summary>Gets what equal minors on both clubs do.</summary>
        public CoincidentalMinors CoincidentalMinors { get; init; } = CoincidentalMinors.Substitute;

        /// <summary>Gets which penalty lengths are handed out.</summary>
        public PenaltySeverity PenaltySeverity { get; init; } = PenaltySeverity.Full;

        /// <summary>Gets a value indicating whether the referee calls interference: a hit on a skater without the puck.</summary>
        public bool Interference { get; init; } = true;

        /// <summary>Gets a value indicating whether the referee calls holding: close trailing contact on a slowed carrier.</summary>
        public bool Holding { get; init; } = true;

        /// <summary>Gets a value indicating whether the referee calls high-sticking: the stick up on a face-to-face check.</summary>
        public bool HighSticking { get; init; } = true;

        /// <summary>Gets a value indicating whether the referee calls cross-checking: a hard hit from the front on the carrier.</summary>
        public bool CrossChecking { get; init; } = true;

        /// <summary>Gets a value indicating whether the referee calls boarding: a hard hit from behind near the boards.</summary>
        public bool Boarding { get; init; } = true;

        /// <summary>Gets a value indicating whether the referee calls roughing: a hit after the whistle, or the same skater hit again at once.</summary>
        public bool Roughing { get; init; } = true;

        /// <summary>Gets what a fight costs.</summary>
        public FightPenalty FightPenalty { get; init; } = FightPenalty.MajorAndEjection;

        /// <summary>Gets a value indicating whether the icing club keeps its line until the faceoff.</summary>
        public bool IcingNoChange { get; init; } = true;

        /// <summary>Gets a value indicating whether the AI plays its power-play and penalty-kill shapes; off, it keeps its even-strength positioning a man up or down.</summary>
        public bool SpecialTeams { get; init; } = true;

        /// <summary>Gets how long the Zamboni break between periods lasts; anything but Off also breaks before a playoff overtime.</summary>
        public IntermissionLength Intermission { get; init; } = IntermissionLength.Standard;

        /// <summary>Gets the points a league game gives in the standings.</summary>
        public TablePoints TablePoints { get; init; } = TablePoints.ThreeTwoOne;

        /// <summary>Gets how clubs level on points are ordered in the standings.</summary>
        public TableTiebreak Tiebreak { get; init; } = TableTiebreak.GoalDifference;

        /// <summary>Gets which games of a best of seven playoff series the higher seed hosts.</summary>
        public PlayoffHomeIce PlayoffHomeIce { get; init; } = PlayoffHomeIce.Alternating;

        /// <summary>Gets the factor on the call chance for <see cref="Strictness"/>.</summary>
        [JsonIgnore]
        public float StrictnessScale =>
            Strictness switch
            {
                RefereeStrictness.Lenient => 0.5f,
                RefereeStrictness.Strict => 1.5f,
                RefereeStrictness.Normal => 1f,
                _ => throw new NotSupportedException("no strictness scale for " + Strictness),
            };

        /// <summary>Gets the name shown for this set: the preset's, or CUSTOM once a rule was changed.</summary>
        [JsonIgnore]
        public string Name => Custom ? Text.Get(K.RulesCustom) : NameOf(Preset);

        /// <summary>Gets a preset's rules.</summary>
        public static RuleSet Of(RulePreset preset)
        {
            return preset switch
            {
                RulePreset.Arcade => Arcade,
                RulePreset.Nhl => Nhl,
                RulePreset.Shl => Shl,
                _ => throw new NotSupportedException($"Preset {preset} is not supported in rules"),
            };
        }

        /// <summary>Gets a preset's name, in capitals.</summary>
        public static string NameOf(RulePreset preset)
        {
            return preset.ToString().ToUpperInvariant();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{Name} rules ({string.Join(", ", RuleRows.All.Select(r => $"{r.Key} {r.Get(this)}"))})";
        }
    }
}
