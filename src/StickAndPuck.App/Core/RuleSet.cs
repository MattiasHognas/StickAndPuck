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

        /// <summary>Arcade: no offside, no icing, no time-outs, a lenient referee, the game's original penalty and fight rules, no team morale, no rivalries,
        /// plain faceoffs (no violations, nearest dot), netting that keeps the puck in play, no too many men call, no penalty shots, strong form swings, no dressing room, no pre-game talk, no coach's challenges, no injuries, no suspensions, no player development, no trade deadline and no playout.</summary>
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
            Morale = false,
            FaceoffViolations = false,
            SmartDots = false,
            IntentionalOffside = false,
            FormStrength = FormStrength.Strong,
            TimeOut = false,
            DressingRoom = DressingRoomStyle.Off,
            News = false,
            CoachReaction = false,
            PreGameTalk = false,
            ScoutingTips = false,
            Challenges = false,
            PuckOverGlass = false,
            OverGlassMinor = false,
            IntermissionTalk = false,
            Injuries = false,
            Suspensions = false,
            TooManyMen = TooManyMenCall.Off,
            Rivalries = false,
            Development = false,
            PenaltyShot = false,
            TradeDeadline = false,
            Playout = false,
        };

        /// <summary>The NHL: SHL rules with home choosing the shootout order, nobody shooting twice until every skater has, a fight major without ejection,
        /// 4 on 4 coincidental minors, 2 points for any win, regulation wins first on equal points, 2-2-1-1-1 playoff home ice, stronger team morale,
        /// a delay-of-game minor for a second faceoff violation, hotter rivalry games, milder player development over its longer season, two trade offers at the deadline and no relegation playout.</summary>
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
            MoraleStrength = MoraleStrength.Strong,
            FaceoffViolationMinor = true,
            NewsFrequency = NewsFrequency.Often,
            CoachTemper = CoachTemper.Strong,
            IntermissionTalkStrength = TalkStrength.Strong,
            RivalryStrength = RivalryStrength.Strong,
            DevelopmentStrength = DevelopmentStrength.Mild,
            TradeOffers = TradeFrequency.Often,
            Playout = false,
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

        /// <summary>Gets a value indicating whether each club may call one 30 second time-out a game at a stoppage, resting its skaters on the ice.</summary>
        public bool TimeOut { get; init; } = true;

        /// <summary>Gets the points a league game gives in the standings.</summary>
        public TablePoints TablePoints { get; init; } = TablePoints.ThreeTwoOne;

        /// <summary>Gets how clubs level on points are ordered in the standings.</summary>
        public TableTiebreak Tiebreak { get; init; } = TableTiebreak.GoalDifference;

        /// <summary>Gets which games of a best of seven playoff series the higher seed hosts.</summary>
        public PlayoffHomeIce PlayoffHomeIce { get; init; } = PlayoffHomeIce.Alternating;

        /// <summary>Gets a value indicating whether an SHL season's 13th and 14th play a best of seven playout
        /// beside the playoffs, its loser relegated (W76); an NHL season never has one.</summary>
        public bool Playout { get; init; } = true;

        /// <summary>Gets a value indicating whether a season keeps a team morale per club that moves with results and events.</summary>
        public bool Morale { get; init; } = true;

        /// <summary>Gets how hard team morale swings and how much it counts.</summary>
        public MoraleStrength MoraleStrength { get; init; } = MoraleStrength.Normal;

        /// <summary>Gets the factor on morale's swings and effects: 0 with <see cref="Morale"/> off, else by <see cref="MoraleStrength"/>.</summary>
        [JsonIgnore]
        public float MoraleScale =>
            !Morale
                ? 0f
                : MoraleStrength switch
                {
                    MoraleStrength.Mild => 0.5f,
                    MoraleStrength.Strong => 1.5f,
                    MoraleStrength.Normal => 1f,
                    _ => throw new NotSupportedException("no morale scale for " + MoraleStrength),
                };

        /// <summary>Gets a value indicating whether a centre who moves before the drop is waved out of the faceoff and a winger takes the draw.</summary>
        public bool FaceoffViolations { get; init; } = true;

        /// <summary>Gets a value indicating whether a club's second faceoff violation in the same faceoff is a delay-of-game minor (the NHL); otherwise it hands the draw to the other club.</summary>
        public bool FaceoffViolationMinor { get; init; }

        /// <summary>Gets a value indicating whether a stoppage's faceoff dot is chosen against the club that caused it, rather than the dot nearest the puck.</summary>
        public bool SmartDots { get; init; } = true;

        /// <summary>Gets a value indicating whether an intentional offside moves the faceoff to the offending club's own end-zone dot.</summary>
        public bool IntentionalOffside { get; init; } = true;

        /// <summary>Gets a value indicating whether a season player's recent games carry over as a hidden form nudge to their traits.</summary>
        public bool Form { get; init; } = true;

        /// <summary>Gets how hard carry-over form pushes the traits.</summary>
        public FormStrength FormStrength { get; init; } = FormStrength.Normal;

        /// <summary>Gets the factor on the form nudge for <see cref="FormStrength"/>, 0 with <see cref="Form"/> off.</summary>
        [JsonIgnore]
        public float FormScale =>
            !Form
                ? 0f
                : FormStrength switch
                {
                    FormStrength.Mild => 0.5f,
                    FormStrength.Strong => 1.5f,
                    FormStrength.Normal => 1f,
                    _ => throw new NotSupportedException("no form scale for " + FormStrength),
                };

        /// <summary>Gets a value indicating whether hard checks, blocked slap shots and fights (and, rarely, games settled on paper) injure players for 1 to 6 season games.</summary>
        public bool Injuries { get; init; } = true;

        /// <summary>Gets how often players are injured.</summary>
        public InjuryRate InjuryRate { get; init; } = InjuryRate.Normal;

        /// <summary>Gets the factor on every injury chance: 0 with <see cref="Injuries"/> off, else by <see cref="InjuryRate"/>.</summary>
        [JsonIgnore]
        public float InjuryScale =>
            !Injuries
                ? 0f
                : InjuryRate switch
                {
                    InjuryRate.Rare => 0.5f,
                    InjuryRate.Frequent => 2f,
                    InjuryRate.Normal => 1f,
                    _ => throw new NotSupportedException("no injury scale for " + InjuryRate),
                };

        /// <summary>Gets a value indicating whether a second fight in a game or a match penalty (a game misconduct, or a
        /// major and game misconduct) suspends the player for 1 to 3 of his club's next season games.</summary>
        public bool Suspensions { get; init; } = true;

        /// <summary>Gets how long suspensions are.</summary>
        public SuspensionStrength SuspensionStrength { get; init; } = SuspensionStrength.Normal;

        /// <summary>Gets the games <see cref="SuspensionStrength"/> adds to (or takes from) a suspension's offences.</summary>
        [JsonIgnore]
        public int SuspensionShift =>
            SuspensionStrength switch
            {
                SuspensionStrength.Mild => -1,
                SuspensionStrength.Strong => 1,
                SuspensionStrength.Normal => 0,
                _ => throw new NotSupportedException(
                    "no suspension shift for " + SuspensionStrength
                ),
            };

        /// <summary>Gets how much the dressing room between season games shows, or Off to go straight to the table.</summary>
        public DressingRoomStyle DressingRoom { get; init; } = DressingRoomStyle.Full;

        /// <summary>Gets a value indicating whether season news events (flu, a rookie call-up, a travel delay and more) are drawn between the human's games.</summary>
        public bool News { get; init; } = true;

        /// <summary>Gets how often a season news event comes up.</summary>
        public NewsFrequency NewsFrequency { get; init; } = NewsFrequency.Normal;

        /// <summary>Gets the odds of a news event after a game, one in this many: 0 with <see cref="News"/> off.</summary>
        [JsonIgnore]
        public int NewsEvery =>
            !News
                ? 0
                : NewsFrequency switch
                {
                    NewsFrequency.Rare => 8,
                    NewsFrequency.Often => 3,
                    NewsFrequency.Normal => 5,
                    _ => throw new NotSupportedException("no news odds for " + NewsFrequency),
                };

        /// <summary>Gets a value indicating whether the coach reacts in the dressing room to what happened in the
        /// human's last game (a hat trick, a blown lead, a shutout, a lost fight and more), naming the player.</summary>
        public bool CoachReaction { get; init; } = true;

        /// <summary>Gets how readily the coach reacts: how big a lead, a loss or a pile of saves or penalties it takes.</summary>
        public CoachTemper CoachTemper { get; init; } = CoachTemper.Normal;

        /// <summary>Gets a value indicating whether the coach gives a pre-game talk before a season game played from the dressing room, its tone changing how the club plays that match.</summary>
        public bool PreGameTalk { get; init; } = true;

        /// <summary>Gets how much the pre-game talk counts.</summary>
        public TalkStrength TalkStrength { get; init; } = TalkStrength.Normal;

        /// <summary>Gets the factor on the pre-game talk's effects for <see cref="TalkStrength"/>, 0 with <see cref="PreGameTalk"/> off.</summary>
        [JsonIgnore]
        public float TalkScale =>
            !PreGameTalk
                ? 0f
                : TalkStrength switch
                {
                    TalkStrength.Mild => 0.5f,
                    TalkStrength.Strong => 1.5f,
                    TalkStrength.Normal => 1f,
                    _ => throw new NotSupportedException("no talk scale for " + TalkStrength),
                };

        /// <summary>Gets a value indicating whether the coach's pre-game talk includes a scouting tip about tonight's
        /// opponent (its forecheck, entries, style, power play, top scorer or a strength gap), read from the club's real data.</summary>
        public bool ScoutingTips { get; init; } = true;

        /// <summary>Gets a value indicating whether the coach sets short three-game challenges in a season (win two of
        /// three, a shutout, ten goals, beat the rival), lifting team morale when met.</summary>
        public bool Challenges { get; init; } = true;

        /// <summary>Gets how often a coach's challenge comes up.</summary>
        public ChallengeFrequency ChallengeFrequency { get; init; } = ChallengeFrequency.Normal;

        /// <summary>Gets the human's games between one challenge ending and the next being set: 0 with <see cref="Challenges"/> off.</summary>
        [JsonIgnore]
        public int ChallengeGap =>
            !Challenges
                ? 0
                : ChallengeFrequency switch
                {
                    ChallengeFrequency.Rare => 12,
                    ChallengeFrequency.Often => 5,
                    ChallengeFrequency.Normal => 8,
                    _ => throw new NotSupportedException(
                        "no challenge gap for " + ChallengeFrequency
                    ),
                };

        /// <summary>Gets a value indicating whether a puck flying over the glass leaves the rink and stops play, with the faceoff where it was last played; otherwise netting keeps it in play.</summary>
        public bool PuckOverGlass { get; init; } = true;

        /// <summary>Gets a value indicating whether a skater who shoots or clears the puck directly over the glass from his own defending zone takes a delay-of-game minor (not for a deflection); needs <see cref="PuckOverGlass"/>.</summary>
        public bool OverGlassMinor { get; init; } = true;

        /// <summary>Gets a value indicating whether a human club trailing after period 1 or 2 gets a one-choice talk
        /// in the Zamboni break (push or stay patient) that changes its shifts and aggression in the next period.
        /// Plays in any match with a Zamboni break, not only in a season.</summary>
        public bool IntermissionTalk { get; init; } = true;

        /// <summary>Gets how much the intermission talk changes the next period.</summary>
        public TalkStrength IntermissionTalkStrength { get; init; } = TalkStrength.Normal;

        /// <summary>Gets the factor on the intermission talk's changes for <see cref="IntermissionTalkStrength"/>, 0 with <see cref="IntermissionTalk"/> off.</summary>
        [JsonIgnore]
        public float IntermissionTalkScale =>
            !IntermissionTalk
                ? 0f
                : IntermissionTalkStrength switch
                {
                    TalkStrength.Mild => 0.5f,
                    TalkStrength.Strong => 1.5f,
                    TalkStrength.Normal => 1f,
                    _ => throw new NotSupportedException(
                        "no intermission talk scale for " + IntermissionTalkStrength
                    ),
                };

        /// <summary>Gets how a rushed human line change that steps on too early is called: a bench minor for too many men on the ice, or Off.</summary>
        public TooManyMenCall TooManyMen { get; init; } = TooManyMenCall.Lenient;

        /// <summary>Gets a value indicating whether a game between rival clubs (data/season/{league}/rivals.json) gets its banner, more checking and fights, and a bigger morale swing.</summary>
        public bool Rivalries { get; init; } = true;

        /// <summary>Gets how much hotter a rivalry game is.</summary>
        public RivalryStrength RivalryStrength { get; init; } = RivalryStrength.Normal;

        /// <summary>Gets a value indicating whether AI clubs low in the table make player-for-player trade offers to the
        /// human's club at the season's trade deadline, two thirds of the way through (W135).</summary>
        public bool TradeDeadline { get; init; } = true;

        /// <summary>Gets how many trade offers come at the deadline: one, one or two, or two.</summary>
        public TradeFrequency TradeOffers { get; init; } = TradeFrequency.Normal;

        /// <summary>Gets the factor on what a rivalry game adds, 0 with <see cref="Rivalries"/> off, else by <see cref="RivalryStrength"/>.</summary>
        [JsonIgnore]
        public float RivalryScale =>
            !Rivalries
                ? 0f
                : RivalryStrength switch
                {
                    RivalryStrength.Mild => 0.5f,
                    RivalryStrength.Strong => 1.5f,
                    RivalryStrength.Normal => 1f,
                    _ => throw new NotSupportedException("no rivalry scale for " + RivalryStrength),
                };

        /// <summary>Gets a value indicating whether season players develop with age: young players gain trait points from
        /// ice time and form over the season, veterans may lose some (W132).</summary>
        public bool Development { get; init; } = true;

        /// <summary>Gets how fast season players develop and decline.</summary>
        public DevelopmentStrength DevelopmentStrength { get; init; } = DevelopmentStrength.Normal;

        /// <summary>Gets the factor on development's trait points for <see cref="DevelopmentStrength"/>, 0 with <see cref="Development"/> off.</summary>
        [JsonIgnore]
        public float DevelopmentScale =>
            !Development
                ? 0f
                : DevelopmentStrength switch
                {
                    DevelopmentStrength.Mild => 0.5f,
                    DevelopmentStrength.Strong => 1.5f,
                    DevelopmentStrength.Normal => 1f,
                    _ => throw new NotSupportedException(
                        "no development scale for " + DevelopmentStrength
                    ),
                };

        /// <summary>Gets a value indicating whether a trip, hook or slash from behind on a skater on a clean breakaway
        /// (past the last defender in the attacking half, only the goalie to beat) is a penalty shot instead of a minor.</summary>
        public bool PenaltyShot { get; init; } = true;

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
