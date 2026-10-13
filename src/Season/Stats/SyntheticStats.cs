using StickAndPuck.Core;

namespace StickAndPuck.Season;

/// <summary>
/// Lines for a game settled on paper (<see cref="QuickSim"/>), from its score alone: scorers and
/// assists are drawn from the rosters by their shooting and passing traits, shots and saves around
/// SHL numbers, so the leaders page fills up from the games the human did not play. The same seed
/// always gives the same sheet, as the simulated score does.
/// </summary>
public static class SyntheticStats
{
    const double ShotsPerSide = 28;

    /// <summary>Builds the sheet of a simulated game.</summary>
    /// <param name="home">The home club.</param>
    /// <param name="away">The away club.</param>
    /// <param name="result">The score, shootout goal included.</param>
    /// <param name="seed">The game's seed.</param>
    /// <returns>The sheet, player of the game picked.</returns>
    public static MatchStatSheet Sheet(ClubData home, ClubData away, FixtureResult result, int seed)
    {
#pragma warning disable S2245 // seeded so a game always gives the same lines, not security
        var dice = new Random(unchecked(seed ^ 0x51A7));
#pragma warning restore S2245
        var sheet = new MatchStatSheet { Home = home.Id, Away = away.Id, HomeGoals = result.HomeGoals, AwayGoals = result.AwayGoals };
        var homeLines = Lines(home, Side.Home, dice);
        var awayLines = Lines(away, Side.Away, dice);
        sheet.Lines.AddRange(homeLines);
        sheet.Lines.AddRange(awayLines);

        // the shootout's deciding goal is on the score but on nobody's line
        bool homeWon = result.HomeGoals > result.AwayGoals;
        int homeGoals = result.HomeGoals - (result.Kind == ResultKind.Shootout && homeWon ? 1 : 0);
        int awayGoals = result.AwayGoals - (result.Kind == ResultKind.Shootout && !homeWon ? 1 : 0);
        Score(home, homeLines, homeGoals, dice);
        Score(away, awayLines, awayGoals, dice);
        Shots(home, homeLines, awayLines, awayGoals, dice);
        Shots(away, awayLines, homeLines, homeGoals, dice);
        sheet.PickPlayerOfGame();
        return sheet;
    }

    /// <summary>Every roster skater plays; the better goalie starts.</summary>
    static List<MatchStatLine> Lines(ClubData club, Side side, Random dice)
    {
        var lines = club.Roster.Select(p => MatchStatLine.For(club.Id, side, p)).ToList();
        var goalies = lines.Where(l => l.IsGoalie).ToList();
        foreach (var l in lines) l.Played = !l.IsGoalie;
        if (goalies.Count > 0)
        {
            // the backup starts about one game in five
            var ordered = goalies.OrderByDescending(g => Goalie(club, g)).ToList();
            var starter = ordered.Count > 1 && dice.NextDouble() < 0.2 ? ordered[1] : ordered[0];
            starter.Played = true;
        }
        return lines;
    }

    static void Score(ClubData club, List<MatchStatLine> lines, int goals, Random dice)
    {
        var skaters = lines.Where(l => !l.IsGoalie).ToList();
        if (skaters.Count == 0) return;
        for (int g = 0; g < goals; g++)
        {
            var scorer = Pick(skaters, l => Shooting(club, l), dice);
            scorer.Goals++;
            var others = skaters.Where(l => l != scorer).ToList();
            if (others.Count == 0 || dice.NextDouble() > 0.9) continue;
            var first = Pick(others, l => Passing(club, l), dice);
            first.Assists++;
            others.Remove(first);
            if (others.Count > 0 && dice.NextDouble() < 0.55) Pick(others, l => Passing(club, l), dice).Assists++;
        }
    }

    static void Shots(ClubData club, List<MatchStatLine> shooters, List<MatchStatLine> defenders, int goalsAgainstThem, Random dice)
    {
        var skaters = shooters.Where(l => !l.IsGoalie).ToList();
        if (skaters.Count == 0) return;
        int shots = Math.Max(goalsAgainstThem, Poisson(dice, ShotsPerSide));
        foreach (var l in skaters) l.Shots = l.Goals;
        for (int s = skaters.Sum(l => l.Goals); s < shots; s++) Pick(skaters, l => Shooting(club, l), dice).Shots++;
        if (defenders.Find(l => l.IsGoalie && l.Played) is { } goalie) goalie.Saves = shots - goalsAgainstThem;
    }

    static MatchStatLine Pick(List<MatchStatLine> from, Func<MatchStatLine, double> weight, Random dice)
    {
        double total = from.Sum(weight);
        double r = dice.NextDouble() * total;
        foreach (var l in from)
        {
            r -= weight(l);
            if (r <= 0) return l;
        }
        return from[^1];
    }

    static PlayerData Roster(ClubData club, MatchStatLine l)
    {
        return club.Roster.First(p => p.Number == l.Number);
    }

    /// <summary>Forwards shoot far more than defenders; the best shooters most.</summary>
    static double Shooting(ClubData club, MatchStatLine l)
    {
        var t = Roster(club, l).Traits;
        double skill = ((t.ShotAccuracy + t.ShotPower + t.PuckControl) / 3.0) - 30;
        return Math.Max(2, skill * skill / 40) * (l.Position == PlayingPosition.D ? 0.35 : 1);
    }

    static double Passing(ClubData club, MatchStatLine l)
    {
        var t = Roster(club, l).Traits;
        double skill = ((t.HockeyIq + t.PuckControl) / 2.0) - 30;
        return Math.Max(2, skill * skill / 40) * (l.Position == PlayingPosition.D ? 0.7 : 1);
    }

    static double Goalie(ClubData club, MatchStatLine l)
    {
        return Roster(club, l).Traits.GoalieReflexes;
    }

    static int Poisson(Random dice, double mean)
    {
        double limit = SimMath.Exp(-mean), p = 1;
        int k = 0;
        do { k++; p *= dice.NextDouble(); } while (p > limit);
        return k - 1;
    }
}
