using System.Numerics;
using StickAndPuck.App.Arena;
using StickAndPuck.App.Core;
using StickAndPuck.App.Goalie;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Rules.Penalties;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Rules.Penalties
{
    /// <summary>
    /// The trapezoid (W72): on in the SHL and NHL, off in Arcade, and its markings follow the rule. A human goalie who
    /// plays the puck behind his goal line in a corner, outside the trapezoid, takes a delay-of-game minor that a
    /// skater serves; inside the trapezoid, or in front of the goal line, he may play it. The AI goalie never breaks it:
    /// it leaves a loose puck in the corner alone and gives up on one that slides out of the trapezoid.
    /// </summary>
    public sealed class TrapezoidTests : CheckedTest
    {
        private static List<MatchEvent> Run(World world, ISystem[] systems, float seconds)
        {
            List<MatchEvent> events = [];
            for (int i = 0; i < seconds / Game.Step; i++)
            {
                world.Events.Clear();
                foreach (ISystem s in systems)
                {
                    s.Update(world, Game.Step);
                }

                events.AddRange(world.Events.Current);
                world.Time += Game.Step;
                world.Tick++;
            }
            return events;
        }

        /// <summary>A match in play after the opening drop, with the referee calling penalties, and its home goalie
        /// in a human slot.</summary>
        private static (World World, ISystem[] Systems, Player Goalie, int End) InPlay(
            RuleSet rules
        )
        {
            World w = new WorldBuilder().Rules(rules).Build();
            ISystem[] systems = [new PuckPhysics(), new MatchRules(), new PenaltyRules()];
            for (int i = 0; i < 10 / Game.Step && w.Match.Phase != MatchPhase.Play; i++)
            {
                _ = Run(w, systems, Game.Step);
            }

            Player g = w.OnIce(Side.Home).First(p => p.IsGoalie);
            g.Slot = 0;
            return (w, systems, g, FaceoffFormation.DefendedEnd(w.Match, Side.Home));
        }

        /// <summary>The human goalie skates to <paramref name="at"/> and plays the puck lying there; returns the events of the second after.</summary>
        private static List<MatchEvent> PlayAt(
            World w,
            ISystem[] systems,
            Player g,
            Vector3 at,
            bool carry = false
        )
        {
            Puck pk = w.Puck;
            pk.Carrier = -1;
            pk.Position = at;
            pk.Velocity = Vector3.Zero;
            g.Position = at with { Z = at.Z - (MathF.Sign(at.Z) * 0.5f) };
            List<MatchEvent> events = Run(w, systems, Game.Step); // the puck lies there a step
            if (carry)
            {
                pk.Carrier = g.Id;
            }

            pk.LastTouch = g.Id;
            pk.Velocity = carry ? Vector3.Zero : new Vector3(-3f, 0, 0);
            events.AddRange(Run(w, systems, 1f));
            return events;
        }

        private static bool Called(World w, List<MatchEvent> events, Player g)
        {
            return events.Exists(e => e is TrapezoidViolated t && t.GoalieId == g.Id)
                && w.Match.Penalties.Any(p =>
                    p.Side == g.Side
                    && string.Equals(p.Infraction, Infraction.DelayOfGame, StringComparison.Ordinal)
                );
        }

        /// <summary>A corner spot behind the goal line at <paramref name="end"/>, well outside the trapezoid.</summary>
        private static Vector3 Corner(int end)
        {
            return new Vector3(end * (RinkSpec.GoalLineX + 1.5f), 0, 5f);
        }

        [Test]
        public void Presets()
        {
            Check(RuleSet.Shl.Trapezoid, "SHL: the trapezoid (IIHF rules)");
            Check(RuleSet.Nhl.Trapezoid, "NHL: the trapezoid");
            Check(!RuleSet.Arcade.Trapezoid, "Arcade: no trapezoid");
            Check(
                RuleRows.All.Any(r =>
                    string.Equals(r.Key, nameof(RuleSet.Trapezoid), StringComparison.Ordinal)
                    && string.Equals(
                        r.Group,
                        Text.Keys.RuleGroupInfractions,
                        StringComparison.Ordinal
                    )
                ),
                "a row in the infractions group"
            );
        }

        [Test]
        public void TheMarkingsFollowTheRule()
        {
            foreach (League league in (League[])[League.Shl, League.Nhl])
            {
                foreach (RuleSet rules in (RuleSet[])[RuleSet.Shl, RuleSet.Arcade, RuleSet.Nhl])
                {
                    bool shown = ArenaLayers
                        .For(league, ArenaLayers.TrapezoidShown(rules, Maybe.None<bool>()))
                        .Shows("Trapezoid");
                    Check(
                        shown == rules.Trapezoid,
                        $"{league} arena, {rules.Preset} rules: markings {(shown ? "shown" : "hidden")}"
                    );
                }
            }

            Check(
                !ArenaLayers.TrapezoidShown(
                    RuleSet.Shl with
                    {
                        Trapezoid = false,
                    },
                    Maybe.None<bool>()
                ),
                "a player who turns the rule off loses the markings"
            );
            Check(
                ArenaLayers.TrapezoidShown(RuleSet.Arcade, environment: true)
                    && !ArenaLayers.TrapezoidShown(RuleSet.Nhl, environment: false),
                "SAP_TRAPEZOID overrides the rule"
            );
        }

        [Test]
        public void TheTrapezoidsShape()
        {
            Check(
                RinkSpec.OutsideTrapezoid(new Vector3(RinkSpec.GoalLineX + 1f, 0, 4f)),
                "the corner is outside"
            );
            Check(
                RinkSpec.OutsideTrapezoid(new Vector3(-(RinkSpec.GoalLineX + 1f), 0, -4f)),
                "at either end and either side"
            );
            Check(
                !RinkSpec.OutsideTrapezoid(new Vector3(RinkSpec.GoalLineX + 1f, 0, 2.5f)),
                "behind the net is inside"
            );
            Check(
                !RinkSpec.OutsideTrapezoid(new Vector3(RinkSpec.HalfLength - 0.2f, 0, 4.1f)),
                "it widens toward the end boards"
            );
            Check(
                !RinkSpec.OutsideTrapezoid(new Vector3(RinkSpec.GoalLineX - 0.5f, 0, 8f)),
                "in front of the goal line there is no trapezoid"
            );
        }

        [Test]
        public void TheHumanGoaliePlayingTheCornerDrawsTheMinorPerPreset()
        {
            foreach (RuleSet rules in (RuleSet[])[RuleSet.Shl, RuleSet.Nhl, RuleSet.Arcade])
            {
                (World w, ISystem[] systems, Player g, int end) = InPlay(rules);
                List<MatchEvent> events = PlayAt(w, systems, g, Corner(end));
                bool called = Called(w, events, g);
                Check(
                    called == rules.Trapezoid,
                    $"{rules.Preset}: {(called ? "a minor" : "no call")} for the puck played in the corner"
                );
                if (called)
                {
                    PenaltyState minor = w.Match.Penalties.Last(p => p.Side == g.Side);
                    Player server = w.Players[minor.PlayerId];
                    Check(
                        !server.IsGoalie && server.Side == g.Side,
                        $"{rules.Preset}: a skater serves the goalie's minor ({server.Data.LastName})"
                    );
                    Check(w.Match.Phase != MatchPhase.Play, $"{rules.Preset}: play stops at once");
                    Check(
                        events.Exists(e => e is PenaltyCalled c && c.Side == g.Side),
                        $"{rules.Preset}: the call goes up"
                    );
                }
            }
        }

        [Test]
        public void CarryingThePuckInTheCornerIsCalledToo()
        {
            (World w, ISystem[] systems, Player g, int end) = InPlay(RuleSet.Shl);
            Check(
                Called(w, PlayAt(w, systems, g, Corner(end) with { Z = -5f }, carry: true), g),
                "a minor for carrying it in the far corner"
            );
        }

        [Test]
        public void TheHumanGoalieMayPlayItInsideTheTrapezoidOrInFrontOfTheGoalLine()
        {
            foreach (RuleSet rules in (RuleSet[])[RuleSet.Shl, RuleSet.Nhl])
            {
                (World w, ISystem[] systems, Player g, int end) = InPlay(rules);
                List<MatchEvent> behind = PlayAt(
                    w,
                    systems,
                    g,
                    new Vector3(end * (RinkSpec.GoalLineX + 1.5f), 0, 2.4f)
                );
                Check(!Called(w, behind, g), $"{rules.Preset}: no call inside the trapezoid");
                List<MatchEvent> front = PlayAt(
                    w,
                    systems,
                    g,
                    new Vector3(end * (RinkSpec.GoalLineX - 1f), 0, 6f)
                );
                Check(
                    !Called(w, front, g),
                    $"{rules.Preset}: no call wide of the net in front of the goal line"
                );
                Check(
                    w.Match.Penalties.Count == 0 && w.Match.Phase == MatchPhase.Play,
                    $"{rules.Preset}: play goes on"
                );
            }
        }

        [Test]
        public void ASkaterInTheCornerIsNoCall()
        {
            (World w, ISystem[] systems, Player g, int end) = InPlay(RuleSet.Nhl);
            Player skater = w.OnIce(Side.Home).First(p => !p.IsGoalie);
            List<MatchEvent> events = PlayAt(w, systems, skater, Corner(end));
            Check(
                !events.Exists(e => e is TrapezoidViolated) && w.Match.Penalties.Count == 0,
                $"only a goalie is limited ({g.Data.LastName} stayed home)"
            );
        }

        /// <summary>Loose pucks behind the AI goalie's net, all over both corners and sliding every way: every touch the
        /// goalie makes behind its goal line is inside the trapezoid under the rule; without it the same goalie plays
        /// pucks in the corners too.</summary>
        [Test]
        public void TheAiGoalieNeverBreaksIt()
        {
            (int insideOn, int outsideOn) = AiTouches(trapezoid: true);
            (int insideOff, int outsideOff) = AiTouches(trapezoid: false);
            Console.WriteLine(
                string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"      AI goalie touches behind the net, rule on: {insideOn} inside, {outsideOn} outside; off: {insideOff} inside, {outsideOff} outside"
                )
            );
            Check(
                outsideOn == 0,
                string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"never outside the trapezoid under the rule ({outsideOn})"
                )
            );
            Check(
                insideOn >= 20,
                string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"still plays pucks behind the net ({insideOn})"
                )
            );
            Check(
                outsideOff > 0,
                string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"without the rule it would play the corners ({outsideOff}), so the limit matters"
                )
            );
        }

        private static (int Inside, int Outside) AiTouches(bool trapezoid)
        {
            int inside = 0,
                outside = 0;
            Random dice = new(7);
            for (int i = 0; i < 120; i++)
            {
                World world = new WorldBuilder()
                    .NoStarters()
                    .NotBegun()
                    .Rules(RuleSet.Shl with { Trapezoid = trapezoid })
                    .Build();
                Player g = world.AddPlayer(
                    Side.Away,
                    new PlayerData
                    {
                        Position = PlayingPosition.G,
                        Traits = new Traits { GoaliePuckHandling = 100 },
                    },
                    new Vector3(RinkSpec.GoalLineX - 1f, 0, 0)
                ); // away defends +x in period 1
                g.Heading = -MathF.PI / 2;
                using GoalieSystem goalie = new() { Dice = Seeds.Dice(200 + i) };
                using PuckPhysics physics = new();
                _ = Run(world, [goalie, physics], 0.3f);
                Puck pk = world.Puck;
                pk.Live = true;
                pk.Carrier = -1;
                pk.LastTouch = -1;
                float side = i % 2 == 0 ? 1 : -1;
                pk.Position = new Vector3(
                    RinkSpec.GoalLineX + 0.3f + (dice.NextSingle() * 2.5f),
                    0,
                    side * dice.NextSingle() * 4.4f
                );
                float heading = dice.NextSingle() * MathF.Tau;
                pk.Velocity =
                    new Vector3(MathF.Cos(heading), 0, MathF.Sin(heading)) * dice.NextSingle() * 3f;
                for (int step = 0; step < 3 / Game.Step; step++)
                {
                    Vector3 before = pk.Position;
                    int last = pk.LastTouch;
                    _ = Run(world, [goalie, physics], Game.Step);
                    if (pk.LastTouch == g.Id && last != g.Id && before.X > RinkSpec.GoalLineX)
                    {
                        if (RinkSpec.OutsideTrapezoid(before))
                        {
                            outside++;
                        }
                        else
                        {
                            inside++;
                        }
                    }
                }
            }
            return (inside, outside);
        }
    }
}
