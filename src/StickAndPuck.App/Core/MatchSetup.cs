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
