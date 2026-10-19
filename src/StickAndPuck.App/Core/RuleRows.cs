using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Core
{
    /// <summary>Every rule switch, in page order: Game (offside, icing, goalie pull), Overtime, Penalties,
    /// Infractions, Faceoffs, Fights, League (table points, tiebreak and playoff home ice) and Season (team morale, carry-over form). <see cref="RuleRow.Plays"/> says whether the game plays a row's rule.</summary>
    public static class RuleRows
    {
        private const string On = "On",
            Off = "Off";

        /// <summary>Gets every rule, shown or not.</summary>
        public static IReadOnlyList<RuleRow> All { get; } =
        [
            Flag(
                nameof(RuleSet.Offside),
                K.RuleOffside,
                K.RuleGroupGame,
                plays: true,
                r => r.Offside,
                (r, v) => r with { Offside = v }
            ),
            Choice(
                nameof(RuleSet.Icing),
                K.RuleIcing,
                K.RuleGroupGame,
                plays: true,
                Enum.GetValues<IcingCall>(),
                r => r.Icing,
                (r, v) => r with { Icing = v }
            ),
            Flag(
                nameof(RuleSet.GoaliePull),
                K.RuleGoaliePull,
                K.RuleGroupGame,
                plays: true,
                r => r.GoaliePull,
                (r, v) => r with { GoaliePull = v }
            ),
            Choice(
                nameof(RuleSet.Overtime),
                K.RuleOvertime,
                K.RuleGroupOvertime,
                plays: true,
                Enum.GetValues<OvertimeStyle>(),
                r => r.Overtime,
                (r, v) => r with { Overtime = v }
            ),
            Choice(
                nameof(RuleSet.PlayoffOvertime),
                K.RulePlayoffOvertime,
                K.RuleGroupOvertime,
                plays: true,
                [OvertimeStyle.SuddenDeath, OvertimeStyle.ThreeOnThree, OvertimeStyle.ShootoutOnly],
                r => r.PlayoffOvertime,
                (r, v) => r with { PlayoffOvertime = v }
            ),
            new(
                nameof(RuleSet.ShootoutShooters),
                K.RuleShootoutShooters,
                K.RuleGroupOvertime,
                Plays: true,
                ["3", "5"],
                r => r.ShootoutShooters.ToString(System.Globalization.CultureInfo.InvariantCulture),
                (r, v) =>
                    r with
                    {
                        ShootoutShooters = int.Parse(
                            v,
                            System.Globalization.CultureInfo.InvariantCulture
                        ),
                    }
            ),
            Choice(
                nameof(RuleSet.ShootoutFirst),
                K.RuleShootoutFirst,
                K.RuleGroupOvertime,
                plays: true,
                Enum.GetValues<ShootoutFirst>(),
                r => r.ShootoutFirst,
                (r, v) => r with { ShootoutFirst = v }
            ),
            Flag(
                nameof(RuleSet.ShootoutRepeat),
                K.RuleShootoutRepeat,
                K.RuleGroupOvertime,
                plays: true,
                r => r.ShootoutRepeat,
                (r, v) => r with { ShootoutRepeat = v }
            ),
            Flag(
                nameof(RuleSet.Penalties),
                K.RulePenalties,
                K.RuleGroupPenalties,
                plays: true,
                r => r.Penalties,
                (r, v) => r with { Penalties = v }
            ),
            Choice(
                nameof(RuleSet.Strictness),
                K.RuleStrictness,
                K.RuleGroupPenalties,
                plays: true,
                Enum.GetValues<RefereeStrictness>(),
                r => r.Strictness,
                (r, v) => r with { Strictness = v }
            ),
            Flag(
                nameof(RuleSet.PenaltiesCarryIntoOvertime),
                K.RulePenaltiesCarryIntoOvertime,
                K.RuleGroupPenalties,
                plays: true,
                r => r.PenaltiesCarryIntoOvertime,
                (r, v) => r with { PenaltiesCarryIntoOvertime = v }
            ),
            Flag(
                nameof(RuleSet.StackPenalties),
                K.RuleStackPenalties,
                K.RuleGroupPenalties,
                plays: true,
                r => r.StackPenalties,
                (r, v) => r with { StackPenalties = v }
            ),
            Choice(
                nameof(RuleSet.CoincidentalMinors),
                K.RuleCoincidentalMinors,
                K.RuleGroupPenalties,
                plays: true,
                Enum.GetValues<CoincidentalMinors>(),
                r => r.CoincidentalMinors,
                (r, v) => r with { CoincidentalMinors = v }
            ),
            Choice(
                nameof(RuleSet.PenaltySeverity),
                K.RulePenaltySeverity,
                K.RuleGroupPenalties,
                plays: true,
                Enum.GetValues<PenaltySeverity>(),
                r => r.PenaltySeverity,
                (r, v) => r with { PenaltySeverity = v }
            ),
            Flag(
                nameof(RuleSet.SpecialTeams),
                K.RuleSpecialTeams,
                K.RuleGroupPenalties,
                plays: true,
                r => r.SpecialTeams,
                (r, v) => r with { SpecialTeams = v }
            ),
            Flag(
                nameof(RuleSet.Interference),
                K.RuleInterference,
                K.RuleGroupInfractions,
                plays: true,
                r => r.Interference,
                (r, v) => r with { Interference = v }
            ),
            Flag(
                nameof(RuleSet.Holding),
                K.RuleHolding,
                K.RuleGroupInfractions,
                plays: true,
                r => r.Holding,
                (r, v) => r with { Holding = v }
            ),
            Flag(
                nameof(RuleSet.HighSticking),
                K.RuleHighSticking,
                K.RuleGroupInfractions,
                plays: true,
                r => r.HighSticking,
                (r, v) => r with { HighSticking = v }
            ),
            Flag(
                nameof(RuleSet.CrossChecking),
                K.RuleCrossChecking,
                K.RuleGroupInfractions,
                plays: true,
                r => r.CrossChecking,
                (r, v) => r with { CrossChecking = v }
            ),
            Flag(
                nameof(RuleSet.Boarding),
                K.RuleBoarding,
                K.RuleGroupInfractions,
                plays: true,
                r => r.Boarding,
                (r, v) => r with { Boarding = v }
            ),
            Flag(
                nameof(RuleSet.Roughing),
                K.RuleRoughing,
                K.RuleGroupInfractions,
                plays: true,
                r => r.Roughing,
                (r, v) => r with { Roughing = v }
            ),
            Flag(
                nameof(RuleSet.IcingNoChange),
                K.RuleIcingNoChange,
                K.RuleGroupGame,
                plays: true,
                r => r.IcingNoChange,
                (r, v) => r with { IcingNoChange = v }
            ),
            Choice(
                nameof(RuleSet.Intermission),
                K.RuleIntermission,
                K.RuleGroupGame,
                plays: true,
                Enum.GetValues<IntermissionLength>(),
                r => r.Intermission,
                (r, v) => r with { Intermission = v }
            ),
            Flag(
                nameof(RuleSet.FaceoffViolations),
                K.RuleFaceoffViolations,
                K.RuleGroupFaceoffs,
                plays: true,
                r => r.FaceoffViolations,
                (r, v) => r with { FaceoffViolations = v }
            ),
            Flag(
                nameof(RuleSet.FaceoffViolationMinor),
                K.RuleFaceoffViolationMinor,
                K.RuleGroupFaceoffs,
                plays: true,
                r => r.FaceoffViolationMinor,
                (r, v) => r with { FaceoffViolationMinor = v }
            ),
            Flag(
                nameof(RuleSet.SmartDots),
                K.RuleSmartDots,
                K.RuleGroupFaceoffs,
                plays: true,
                r => r.SmartDots,
                (r, v) => r with { SmartDots = v }
            ),
            Flag(
                nameof(RuleSet.IntentionalOffside),
                K.RuleIntentionalOffside,
                K.RuleGroupFaceoffs,
                plays: true,
                r => r.IntentionalOffside,
                (r, v) => r with { IntentionalOffside = v }
            ),
            Flag(
                nameof(RuleSet.TimeOut),
                K.RuleTimeOut,
                K.RuleGroupGame,
                plays: true,
                r => r.TimeOut,
                (r, v) => r with { TimeOut = v }
            ),
            Flag(
                nameof(RuleSet.Fights),
                K.RuleFights,
                K.RuleGroupFights,
                plays: true,
                r => r.Fights,
                (r, v) => r with { Fights = v }
            ),
            Choice(
                nameof(RuleSet.FightPenalty),
                K.RuleFightPenalty,
                K.RuleGroupFights,
                plays: true,
                Enum.GetValues<FightPenalty>(),
                r => r.FightPenalty,
                (r, v) => r with { FightPenalty = v }
            ),
            Choice(
                nameof(RuleSet.TablePoints),
                K.RuleTablePoints,
                K.RuleGroupLeague,
                plays: true,
                Enum.GetValues<TablePoints>(),
                r => r.TablePoints,
                (r, v) => r with { TablePoints = v }
            ),
            Choice(
                nameof(RuleSet.Tiebreak),
                K.RuleTiebreak,
                K.RuleGroupLeague,
                plays: true,
                Enum.GetValues<TableTiebreak>(),
                r => r.Tiebreak,
                (r, v) => r with { Tiebreak = v }
            ),
            Choice(
                nameof(RuleSet.PlayoffHomeIce),
                K.RulePlayoffHomeIce,
                K.RuleGroupLeague,
                plays: true,
                Enum.GetValues<PlayoffHomeIce>(),
                r => r.PlayoffHomeIce,
                (r, v) => r with { PlayoffHomeIce = v }
            ),
            Flag(
                nameof(RuleSet.Morale),
                K.RuleMorale,
                K.RuleGroupSeason,
                plays: true,
                r => r.Morale,
                (r, v) => r with { Morale = v }
            ),
            Choice(
                nameof(RuleSet.MoraleStrength),
                K.RuleMoraleStrength,
                K.RuleGroupSeason,
                plays: true,
                Enum.GetValues<MoraleStrength>(),
                r => r.MoraleStrength,
                (r, v) => r with { MoraleStrength = v }
            ),
            Flag(
                nameof(RuleSet.Form),
                K.RuleForm,
                K.RuleGroupSeason,
                plays: true,
                r => r.Form,
                (r, v) => r with { Form = v }
            ),
            Choice(
                nameof(RuleSet.FormStrength),
                K.RuleFormStrength,
                K.RuleGroupSeason,
                plays: true,
                Enum.GetValues<FormStrength>(),
                r => r.FormStrength,
                (r, v) => r with { FormStrength = v }
            ),
            Choice(
                nameof(RuleSet.DressingRoom),
                K.RuleDressingRoom,
                K.RuleGroupSeason,
                plays: true,
                Enum.GetValues<DressingRoomStyle>(),
                r => r.DressingRoom,
                (r, v) => r with { DressingRoom = v }
            ),
            Flag(
                nameof(RuleSet.News),
                K.RuleNews,
                K.RuleGroupSeason,
                plays: true,
                r => r.News,
                (r, v) => r with { News = v }
            ),
            Choice(
                nameof(RuleSet.NewsFrequency),
                K.RuleNewsFrequency,
                K.RuleGroupSeason,
                plays: true,
                Enum.GetValues<NewsFrequency>(),
                r => r.NewsFrequency,
                (r, v) => r with { NewsFrequency = v }
            ),
            Flag(
                nameof(RuleSet.CoachReaction),
                K.RuleCoachReaction,
                K.RuleGroupSeason,
                plays: true,
                r => r.CoachReaction,
                (r, v) => r with { CoachReaction = v }
            ),
            Choice(
                nameof(RuleSet.CoachTemper),
                K.RuleCoachTemper,
                K.RuleGroupSeason,
                plays: true,
                Enum.GetValues<CoachTemper>(),
                r => r.CoachTemper,
                (r, v) => r with { CoachTemper = v }
            ),
        ];

        /// <summary>Gets the rules the game plays, grouped as the rules page shows them.</summary>
        public static IReadOnlyList<RuleRow> Shown =>
            [
                .. All.Where(r => r.Plays)
                    .GroupBy(r => r.Group, StringComparer.Ordinal)
                    .SelectMany(g => g),
            ];

        private static RuleRow Flag(
            string key,
            string label,
            string group,
            bool plays,
            Func<RuleSet, bool> get,
            Func<RuleSet, bool, RuleSet> set
        )
        {
            return new RuleRow(
                key,
                label,
                group,
                plays,
                [On, Off],
                r => get(r) ? On : Off,
                (r, v) => set(r, string.Equals(v, On, StringComparison.Ordinal))
            );
        }

        private static RuleRow Choice<T>(
            string key,
            string label,
            string group,
            bool plays,
            IReadOnlyList<T> values,
            Func<RuleSet, T> get,
            Func<RuleSet, T, RuleSet> set
        )
            where T : struct, Enum
        {
            return new RuleRow(
                key,
                label,
                group,
                plays,
                [.. values.Select(v => v.ToString())],
                r => get(r).ToString(),
                (r, v) => set(r, Enum.Parse<T>(v))
            );
        }
    }
}
