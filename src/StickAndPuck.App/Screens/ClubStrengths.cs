using StickAndPuck.App.Core;
using StickAndPuck.App.Season;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens;

/// <summary>
/// W33: a club's strengths for team select, 0 to 100, from the traits in its data file: attack
/// (forwards' shooting, puck control and hockey IQ), defence (defenders' checking, IQ and puck
/// control), speed (every skater's acceleration and top speed), goalie (the best goalie's five
/// traits) and overall (W16c's <see cref="ArcadeRun.Strength"/>), plus how it plays (W22d's tactics).
/// </summary>
/// <param name="Attack">Forwards' attack.</param>
/// <param name="Defence">Defenders' defence.</param>
/// <param name="Speed">Skating.</param>
/// <param name="Goalie">The best goalie.</param>
/// <param name="Overall">The whole roster.</param>
/// <param name="Plays">The club's tactics, put in words by <see cref="Describe"/>.</param>
internal sealed record ClubStrengths(int Attack, int Defence, int Speed, int Goalie, int Overall, ClubTactics Plays)
{
    static readonly Dictionary<string, ClubStrengths> Cache = new(StringComparer.Ordinal);

    /// <summary>Gets a club's strengths (worked out once per club).</summary>
    /// <param name="club">The club.</param>
    /// <returns>The strengths.</returns>
    public static ClubStrengths Of(ClubData club)
    {
        if (Cache.TryGetValue(club.Id, out var known)) return known;
        var roster = club.Roster;
        var forwards = roster.Where(p => p.Position is PlayingPosition.C or PlayingPosition.LW or PlayingPosition.RW).Select(p => p.Traits).ToList();
        var defence = roster.Where(p => p.Position == PlayingPosition.D).Select(p => p.Traits).ToList();
        var skaters = roster.Where(p => p.Position != PlayingPosition.G).Select(p => p.Traits).ToList();
        var goalies = roster.Where(p => p.Position == PlayingPosition.G).Select(p => p.Traits).ToList();
        int attack = Average(forwards, t => (t.ShotAccuracy + t.ShotPower + t.PuckControl + t.HockeyIq) / 4.0);
        int def = Average(defence, t => (t.Checking + t.HockeyIq + t.PuckControl) / 3.0);
        int speed = Average(skaters, t => (t.Acceleration + t.TopSpeed) / 2.0);
        int goalie = goalies.Count == 0 ? 50
            : (int)Math.Round(goalies.Max(t => (t.GoalieReflexes + t.GoalieGlove + t.GoalieBlocker + t.GoalieReboundControl + t.GoaliePuckHandling) / 5.0));
        var strengths = new ClubStrengths(attack, def, speed, goalie, (int)Math.Round(ArcadeRun.Strength(club)), club.Tactics);
        Cache[club.Id] = strengths;
        return strengths;
    }

    static int Average(List<Traits> players, Func<Traits, double> value)
    {
        return players.Count == 0 ? 50 : (int)Math.Round(players.Average(value));
    }

    /// <summary>Puts a club's tactics in words, in the language shown.</summary>
    /// <param name="tactics">The tactics.</param>
    /// <returns>The forecheck, the zone entry and the power play.</returns>
    public static string Describe(ClubTactics tactics)
    {
        string forecheck = Text.Get(tactics.Forecheck switch
        {
            Forecheck.Aggressive => K.Forecheck212,
            Forecheck.Trap => K.Trap131,
            _ => K.ForecheckBalanced,
        });
        string entry = Text.Get(tactics.Entry == ZoneEntry.DumpAndChase ? K.DumpIn : K.CarryIn);
        string pp = Text.Get(tactics.PowerPlay switch
        {
            PowerPlayShape.Umbrella => K.PpUmbrella,
            PowerPlayShape.OneThreeOne => K.Pp131,
            _ => K.PpMixed,
        });
        return $"{forecheck}  {entry}  {pp}";
    }
}
