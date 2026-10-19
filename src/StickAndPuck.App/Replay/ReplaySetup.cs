using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Overtime;
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
                Home = FormTable.Apply(Club(Home), Form),
                Away = FormTable.Apply(Club(Away), Form),
                Form = Form,
                Inputs = Inputs,
                PeriodSeconds = PeriodSeconds,
                Periods = Periods,
                Overtime = Overtime,
                OvertimeFormat = OvertimeFormat,
                Rules = Rules,
                Morale = Morale,
            };
        }
    }
}
