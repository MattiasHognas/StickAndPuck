using System.Globalization;
using StickAndPuck.App.AI.Tactics;
using StickAndPuck.App.Core;
using StickAndPuck.Tests.Support;
using TUnit.Core.Enums;

namespace StickAndPuck.Tests.AI.Tactics
{
    /// <summary>
    /// The club playstyles (W140): a balanced club, a physical one, one that shoots from anywhere and a
    /// defensive shell each play a period against the same plain opponent (balanced forecheck, carries
    /// the puck in), at home and away, and their <see cref="TacticsTally"/> numbers come apart: the
    /// physical club throws the most checks, the shooting club takes the most shots from distance and
    /// the shell stands around its own net and sends nobody in on the forecheck. Then the SHL club files
    /// play more than one style, and a misspelt style fails the load.
    /// </summary>
    public sealed class PlaystylesTests : CheckedTest
    {
        /// <summary>The first seed.</summary>
        private const int Seed = 11;

        private static readonly PlayStyle[] Styles =
        [
            PlayStyle.Balanced,
            PlayStyle.Physical,
            PlayStyle.ShootFromAnywhere,
            PlayStyle.DefensiveShell,
        ];

        /// <summary>Gets the periods' worlds, shared by the class's tests.</summary>
        [ClassDataSource<Played<World>>(Shared = SharedType.PerClass)]
        public required Played<World> StylePlayed { get; init; }

        /// <summary>Gets the periods: style by style, the styled club at home then away.</summary>
        public static IEnumerable<int> PeriodNumbers()
        {
            return Enumerable.Range(0, Styles.Length * 2);
        }

        /// <summary>The club files: the SHL plays more than one style, a file may leave the style out, and a typo fails the load.</summary>
        [Test]
        public void Data()
        {
            IReadOnlyList<ClubData> clubs = WorldBuilder.Clubs;
            Console.WriteLine(
                "      " + string.Join(", ", clubs.Select(c => $"{c.ShortName} {c.Tactics.Style}"))
            );
            Check(
                Styles.All(s => clubs.Any(c => c.Tactics.Style == s)),
                "the league plays every style, balanced included"
            );
            Check(
                Load( /*lang=json,strict*/
                        "{\"forecheck\": \"trap\"}"
                    )
                    .TryGet(out ClubTactics none)
                    && none.Style == PlayStyle.Balanced,
                "a club file without a style plays balanced"
            );
            Check(
                Load( /*lang=json,strict*/
                        "{\"style\": \"defensiveShell\"}"
                    )
                    .TryGet(out ClubTactics shell)
                    && shell.Style == PlayStyle.DefensiveShell,
                "a club file's style loads as written"
            );
            Check(
                !Load( /*lang=json,strict*/
                    "{\"style\": \"goon\"}"
                ).HasValue,
                "an unknown style fails the club load"
            );
        }

        private static Maybe<ClubTactics> Load(string tactics)
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"sap_style_{Environment.ProcessId}_{tactics.GetHashCode(StringComparison.Ordinal)}.json"
                )
            );
            File.WriteAllText(path, $"{{\"id\": \"t\", \"tactics\": {tactics}}}");
            try
            {
                return Clubs.Load(path).Tactics;
            }
            catch (System.Text.Json.JsonException)
            {
                return Maybe.None<ClubTactics>();
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>One period of a styled club against the plain one; <see cref="StylesApart"/> judges them.</summary>
        [Test]
        [ExecutionPriority(Priority.High)]
        [MethodDataSource(nameof(PeriodNumbers))]
        public void PlayPeriod(int i)
        {
            PlayStyle style = Styles[i / 2];
            Side side = (Side)(i % 2);
            ClubTactics styled = new() { Style = style };
            ClubTactics plain = new();
            World w = TacticsTests.Make(
                WorldBuilder.Clubs,
                new Settings(),
                side == Side.Home ? styled : plain,
                side == Side.Home ? plain : styled
            );
            TacticsTests.Play(w, TacticsTests.Systems(w, Seed + i), (20 * 60) + 300);
            StylePlayed.Add(i, w);
        }

        /// <summary>Each style's tally against the plain club stands apart from the others'.</summary>
        [Test]
        [DependsOn(nameof(PlayPeriod), [typeof(int)])]
        public void StylesApart()
        {
            World[] played = StylePlayed.All(Styles.Length * 2);
            Dictionary<PlayStyle, TacticsTally> by = [];
            int goals = 0;
            for (int s = 0; s < Styles.Length; s++)
            {
                TacticsTally sum = new();
                foreach (Side side in new[] { Side.Home, Side.Away })
                {
                    World world = played[(2 * s) + (int)side];
                    Add(sum, TacticsState.Tally(world, side));
                    goals += world.Match.Score[0] + world.Match.Score[1];
                }
                by[Styles[s]] = sum;
                Console.WriteLine(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"      {Styles[s], -17} hits {sum.Hits, 3}  shots {sum.Shots, 3}, {sum.LongShots, 3} from distance ({sum.LongShare:P0})  {sum.Forecheckers:F1} deep on the breakout  {sum.Collapsed:F1} in the house defending  {sum.Dumps} dumps"
                    )
                );
            }

            TacticsTally bal = by[PlayStyle.Balanced];
            TacticsTally phys = by[PlayStyle.Physical];
            TacticsTally shoot = by[PlayStyle.ShootFromAnywhere];
            TacticsTally shell = by[PlayStyle.DefensiveShell];
            Check(
                Styles.Where(s => s != PlayStyle.Physical).All(s => phys.Hits > 1.8f * by[s].Hits),
                "the physical club throws far more checks than any other style"
            );
            Check(
                Styles
                    .Where(s => s != PlayStyle.ShootFromAnywhere)
                    .All(s =>
                        shoot.LongShots > 1.8f * by[s].LongShots
                        && shoot.LongShare > by[s].LongShare + 0.1f
                    ),
                "the shooting club shoots from distance far more often, and a larger share of its shots"
            );
            Check(
                Styles
                    .Where(s => s != PlayStyle.DefensiveShell)
                    .All(s =>
                        shell.Collapsed > by[s].Collapsed + 1f
                        && shell.Forecheckers < by[s].Forecheckers - 0.4f
                    ),
                "the shell packs its own net when defending and sends nobody in on the forecheck"
            );
            Check(
                goals <= 14 * Styles.Length && bal.Shots >= 6 && shell.Shots >= 6,
                "the styles still play hockey: a normal number of goals, shots for each"
            );
        }

        private static void Add(TacticsTally into, TacticsTally from)
        {
            into.Entries += from.Entries;
            into.Carried += from.Carried;
            into.Dumps += from.Dumps;
            into.Recovered += from.Recovered;
            into.BreakoutSeconds += from.BreakoutSeconds;
            into.DeepSkaterSeconds += from.DeepSkaterSeconds;
            into.NeutralSkaterSeconds += from.NeutralSkaterSeconds;
            into.Exits += from.Exits;
            into.BackAtExits += from.BackAtExits;
            into.Hits += from.Hits;
            into.Shots += from.Shots;
            into.LongShots += from.LongShots;
            into.DefendSeconds += from.DefendSeconds;
            into.HouseSkaterSeconds += from.HouseSkaterSeconds;
        }
    }
}
