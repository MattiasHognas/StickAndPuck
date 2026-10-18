using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.Schedule
{
    /// <summary>A played match's final score, shootout goal included, and how it was decided.</summary>
    public sealed record FixtureResult(int HomeGoals, int AwayGoals, ResultKind Kind)
    {
        /// <summary>Gets the SHL points for the home club.</summary>
        public int HomePoints => Points(HomeGoals, AwayGoals, TablePoints.ThreeTwoOne);

        /// <summary>Gets the SHL points for the away club.</summary>
        public int AwayPoints => Points(AwayGoals, HomeGoals, TablePoints.ThreeTwoOne);

        /// <summary>Gets the points one club takes under a points system: 3-2-1-0 (SHL) or 2-2-1-0 (NHL).</summary>
        public int PointsFor(bool home, TablePoints system)
        {
            return home
                ? Points(HomeGoals, AwayGoals, system)
                : Points(AwayGoals, HomeGoals, system);
        }

        private int Points(int own, int other, TablePoints system)
        {
            if (own == other)
            {
                return 1;
            }

            bool extra = Kind != ResultKind.Regulation;
            int win = (system == TablePoints.TwoTwoOne || extra) ? 2 : 3;

            return (own > other) ? win : (extra ? 1 : 0);
        }
    }
}
