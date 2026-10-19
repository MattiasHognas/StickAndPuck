using StickAndPuck.App.Rules.Overtime;

namespace StickAndPuck.App.Core
{
    /// <summary>Everything needed to start a match, so season, arcade and player vs player modes all start one
    /// the same way: the two clubs, who is human, period length, the overtime rule and the rules.</summary>
    public sealed record MatchSetup
    {
        /// <summary>The number of human input slots. Two from the start, so player vs player needs no rework.</summary>
        public const int Slots = 2;

        /// <summary>Gets the home club.</summary>
        public required ClubData Home { get; init; }

        /// <summary>Gets the visiting club.</summary>
        public required ClubData Away { get; init; }

        /// <summary>Gets the two human input slots; a slot with <see cref="InputDevice.None"/> is unused.</summary>
        public IReadOnlyList<InputSlot> Inputs { get; init; } =
        [
            new InputSlot(InputDevice.KeyboardMouse, Side.Home),
            new InputSlot(InputDevice.None, Side.Away),
        ];

        /// <summary>Gets the length of one period in game seconds.</summary>
        public float PeriodSeconds { get; init; } = 20 * 60;

        /// <summary>Gets the number of regulation periods.</summary>
        public int Periods { get; init; } = 3;

        /// <summary>Gets how a tie after regulation is settled.</summary>
        public OvertimeRule Overtime { get; init; } = OvertimeRule.Shl;

        /// <summary>Gets the mode's own overtime rules, or None to play the format <see cref="Overtime"/> names.</summary>
        public Maybe<OvertimeFormat> OvertimeFormat { get; init; }

        /// <summary>Gets the rules the match is played by, resolved when it starts; systems read their switches here.</summary>
        public RuleSet Rules { get; init; } = RuleSet.Shl;

        /// <summary>Gets both clubs' team morale as a season game starts, or None outside a season; the momentum meter starts from it.</summary>
        public Maybe<MatchMorale> Morale { get; init; }

        /// <summary>Gets a value indicating whether the two clubs are rivals (<see cref="Rivals"/>), decided when the
        /// match is set up and kept in its replay; <see cref="Rivals.Playing"/> also asks the rules.</summary>
        public bool Rivalry { get; init; }

        /// <summary>Gets the carry-over form nudges a season match plays with, trait points by "club#number", with
        /// the season news events' trait points added; empty outside the season. <see cref="Home"/> and <see cref="Away"/> already carry them in their traits;
        /// a replay stores these to build the same clubs again.</summary>
        public IReadOnlyDictionary<string, int> Form { get; init; } =
            new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>Gets the energy, 0 to 1, each club's players start the match with, home then away: 1, or
        /// less for a club tired from a season news event (a travel delay).</summary>
        public IReadOnlyList<float> StartEnergy { get; init; } = [1f, 1f];

        /// <summary>Gets the energy a club's players start the match with.</summary>
        public float StartEnergyOf(Side side)
        {
            return StartEnergy.Count > (int)side ? StartEnergy[(int)side] : 1f;
        }

        /// <summary>Gets the coach's pre-game talk the human's club heard before a season game, or None
        /// (<see cref="CoachTalk"/> reads its effects); a replay stores it.</summary>
        public Maybe<CoachTalk> Talk { get; init; }

        /// <summary>Gets the players left out of a season match, injured or suspended, by "club#number"; empty outside
        /// the season. <see cref="Home"/> and <see cref="Away"/> are already without them; a replay stores
        /// these to build the same clubs again.</summary>
        public IReadOnlyList<string> Absent { get; init; } = [];

        /// <summary>This setup played by a rule set: the set goes on <see cref="Rules"/>, and its overtime
        /// switch picks the format. A setup whose mode pinned its own <see cref="OvertimeFormat"/> is a
        /// playoff game and takes <see cref="RuleSet.PlayoffOvertime"/>, keeping the mode's own
        /// format for sudden death; every other game takes <see cref="RuleSet.Overtime"/>. A setup
        /// whose tie stands (<see cref="OvertimeRule.None"/>, the test scenes) keeps it.</summary>
        public MatchSetup WithRules(RuleSet rules)
        {
            MatchSetup setup = this with { Rules = rules };
            return (Overtime == OvertimeRule.None && !OvertimeFormat.HasValue)
                ? setup
                : (
                    OvertimeFormat.TryGet(out OvertimeFormat own)
                        ? (
                            setup with
                            {
                                OvertimeFormat =
                                    rules.PlayoffOvertime == OvertimeStyle.SuddenDeath
                                    && own.UntilGoal
                                        ? own
                                        : Format(rules.PlayoffOvertime),
                            }
                        )
                        : (
                            rules.Overtime switch
                            {
                                OvertimeStyle.ThreeOnThree => setup with
                                {
                                    Overtime = OvertimeRule.Shl,
                                },
                                OvertimeStyle.ShootoutOnly => setup with
                                {
                                    Overtime = OvertimeRule.ShootoutOnly,
                                },
                                OvertimeStyle.Tie => setup with { Overtime = OvertimeRule.None },
                                OvertimeStyle.SuddenDeath => setup with
                                {
                                    OvertimeFormat = App.Rules.Overtime.OvertimeFormat.ShlPlayoffs,
                                },
                                _ => throw new NotSupportedException(
                                    $"Overtime style {rules.Overtime} is not supported in match setup"
                                ),
                            }
                        )
                );
        }

        /// <summary>Gets the league the match is played in: the home club's (league choice keeps both clubs in
        /// one league). Presentation that depends on the league, such as the arena ads, reads it here.</summary>
        public League League => Home.League;

        /// <summary>Gets the club playing on the given side.</summary>
        public ClubData Club(Side side)
        {
            return side == Side.Home ? Home : Away;
        }

        private static OvertimeFormat Format(OvertimeStyle style)
        {
            return style switch
            {
                OvertimeStyle.ThreeOnThree => App.Rules.Overtime.OvertimeFormat.Shl,
                OvertimeStyle.ShootoutOnly => App.Rules.Overtime.OvertimeFormat.ShootoutOnly,
                OvertimeStyle.Tie => App.Rules.Overtime.OvertimeFormat.None,
                OvertimeStyle.SuddenDeath => App.Rules.Overtime.OvertimeFormat.ShlPlayoffs,
                _ => throw new NotSupportedException(
                    $"Overtime style {style} is not supported in match setup"
                ),
            };
        }
    }
}
