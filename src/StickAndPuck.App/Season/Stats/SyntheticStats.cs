using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Schedule;

namespace StickAndPuck.App.Season.Stats
{
    /// <summary>Lines for a game settled on paper (<see cref="QuickSim"/>), from its score alone: scorers and
    /// assists are drawn from the rosters by their shooting and passing traits, shots and saves around
    /// SHL numbers, so the leaders page fills up from the games the human did not play. The same seed
    /// always gives the same sheet, as the simulated score does.</summary>
    public static class SyntheticStats
    {
        private const double ShotsPerSide = 28;

        /// <summary>Builds the sheet of a simulated game.</summary>
        public static MatchStatSheet Sheet(
            ClubData home,
            ClubData away,
            FixtureResult result,
            int seed
        )
        {
            Random dice = Seeds.Dice(unchecked(seed ^ 0x51A7));
            MatchStatSheet sheet = new()
            {
                Home = home.Id,
                Away = away.Id,
                HomeGoals = result.HomeGoals,
                AwayGoals = result.AwayGoals,
            };
            List<MatchStatLine> homeLines = Lines(home, Side.Home, dice);
            List<MatchStatLine> awayLines = Lines(away, Side.Away, dice);
            foreach (MatchStatLine line in homeLines)
            {
                sheet.Lines.Add(line);
            }

            foreach (MatchStatLine line in awayLines)
            {
                sheet.Lines.Add(line);
            }

            // the shootout's deciding goal is on the score but on nobody's line
            bool homeWon = result.HomeGoals > result.AwayGoals;
            int homeGoals =
                result.HomeGoals - (result.Kind == ResultKind.Shootout && homeWon ? 1 : 0);
            int awayGoals =
                result.AwayGoals - (result.Kind == ResultKind.Shootout && !homeWon ? 1 : 0);
            Score(home, homeLines, homeGoals, dice);
            Score(away, awayLines, awayGoals, dice);
            Shots(home, homeLines, awayLines, awayGoals, dice);
            Shots(away, awayLines, homeLines, homeGoals, dice);
            IceAndPlusMinus(homeLines, awayLines, homeGoals, awayGoals, seed);
            sheet.PickPlayerOfGame();
            return sheet;
        }

        /// <summary>Ice time by line (the roster order: first line, second line and so on) and a plus/minus for
        /// each goal: five skaters on the ice for it, the busiest and the ones with points most likely,
        /// minus the one in five that is a power-play goal. Drawn from dice of their own, so the scorers
        /// and shots of a seed stay what they were before these were kept.</summary>
        private static void IceAndPlusMinus(
            List<MatchStatLine> home,
            List<MatchStatLine> away,
            int homeGoals,
            int awayGoals,
            int seed
        )
        {
            Random dice = Seeds.Dice(unchecked(seed ^ 0x2F0E));
            IceTimes(home);
            IceTimes(away);
            PlusMinus(home, away, homeGoals, dice);
            PlusMinus(away, home, awayGoals, dice);
        }

        private static void IceTimes(List<MatchStatLine> lines)
        {
            float[] forwards = [19, 16, 13, 10],
                defence = [23, 20, 17];
            int f = 0,
                d = 0;
            foreach (MatchStatLine l in lines.Where(l => l.Played))
            {
                float minutes = l.Position switch
                {
                    PlayingPosition.G => 60,
                    PlayingPosition.D => defence[Math.Min(d++ / 2, defence.Length - 1)],
                    PlayingPosition.C or PlayingPosition.LW or PlayingPosition.RW => forwards[
                        Math.Min(f++ / 3, forwards.Length - 1)
                    ],
                    _ => throw new NotSupportedException("no ice time for " + l.Position),
                };
                l.IceTime = minutes * 60;
            }
        }

        private static void PlusMinus(
            List<MatchStatLine> scorers,
            List<MatchStatLine> against,
            int goals,
            Random dice
        )
        {
            for (int g = 0; g < goals; g++)
            {
                if (dice.NextDouble() < 0.2)
                {
                    continue; // a power-play goal
                }

                foreach (MatchStatLine l in OnIce(scorers, l => l.IceTime * (1 + l.Points), dice))
                {
                    l.PlusMinus++;
                }

                foreach (MatchStatLine l in OnIce(against, l => l.IceTime, dice))
                {
                    l.PlusMinus--;
                }
            }
        }

        private static List<MatchStatLine> OnIce(
            List<MatchStatLine> lines,
            Func<MatchStatLine, double> weight,
            Random dice
        )
        {
            List<MatchStatLine> left = [.. lines.Where(l => l.Played && !l.IsGoalie)];
            List<MatchStatLine> on = [];
            while (on.Count < 5 && left.Count > 0)
            {
                MatchStatLine picked = Pick(left, l => Math.Max(1, weight(l)), dice);
                on.Add(picked);
                _ = left.Remove(picked);
            }
            return on;
        }

        /// <summary>Every roster skater plays; the better goalie starts.</summary>
        private static List<MatchStatLine> Lines(ClubData club, Side side, Random dice)
        {
            List<MatchStatLine> lines =
            [
                .. club.Roster.Select(p => MatchStatLine.For(club.Id, side, p)),
            ];
            List<MatchStatLine> goalies = [.. lines.Where(l => l.IsGoalie)];
            foreach (MatchStatLine l in lines)
            {
                l.Played = !l.IsGoalie;
            }

            if (goalies.Count > 0)
            {
                // the backup starts about one game in five
                List<MatchStatLine> ordered = [.. goalies.OrderByDescending(g => Goalie(club, g))];
                MatchStatLine starter =
                    ordered.Count > 1 && dice.NextDouble() < 0.2 ? ordered[1] : ordered[0];
                starter.Played = true;
            }
            return lines;
        }

        private static void Score(ClubData club, List<MatchStatLine> lines, int goals, Random dice)
        {
            List<MatchStatLine> skaters = [.. lines.Where(l => !l.IsGoalie)];
            if (skaters.Count == 0)
            {
                return;
            }

            for (int g = 0; g < goals; g++)
            {
                MatchStatLine scorer = Pick(skaters, l => Shooting(club, l), dice);
                scorer.Goals++;
                List<MatchStatLine> others = [.. skaters.Where(l => l != scorer)];
                if (others.Count == 0 || dice.NextDouble() > 0.9)
                {
                    continue;
                }

                MatchStatLine first = Pick(others, l => Passing(club, l), dice);
                first.Assists++;
                _ = others.Remove(first);
                if (others.Count > 0 && dice.NextDouble() < 0.55)
                {
                    Pick(others, l => Passing(club, l), dice).Assists++;
                }
            }
        }

        private static void Shots(
            ClubData club,
            List<MatchStatLine> shooters,
            List<MatchStatLine> defenders,
            int goalsAgainstThem,
            Random dice
        )
        {
            List<MatchStatLine> skaters = [.. shooters.Where(l => !l.IsGoalie)];
            if (skaters.Count == 0)
            {
                return;
            }

            int shots = Math.Max(goalsAgainstThem, Poisson(dice, ShotsPerSide));
            foreach (MatchStatLine l in skaters)
            {
                l.Shots = l.Goals;
            }

            for (int s = skaters.Sum(l => l.Goals); s < shots; s++)
            {
                Pick(skaters, l => Shooting(club, l), dice).Shots++;
            }

            if (defenders.Find(l => l.IsGoalie && l.Played) is { } goalie)
            {
                goalie.Saves = shots - goalsAgainstThem;
            }
        }

        private static MatchStatLine Pick(
            List<MatchStatLine> from,
            Func<MatchStatLine, double> weight,
            Random dice
        )
        {
            double total = from.Sum(weight);
            double r = dice.NextDouble() * total;
            foreach (MatchStatLine l in from)
            {
                r -= weight(l);
                if (r <= 0)
                {
                    return l;
                }
            }
            return from[^1];
        }

        private static PlayerData Roster(ClubData club, MatchStatLine l)
        {
            return club.Roster.First(p => p.Number == l.Number);
        }

        /// <summary>Forwards shoot far more than defenders; the best shooters most.</summary>
        private static double Shooting(ClubData club, MatchStatLine l)
        {
            Traits t = Roster(club, l).Traits;
            double skill = ((t.ShotAccuracy + t.ShotPower + t.PuckControl) / 3.0) - 30;
            return Math.Max(2, skill * skill / 40) * (l.Position == PlayingPosition.D ? 0.35 : 1);
        }

        private static double Passing(ClubData club, MatchStatLine l)
        {
            Traits t = Roster(club, l).Traits;
            double skill = ((t.HockeyIq + t.PuckControl) / 2.0) - 30;
            return Math.Max(2, skill * skill / 40) * (l.Position == PlayingPosition.D ? 0.7 : 1);
        }

        private static double Goalie(ClubData club, MatchStatLine l)
        {
            return Roster(club, l).Traits.GoalieReflexes;
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
