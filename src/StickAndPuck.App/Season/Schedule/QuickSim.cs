using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.Schedule
{
    /// <summary>Settles the games the human does not play from the clubs' traits, in an instant: goals are
    /// Poisson draws around about three a side, tilted by attack against defence and goalie, with a
    /// small home edge and each club's lift (its team morale, <see cref="Morale.TeamMorale.GoalLift"/>). A tie goes to 3 on 3 overtime or a shootout, which adds one goal to the winner
    /// as in a played match. The same season seed and game always give the same score.</summary>
    public static class QuickSim
    {
        private const double GoalsPerSide = 2.9;
        private const double HomeEdge = 1.06;
        private const double OvertimeGoalShare = 0.55;

        /// <summary>Plays one game on paper; each lift multiplies that club's expected goals (1 leaves them as the traits say).</summary>
        public static FixtureResult Play(
            ClubData home,
            ClubData away,
            int seed,
            double homeLift = 1.0,
            double awayLift = 1.0
        )
        {
            Random dice = Seeds.Dice(seed);
            double h = GoalsPerSide * HomeEdge * Ratio(home, away) * homeLift;
            double a = GoalsPerSide / HomeEdge * Ratio(away, home) * awayLift;
            int hg = Poisson(dice, h),
                ag = Poisson(dice, a);
            if (hg != ag)
            {
                return new FixtureResult(hg, ag, ResultKind.Regulation);
            }

            bool homeWins = dice.NextDouble() < h / (h + a);
            ResultKind kind =
                dice.NextDouble() < OvertimeGoalShare ? ResultKind.Overtime : ResultKind.Shootout;
            return homeWins
                ? new FixtureResult(hg + 1, ag, kind)
                : new FixtureResult(hg, ag + 1, kind);
        }

        /// <summary>Gets how a club's attack compares with the other club's defence and goalie, about 0.7 to 1.4.</summary>
        private static double Ratio(ClubData attack, ClubData defence)
        {
            double att = Attack(attack),
                def = Defence(defence);
            return SimMath.Pow(att / def, 1.5);
        }

        private static double Attack(ClubData club)
        {
            List<Traits> s = Skaters(club);
            return s.Count == 0
                ? 50
                : s.Average(t =>
                    (
                        t.ShotAccuracy
                        + t.ShotPower
                        + t.PuckControl
                        + t.HockeyIq
                        + t.TopSpeed
                        + t.Acceleration
                    ) / 6.0
                );
        }

        private static double Defence(ClubData club)
        {
            List<Traits> s = Skaters(club);
            double skaters =
                s.Count == 0 ? 50 : s.Average(t => (t.Checking + t.HockeyIq + t.TopSpeed) / 3.0);
            double goalie = club
                .Roster.Where(p => p.Position == PlayingPosition.G)
                .Select(p => (double)p.Traits.GoalieReflexes)
                .DefaultIfEmpty(50)
                .Max();
            return (skaters + goalie) / 2;
        }

        private static List<Traits> Skaters(ClubData club)
        {
            return
            [
                .. club.Roster.Where(p => p.Position != PlayingPosition.G).Select(p => p.Traits),
            ];
        }

        private static int Poisson(Random dice, double mean)
        {
            double limit = SimMath.Exp(-mean),
                p = 1;
            int k = 0;
            do
            {
                k++;
                p *= dice.NextDouble();
            } while (p > limit);
            return k - 1;
        }
    }
}
