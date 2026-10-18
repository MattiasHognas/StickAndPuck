using StickAndPuck.App.AI.Tactics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Arcade;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens
{
    /// <summary>A club's strengths for team select, 0 to 100, from the traits in its data file: attack
    /// (forwards' shooting, puck control and hockey IQ), defence (defenders' checking, IQ and puck
    /// control), speed (every skater's acceleration and top speed), goalie (the best goalie's five
    /// traits) and overall (<see cref="ArcadeRun.Strength"/>), plus how it plays (its tactics).</summary>
    internal sealed record ClubStrengths(
        int Attack,
        int Defence,
        int Speed,
        int Goalie,
        int Overall,
        ClubTactics Plays
    )
    {
        /// <summary>Works out a club's strengths.</summary>
        public static ClubStrengths Of(ClubData club)
        {
            IReadOnlyList<PlayerData> roster = club.Roster;
            List<Traits> forwards =
            [
                .. roster
                    .Where(p =>
                        p.Position is PlayingPosition.C or PlayingPosition.LW or PlayingPosition.RW
                    )
                    .Select(p => p.Traits),
            ];
            List<Traits> defence =
            [
                .. roster.Where(p => p.Position == PlayingPosition.D).Select(p => p.Traits),
            ];
            List<Traits> skaters =
            [
                .. roster.Where(p => p.Position != PlayingPosition.G).Select(p => p.Traits),
            ];
            List<Traits> goalies =
            [
                .. roster.Where(p => p.Position == PlayingPosition.G).Select(p => p.Traits),
            ];
            int attack = Average(
                forwards,
                t => (t.ShotAccuracy + t.ShotPower + t.PuckControl + t.HockeyIq) / 4.0
            );
            int def = Average(defence, t => (t.Checking + t.HockeyIq + t.PuckControl) / 3.0);
            int speed = Average(skaters, t => (t.Acceleration + t.TopSpeed) / 2.0);
            int goalie =
                goalies.Count == 0
                    ? 50
                    : (int)
                        Math.Round(
                            goalies.Max(t =>
                                (
                                    t.GoalieReflexes
                                    + t.GoalieGlove
                                    + t.GoalieBlocker
                                    + t.GoalieReboundControl
                                    + t.GoaliePuckHandling
                                ) / 5.0
                            ),
                            MidpointRounding.ToEven
                        );
            return new ClubStrengths(
                attack,
                def,
                speed,
                goalie,
                (int)Math.Round(ArcadeRun.Strength(club), MidpointRounding.ToEven),
                club.Tactics
            );
        }

        private static int Average(List<Traits> players, Func<Traits, double> value)
        {
            return players.Count == 0
                ? 50
                : (int)Math.Round(players.Average(value), MidpointRounding.ToEven);
        }

        /// <summary>Puts a club's tactics in words, in the language shown.</summary>
        public static string Describe(ClubTactics tactics)
        {
            string forecheck = Text.Get(
                tactics.Forecheck switch
                {
                    Forecheck.Aggressive => K.Forecheck212,
                    Forecheck.Trap => K.Trap131,
                    Forecheck.Balanced => K.ForecheckBalanced,
                    _ => throw new NotSupportedException(
                        $"Unhandled forecheck {tactics.Forecheck} in club strengths"
                    ),
                }
            );
            string entry = Text.Get(tactics.Entry == ZoneEntry.DumpAndChase ? K.DumpIn : K.CarryIn);
            string pp = Text.Get(
                tactics.PowerPlay switch
                {
                    PowerPlayShape.Umbrella => K.PpUmbrella,
                    PowerPlayShape.OneThreeOne => K.Pp131,
                    PowerPlayShape.Mixed => K.PpMixed,
                    _ => throw new NotSupportedException(
                        $"Unhandled power play shape {tactics.PowerPlay} in club strengths"
                    ),
                }
            );
            return $"{forecheck}  {entry}  {pp}";
        }
    }
}
