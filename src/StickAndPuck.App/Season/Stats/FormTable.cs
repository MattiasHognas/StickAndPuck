using System.Globalization;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.Stats
{
    /// <summary>Every season player's carry-over form, saved inside season.json with the stats (an older save
    /// reads with an empty table: everyone starts level). Each credited game moves a player's form: it
    /// keeps <see cref="Keep"/> of what it was and adds the game's <see cref="Rating"/>, built from the
    /// points, the plus/minus and the ice time (a goalie's save percentage and shutouts), so a hot streak
    /// climbs towards +5 and a cold one sinks towards -5, and a player who sits out drifts back to level.
    /// In a season match the form is a hidden nudge of up to 5 points on every trait
    /// (<see cref="Apply(ClubData, RuleSet)"/>), scaled by the rules' <see cref="RuleSet.FormStrength"/>, and the screens show
    /// it as an arrow by the name.</summary>
    public sealed class FormTable
    {
        /// <summary>The most form either way, and the most trait points it moves.</summary>
        public const int Max = 5;

        /// <summary>How much of the form a game keeps before adding its own rating.</summary>
        public const double Keep = 0.7;

        /// <summary>Gets or sets every player with a form, in no particular order.</summary>
        public ICollection<PlayerForm> Players { get; set; } = [];

        /// <summary>Gets a player's form, -5 to +5; 0 for a player with none yet.</summary>
        public double Of(string club, int number)
        {
            return Players.FirstOrNone(p => p.Is(club, number)).Select(p => p.Value).Or(0);
        }

        /// <summary>Gets the trait points a player's form moves under these rules: -5 to +5, 0 with form off.</summary>
        public int Nudge(string club, int number, RuleSet rules)
        {
            return Math.Clamp(
                (int)Math.Round(Of(club, number) * rules.FormScale, MidpointRounding.AwayFromZero),
                -Max,
                Max
            );
        }

        /// <summary>Moves the form of everyone with a line in a credited game.</summary>
        public void Add(MatchStatSheet sheet)
        {
            foreach (MatchStatLine line in sheet.Lines)
            {
                Maybe<PlayerForm> found = Players.FirstOrNone(p => p.Is(line.Club, line.Number));
                if (!found.TryGet(out PlayerForm form))
                {
                    if (!line.Played)
                    {
                        continue;
                    }

                    form = new PlayerForm { Club = line.Club, Number = line.Number };
                    Players.Add(form);
                }

                form.Value = Math.Clamp(
                    (form.Value * Keep) + (line.Played ? Rating(line, sheet) : 0),
                    -Max,
                    Max
                );
            }
        }

        /// <summary>How well one line played, about -3 to +3 with 0 an ordinary night. A skater: 1.5 a goal,
        /// 1 an assist and 0.6 a plus/minus point, less 0.6 for the points an ordinary night brings, and up
        /// to half a point either way for more or less ice time than the club's other skaters at that
        /// position. A goalie: 30 times the save percentage over .900, and 1 for a shutout.</summary>
        public static double Rating(MatchStatLine line, MatchStatSheet sheet)
        {
            double r;
            if (line.IsGoalie)
            {
                int against = line.Side == Side.Home ? sheet.AwayGoals : sheet.HomeGoals;
                int faced = line.Saves + against;
                r =
                    faced == 0
                        ? 0
                        : ((((double)line.Saves / faced) - 0.9) * 30) + (against == 0 ? 1 : 0);
            }
            else
            {
                r = (line.Goals * 1.5) + line.Assists + (line.PlusMinus * 0.6) - 0.6;
                List<float> mates =
                [
                    .. sheet
                        .Lines.Where(l =>
                            l.Played
                            && l.Side == line.Side
                            && l.Position == line.Position
                            && l.IceTime > 0
                        )
                        .Select(l => l.IceTime),
                ];
                if (line.IceTime > 0 && mates.Count > 0)
                {
                    r += Math.Clamp((line.IceTime / mates.Average()) - 1, -0.5, 0.5);
                }
            }
            return Math.Clamp(r, -3, 3);
        }

        /// <summary>The key of a player in <see cref="MatchSetup.Form"/>: "club#number".</summary>
        public static string Key(string club, int number)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{club}#{number}");
        }

        /// <summary>Gets the nudges of a club's players under these rules, by <see cref="Key"/>, leaving out the zeros.</summary>
        public IReadOnlyDictionary<string, int> Nudges(ClubData club, RuleSet rules)
        {
            return club
                .Roster.Select(p => p.Number)
                .Distinct()
                .Select(n => (Key: Key(club.Id, n), Nudge: Nudge(club.Id, n, rules)))
                .Where(x => x.Nudge != 0)
                .ToDictionary(x => x.Key, x => x.Nudge, StringComparer.Ordinal);
        }

        /// <summary>The club as a season match plays it: every player's traits moved by their form's nudge.</summary>
        public ClubData Apply(ClubData club, RuleSet rules)
        {
            return Apply(club, Nudges(club, rules));
        }

        /// <summary>The club with every player's traits moved by their nudge in the list, by <see cref="Key"/>.</summary>
        public static ClubData Apply(ClubData club, IReadOnlyDictionary<string, int> nudges)
        {
            return !club.Roster.Any(p => nudges.ContainsKey(Key(club.Id, p.Number)))
                ? club
                : club with
                {
                    Roster =
                    [
                        .. club.Roster.Select(p =>
                            nudges.TryGetValue(Key(club.Id, p.Number), out int n) && n != 0
                                ? p with
                                {
                                    Traits = Nudged(p.Traits, n),
                                }
                                : p
                        ),
                    ],
                };
        }

        /// <summary>Every trait moved by the same points, kept within 0 to 100.</summary>
        public static Traits Nudged(Traits t, int n)
        {
            static int M(int v, int n)
            {
                return Math.Clamp(v + n, 0, 100);
            }

            return t with
            {
                Acceleration = M(t.Acceleration, n),
                TopSpeed = M(t.TopSpeed, n),
                ShotAccuracy = M(t.ShotAccuracy, n),
                ShotPower = M(t.ShotPower, n),
                Checking = M(t.Checking, n),
                PuckControl = M(t.PuckControl, n),
                HockeyIq = M(t.HockeyIq, n),
                GoalieReflexes = M(t.GoalieReflexes, n),
                GoalieGlove = M(t.GoalieGlove, n),
                GoalieBlocker = M(t.GoalieBlocker, n),
                GoalieReboundControl = M(t.GoalieReboundControl, n),
                GoaliePuckHandling = M(t.GoaliePuckHandling, n),
            };
        }
    }
}
