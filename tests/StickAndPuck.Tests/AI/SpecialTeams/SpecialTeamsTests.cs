using System.Globalization;
using System.Numerics;
using StickAndPuck.App.AI.Positioning;
using StickAndPuck.App.AI.SpecialTeams;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players.Puck;
using StickAndPuck.Tests.Support;
using TUnit.Core.Enums;

namespace StickAndPuck.Tests.AI.SpecialTeams
{
    /// <summary>
    /// The special teams: the formation maths, then all-AI power plays (a skater of the short club
    /// sits in the box for the whole run, and a new power play starts every 100 s so both shapes come
    /// up) with special teams on and off, both clubs on the power play in turn. On, the power play holds
    /// its umbrella or 1-3-1 and the kill its box or diamond clearly closer than the even-strength AI
    /// does, and the kill clears the puck down the ice (<see cref="PowerPlay"/>, also run by the AI
    /// match tests). Formerly SAP_SCENE=specialteamstest.
    /// </summary>
    public sealed class SpecialTeamsTests : CheckedTest
    {
        /// <summary>The first seed (the scene's SAP_SEED fallback).</summary>
        private const int Seed = 1;

        /// <summary>The formation maths.</summary>
        [Test]
        public void Maths()
        {
            bool inside = true,
                inZone = true,
                apart = true;
            foreach (int end in new[] { -1, 1 })
            {
                foreach (
                    Formation f in new[]
                    {
                        Formation.Umbrella,
                        Formation.OneThreeOne,
                        Formation.Box,
                        Formation.Diamond,
                    }
                )
                {
                    foreach (int n in new[] { 3, 4, 5 })
                    {
                        IReadOnlyList<Vector2> spots = Formations.Spots(
                            f,
                            n,
                            end,
                            new Vector2(end * 18f, 4f)
                        );
                        foreach (Vector2 s in spots)
                        {
                            inside &= Vector2.Distance(s, TeamShape.Inside(s, 1.1f)) < 1e-4f;
                            inZone &= Formations.InZone(end, s, 1f);
                        }
                        for (int i = 0; i < spots.Count; i++)
                        {
                            for (int j = i + 1; j < spots.Count; j++)
                            {
                                apart &= Vector2.Distance(spots[i], spots[j]) > 2.5f;
                            }
                        }
                    }
                }
            }

            Check(
                inside && inZone,
                "every power play and kill spot is on the ice, inside the zone"
            );
            Check(apart, "no two spots of a shape sit on top of each other");

            IReadOnlyList<Vector2> umbrella = Formations.Spots(
                Formation.Umbrella,
                5,
                1,
                new Vector2(18f, 0)
            );
            IReadOnlyList<Vector2> one31 = Formations.Spots(
                Formation.OneThreeOne,
                5,
                1,
                new Vector2(18f, 0)
            );
            VerifySpecialTeamsReads(umbrella, one31);
        }

        private void VerifySpecialTeamsReads(
            IReadOnlyList<Vector2> umbrella,
            IReadOnlyList<Vector2> one31
        )
        {
            int high = umbrella.Count(s => s.X < RinkSpec.GoalLineX - 10f);
            int middle = one31.Count(s =>
                s.X is > RinkSpec.GoalLineX - 10f and < RinkSpec.GoalLineX - 7.5f
            );
            Check(
                high == 3 && middle == 3,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the umbrella has three high, the 1-3-1 three across the middle ({high}, {middle})"
                )
            );
            IReadOnlyList<Vector2> box = Formations.Spots(Formation.Box, 4, 1, new Vector2(18f, 0));
            IReadOnlyList<Vector2> diamond = Formations.Spots(
                Formation.Diamond,
                4,
                1,
                new Vector2(18f, 0)
            );
            Check(
                box.Where(s => MathF.Abs(s.Y) > 2f).Take(5).Count() == 4
                    && diamond.Where(s => MathF.Abs(s.Y) < 0.5f).Take(3).Count() == 2,
                "the box has two low and two high, the diamond one at each tip"
            );
            IReadOnlyList<Vector2> slid = Formations.Spots(
                Formation.Box,
                4,
                1,
                new Vector2(18f, 8f)
            );
            Check(slid.Average(s => s.Y) > 1.5f, "the box slides toward the puck");
            Check(
                Formations.Answer(Formation.OneThreeOne) == Formation.Diamond
                    && Formations.Answer(Formation.Umbrella) == Formation.Box,
                "the diamond answers the 1-3-1, the box the umbrella"
            );

            (int[] pair, float _) = Formations.Assign(
                [
                    (new Vector2(10f, 0), true),
                    (new Vector2(24f, 1f), false),
                    (new Vector2(15f, -8f), false),
                ],
                Formations.Spots(Formation.Umbrella, 3, 1, Vector2.Zero)
            );
            Check(
                pair[0] == 0 && pair[2] == 1,
                "skaters take the spot nearest them, a defenceman the point"
            );
        }

        /// <summary>Power plays with special teams on and off, each club on the power play in turn.</summary>
        [Test]
        [ExecutionPriority(Priority.High)]
        public void PowerPlayAndKill()
        {
            _ = PowerPlay(WorldBuilder.Clubs, new Settings(), Seed, Check);
        }

        /// <summary>
        /// The SpecialTeams rule off: during a power play the AI plays exactly as the even-strength AI does
        /// (the same match as special teams switched off in the system, step for step), no shape is set,
        /// while the same match with the rule on does set up its shapes.
        /// </summary>
        [Test]
        public void RuleOffKeepsEvenStrength()
        {
            RuleSet off = RuleSet.Shl with { SpecialTeams = false, Custom = true };
            Check(
                RuleSet.Shl.SpecialTeams && RuleSet.Arcade.SpecialTeams && RuleSet.Nhl.SpecialTeams,
                "special teams are on in SHL, Arcade and NHL"
            );
            RuleRow row = RuleRows.Shown.Single(r =>
                string.Equals(r.Key, nameof(RuleSet.SpecialTeams), StringComparison.Ordinal)
            );
            Check(
                new RuleChoice().With(row, "Off").Resolve()
                    is { SpecialTeams: false, Custom: true },
                "the rules page row turns special teams off as a custom rule"
            );

            World ruleOff = SpecialTeamsSetup.ShortHanded(
                WorldBuilder.Clubs,
                new Settings(),
                Side.Away,
                off
            );
            World even = SpecialTeamsSetup.ShortHanded(
                WorldBuilder.Clubs,
                new Settings(),
                Side.Away
            );
            World ruleOn = SpecialTeamsSetup.ShortHanded(
                WorldBuilder.Clubs,
                new Settings(),
                Side.Away
            );
            ISystem[] offSystems = SpecialTeamsSetup.Systems(
                ruleOff,
                Seed,
                special: true,
                rules: true
            );
            ISystem[] evenSystems = SpecialTeamsSetup.Systems(
                even,
                Seed,
                special: false,
                rules: true
            );
            ISystem[] onSystems = SpecialTeamsSetup.Systems(
                ruleOn,
                Seed,
                special: true,
                rules: true
            );

            bool same = true,
                noShape = true,
                onShape = false;
            int powerPlaySteps = 0;
            for (int i = 0; i < (int)(120 / Game.Step); i++)
            {
                Step(ruleOff, offSystems);
                Step(even, evenSystems);
                Step(ruleOn, onSystems);
                if (App.Rules.Penalties.PenaltyCalls.PowerPlay(ruleOff).TryGetSide(out _))
                {
                    powerPlaySteps++;
                }

                same &=
                    ruleOff
                        .Players.Zip(even.Players)
                        .All(p => p.First.Position == p.Second.Position)
                    && ruleOff.Puck.Position == even.Puck.Position;
                noShape &=
                    SpecialTeamsState.FormationOf(ruleOff, Side.Home) == Formation.None
                    && SpecialTeamsState.FormationOf(ruleOff, Side.Away) == Formation.None;
                onShape |= SpecialTeamsState.FormationOf(ruleOn, Side.Home) != Formation.None;
            }

            Check(powerPlaySteps > 0, "home is on the power play");
            Check(
                same,
                "with the rule off every skater and the puck move exactly as with even-strength AI"
            );
            Check(noShape, "with the rule off no power-play or kill shape is set");
            Check(onShape, "with the rule on the power play sets up its shape");
        }

        private static void Step(World world, ISystem[] systems)
        {
            world.Events.Clear();
            foreach (ISystem s in systems)
            {
                s.Update(world, Game.Step);
            }

            world.Time += Game.Step;
            world.Tick++;
        }

        /// <summary>
        /// Plays power plays with special teams on and off, each club on the power play in turn, and
        /// checks the shapes and the clears. Used by <see cref="PowerPlayAndKill"/> and by aimatchtest.
        /// </summary>
        /// <param name="clubs">The clubs.</param>
        /// <param name="settings">The settings.</param>
        /// <param name="seed">The first seed.</param>
        /// <param name="check">Prints and counts one check.</param>
        /// <returns>The last match played.</returns>
        public static World PowerPlay(
            IReadOnlyList<ClubData> clubs,
            Settings settings,
            int seed,
            Action<bool, string> check
        )
        {
            Result on = new();
            Result off = new();
            Maybe<World> last = Maybe.None<World>();
            foreach (Side pp in new[] { Side.Away, Side.Home })
            {
                last = Run(clubs, settings, pp, seed, special: true, on);
                _ = Run(clubs, settings, pp, seed, special: false, off);
                seed++;
            }

            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"      power play: {on.PowerPlays} power plays, {on.Entries} set-ups, {on.PpSeconds:F0} s in the zone (umbrella {on.Umbrella:F0} s, 1-3-1 {on.OneThreeOne:F0} s), shots on goal {on.PpShots} against {on.KillShots} (off {off.PpShots}-{off.KillShots}), attempts {on.PpAttempts} against {on.KillAttempts} (off {off.PpAttempts}-{off.KillAttempts})"
                )
            );
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"      shape held: power play {on.PpHeld:P0} (off {off.PpHeld:P0}), kill {on.KillHeld:P0} (off {off.KillHeld:P0}); kill in box {on.Box:F0} s, diamond {on.Diamond:F0} s"
                )
            );
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"      clears: {on.Clears} ({on.Flips} flipped), {on.ClearedOut} out of the zone; puck in the kill's zone {on.ZoneShare:P0} of the time (off {off.ZoneShare:P0}), goals {on.PpGoals}-{on.KillGoals} (off {off.PpGoals}-{off.KillGoals})"
                )
            );
            check(
                on.Entries >= 6 && on.Umbrella > 10f && on.OneThreeOne > 10f,
                "the power play sets up in the zone in both the umbrella and the 1-3-1"
            );
            check(
                on.PpHeld > 0.45f && on.PpHeld > off.PpHeld + 0.2f,
                "power play skaters hold their spots, clearly more than without special teams"
            );
            check(
                on.Box > 10f && on.KillHeld > 0.6f && on.KillHeld > off.KillHeld + 0.15f,
                "the kill holds its box, clearly more than without special teams"
            );
            check(
                on.Clears >= 8 && on.ClearedOut >= on.Clears * 0.6f,
                "the kill clears the puck down the ice, most clears leaving the zone"
            );
            check(on.PpShots > on.KillShots * 1.5f, "the power play clearly outshoots the kill");
            return last.TryGet(out World played)
                ? played
                : throw new InvalidOperationException("expected a match to have been played");
        }

        private sealed class Result
        {
            public int PpAttempts,
                KillAttempts,
                PowerPlays,
                Entries,
                Clears,
                Flips,
                ClearedOut,
                PpShots,
                KillShots,
                PpGoals,
                KillGoals;
            public float PpSeconds,
                Umbrella,
                OneThreeOne,
                Box,
                Diamond,
                ZoneSeconds,
                Seconds;
            public float PpHeldSum,
                PpSamples,
                KillHeldSum,
                KillSamples;

            public float PpHeld => PpSamples == 0 ? 0 : PpHeldSum / PpSamples;

            public float KillHeld => KillSamples == 0 ? 0 : KillHeldSum / KillSamples;

            public float ZoneShare => Seconds == 0 ? 0 : ZoneSeconds / Seconds;
        }

        /// <summary>Six minutes of play with one skater of the short club in the box throughout, a new power play every 100 s.</summary>
        private static World Run(
            IReadOnlyList<ClubData> clubs,
            Settings settings,
            Side pp,
            int seed,
            bool special,
            Result r
        )
        {
            Side kill = pp == Side.Home ? Side.Away : Side.Home;
            World world = SpecialTeamsSetup.ShortHanded(clubs, settings, kill);
            ISystem[] systems = SpecialTeamsSetup.Systems(world, seed, special, rules: true);

            int end = PuckHandling.AttackEnd(world, pp);
            int steps = (int)(6 * 60 / Game.Step),
                every = (int)(100 / Game.Step);
            for (int i = 0; i < steps && world.Match.Phase != MatchPhase.Final; i++)
            {
                if (i > 0 && i % every == 0)
                {
                    SpecialTeamsState.Board(world).PowerPlay = SideOrNone.None; // the next power play picks its shape again
                }

                world.Events.Clear();
                foreach (ISystem s in systems)
                {
                    s.Update(world, Game.Step);
                }

                world.Time += Game.Step;
                world.Tick++;
                if (world.Match.Phase != MatchPhase.Play || !world.Puck.Live)
                {
                    continue;
                }

                r.Seconds += Game.Step;
                Vector2 puck = new(world.Puck.Position.X, world.Puck.Position.Z);
                if (!Formations.InZone(end, puck))
                {
                    continue;
                }

                r.ZoneSeconds += Game.Step;
                Puck pk = world.Puck;
                bool ppPuck = pk.Carrier >= 0 && world.Players[pk.Carrier].Side == pp;
                if (!ppPuck || i % 15 != 0)
                {
                    continue;
                }

                // measured the same way on and off: how near each skater is to the nearest pairing of the shape's spots
                r.PpHeldSum += Held(
                    world,
                    pp,
                    [Formation.Umbrella, Formation.OneThreeOne],
                    end,
                    puck,
                    -1
                );
                r.PpSamples++;
                r.KillHeldSum += Held(
                    world,
                    kill,
                    [Formation.Box, Formation.Diamond],
                    end,
                    puck,
                    pk.Carrier
                );
                r.KillSamples++;
            }

            Add(world, pp, kill, special, r);
            return world;
        }

        private static void Add(World world, Side pp, Side kill, bool special, Result r)
        {
            SpecialTeamsTally p = SpecialTeamsState.Tally(world, pp);
            SpecialTeamsTally k = SpecialTeamsState.Tally(world, kill);
            if (special)
            {
                r.PowerPlays += p.PowerPlays;
                r.Entries += p.Entries;
                r.PpSeconds += p.Seconds;
                r.Umbrella += p.Umbrella;
                r.OneThreeOne += p.OneThreeOne;
                r.Box += k.Box;
                r.Diamond += k.Diamond;
                r.Clears += k.Clears;
                r.Flips += k.Flips;
                r.ClearedOut += k.ClearedOut;
            }
            r.PpAttempts += App.AI.Decisions.DecisionState.Tally(world, pp).Shots;
            r.KillAttempts += App.AI.Decisions.DecisionState.Tally(world, kill).Shots;
            r.PpShots += world.Match.Shots[(int)pp];
            r.KillShots += world.Match.Shots[(int)kill];
            r.PpGoals += world.Match.Score[(int)pp];
            r.KillGoals += world.Match.Score[(int)kill];
        }

        /// <summary>The share of a club's skaters within 2.5 m of their spot in the best fitting of its shapes; the killer nearest the carrier counts as held when he is on him.</summary>
        private static float Held(
            World world,
            Side side,
            Formation[] shapes,
            int end,
            Vector2 puck,
            int carrier
        )
        {
            List<Player> team = [.. world.OnIce(side).Where(o => !o.IsGoalie)];
            if (team.Count == 0)
            {
                return 0;
            }

            Maybe<Player> presser = Maybe.None<Player>();
            if (
                carrier >= 0
                && team.MinBy(o => Vector3.Distance(o.Position, world.Players[carrier].Position))
                    is { } nearest
                && Vector3.Distance(nearest.Position, world.Players[carrier].Position) <= 3f
            )
            {
                presser = nearest;
            }

            float best = 0;
            foreach (Formation f in shapes)
            {
                IReadOnlyList<Vector2> spots = Formations.Spots(f, team.Count, end, puck);
                List<(Vector2, bool)> who =
                [
                    .. team.Select(o => (new Vector2(o.Position.X, o.Position.Z), false)),
                ];
                (int[] pair, float _) = Formations.Assign(who, spots);
                int held = 0;
                for (int i = 0; i < team.Count; i++)
                {
                    if (
                        presser.Is(x => x == team[i])
                        || (pair[i] >= 0 && Vector2.Distance(who[i].Item1, spots[pair[i]]) < 2.5f)
                    )
                    {
                        held++;
                    }
                }

                best = Math.Max(best, (float)held / team.Count);
            }
            return best;
        }
    }
}
