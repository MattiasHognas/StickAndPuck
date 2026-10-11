using StickAndPuck.Core;

namespace StickAndPuck.Season;

/// <summary>
/// Settles the games the human does not play from the clubs' traits, in an instant: goals are
/// Poisson draws around about three a side, tilted by attack against defence and goalie, with a
/// small home edge. A tie goes to 3 on 3 overtime or a shootout, which adds one goal to the winner
/// as in a played match. The same season seed and game always give the same score.
/// </summary>
public static class QuickSim
{
    const double GoalsPerSide = 2.9;
    const double HomeEdge = 1.06;
    const double OvertimeGoalShare = 0.55;

    /// <summary>Plays one game on paper.</summary>
    /// <param name="home">The home club.</param>
    /// <param name="away">The away club.</param>
    /// <param name="seed">The season seed mixed with the game number.</param>
    /// <returns>The result.</returns>
    public static FixtureResult Play(ClubData home, ClubData away, int seed)
    {
#pragma warning disable S2245 // seeded so a game always plays out the same, not security
        var dice = new Random(seed);
#pragma warning restore S2245
        double h = GoalsPerSide * HomeEdge * Ratio(home, away);
        double a = GoalsPerSide / HomeEdge * Ratio(away, home);
        int hg = Poisson(dice, h), ag = Poisson(dice, a);
        if (hg != ag) return new FixtureResult(hg, ag, ResultKind.Regulation);

        bool homeWins = dice.NextDouble() < h / (h + a);
        var kind = dice.NextDouble() < OvertimeGoalShare ? ResultKind.Overtime : ResultKind.Shootout;
        return homeWins ? new FixtureResult(hg + 1, ag, kind) : new FixtureResult(hg, ag + 1, kind);
    }

    /// <summary>Gets how a club's attack compares with the other club's defence and goalie, about 0.7 to 1.4.</summary>
    static double Ratio(ClubData attack, ClubData defence)
    {
        double att = Attack(attack), def = Defence(defence);
        return Math.Pow(att / def, 1.5);
    }

    static double Attack(ClubData club)
    {
        var s = Skaters(club);
        if (s.Count == 0) return 50;
        return s.Average(t => (t.ShotAccuracy + t.ShotPower + t.PuckControl + t.HockeyIq + t.TopSpeed + t.Acceleration) / 6.0);
    }

    static double Defence(ClubData club)
    {
        var s = Skaters(club);
        double skaters = s.Count == 0 ? 50 : s.Average(t => (t.Checking + t.HockeyIq + t.TopSpeed) / 3.0);
        var goalie = club.Roster.Where(p => p.Position == PlayingPosition.G).Select(p => (double)p.Traits.GoalieReflexes).DefaultIfEmpty(50).Max();
        return (skaters + goalie) / 2;
    }

    static List<Traits> Skaters(ClubData club)
    {
        return club.Roster.Where(p => p.Position != PlayingPosition.G).Select(p => p.Traits).ToList();
    }

    static int Poisson(Random dice, double mean)
    {
        double limit = Math.Exp(-mean), p = 1;
        int k = 0;
        do { k++; p *= dice.NextDouble(); } while (p > limit);
        return k - 1;
    }
}
