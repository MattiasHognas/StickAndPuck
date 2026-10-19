using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Overtime;
using StickAndPuck.App.Season.Injuries;
using StickAndPuck.App.Season.Stats;

namespace StickAndPuck.App.Replay
{
    /// <summary>The setup a match started from, with the clubs by id, as a replay stores it.</summary>
    public sealed record ReplaySetup(
        string Home,
        string Away,
        IReadOnlyList<InputSlot> Inputs,
        float PeriodSeconds,
        int Periods,
        OvertimeRule Overtime,
        Maybe<OvertimeFormat> OvertimeFormat,
        RuleSet Rules,
        Maybe<MatchMorale> Morale = default
    )
    {
        /// <summary>Gets the season's carry-over form nudges, as <see cref="MatchSetup.Form"/>; empty in an older replay.</summary>
        public IReadOnlyDictionary<string, int> Form { get; init; } =
            new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>Gets the energy each club started with, as <see cref="MatchSetup.StartEnergy"/>; full in an older replay.</summary>
        public IReadOnlyList<float> StartEnergy { get; init; } = [1f, 1f];

        /// <summary>Gets the coach's pre-game talk, as <see cref="MatchSetup.Talk"/>; None in an older replay.</summary>
        public Maybe<CoachTalk> Talk { get; init; }

        /// <summary>Gets the players the season left out of the match, as <see cref="MatchSetup.Absent"/>; empty in an older replay.</summary>
        public IReadOnlyList<string> Absent { get; init; } = [];

        /// <summary>Gets a value indicating whether the match is a rivalry game, as <see cref="MatchSetup.Rivalry"/>; false in an older replay.</summary>
        public bool Rivalry { get; init; }

        /// <summary>Gets the stored form of a setup.</summary>
        public static ReplaySetup Of(MatchSetup s)
        {
            return new(
                s.Home.Id,
                s.Away.Id,
                s.Inputs,
                s.PeriodSeconds,
                s.Periods,
                s.Overtime,
                s.OvertimeFormat,
                s.Rules,
                s.Morale
            )
            {
                Form = s.Form,
                StartEnergy = s.StartEnergy,
                Talk = s.Talk,
                Absent = s.Absent,
                Rivalry = s.Rivalry,
            };
        }

        /// <summary>Builds the setup again from the clubs on this machine.</summary>
        public MatchSetup ToSetup(IReadOnlyList<ClubData> clubs)
        {
            ClubData Club(string id)
            {
                return clubs.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal))
                    ?? Clubs
                        .LoadNhl()
                        .FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal))
                    ?? throw new InvalidOperationException(
                        $"the replay's club {id} is not in data/clubs"
                    );
            }

            return new MatchSetup
            {
                Home = FormTable.Apply(AbsenceTable.Without(Club(Home), Absent), Form),
                Away = FormTable.Apply(AbsenceTable.Without(Club(Away), Absent), Form),
                Form = Form,
                Absent = Absent,
                Inputs = Inputs,
                PeriodSeconds = PeriodSeconds,
                Periods = Periods,
                Overtime = Overtime,
                OvertimeFormat = OvertimeFormat,
                Rules = Rules,
                Morale = Morale,
                StartEnergy = StartEnergy,
                Talk = Talk,
                Rivalry = Rivalry,
            };
        }
    }
}
