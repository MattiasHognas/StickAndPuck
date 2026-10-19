using System.Globalization;
using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Rules.Penalties;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Rules.Match
{
    /// <summary>The puck over the glass under each preset: in the SHL and NHL a puck flying over the glass leaves the
    /// rink and stops play with the faceoff where it was last played, and a skater who shoots it straight out from his
    /// own defending zone takes a delay-of-game minor with the draw in his own end. A deflection, a puck off the
    /// glass first, or a clear from the neutral or attacking zone draws no minor. In Arcade netting keeps the puck in
    /// play. In period 1 home defends -x.</summary>
    public sealed class OverGlassTests : CheckedTest
    {
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
            World,
            ISystem[]
        > Sets = [];

        private static ISystem[] Systems(World world)
        {
            return Sets.GetValue(world, _ => [new PuckPhysics(), new MatchRules()]);
        }

        /// <summary>Runs physics and the match rules step by step, calling <paramref name="each"/> before each step, and returns every event.</summary>
        private static List<MatchEvent> Run(
            World world,
            Func<World, bool> until,
            Action<World> each,
            float limit = 5
        )
        {
            List<MatchEvent> events = [];
            for (int i = 0; i < limit / Game.Step && !until(world); i++)
            {
                world.Events.Clear();
                each(world);
                foreach (ISystem s in Systems(world))
                {
                    s.Update(world, Game.Step);
                }

                events.AddRange(world.Events.Current);
                world.Time += Game.Step;
                world.Tick++;
            }
            return events;
        }

        private static List<MatchEvent> Run(World world, Func<World, bool> until, float limit = 5)
        {
            return Run(world, until, _ => { }, limit);
        }

        /// <summary>A world run up to play after the opening drop.</summary>
        private static World InPlay(RuleSet rules, bool penalties = true)
        {
            World w = new WorldBuilder()
                .Rules(rules)
                .Settings(s => s with { Penalties = penalties })
                .Build();
            _ = Run(w, x => x.Match.Phase == MatchPhase.Play, 10);
            return w;
        }

        /// <summary>A skater of the club, not the goalie.</summary>
        private static Player Skater(World w, Side side)
        {
            return w.OnIce(side).First(p => !p.IsGoalie);
        }

        /// <summary>The skater carries the puck at <paramref name="start"/> for a step, then flips it with <paramref name="velocity"/>;
        /// <paramref name="midFlight"/> may touch it on the way. Runs until play stops (or two seconds) and returns the events.</summary>
        private static List<MatchEvent> Flip(
            World w,
            Player shooter,
            Vector3 start,
            Vector3 velocity,
            Action<World, int> midFlight
        )
        {
            Puck pk = w.Puck;
            shooter.Position = start with { Y = 0 };
            shooter.Velocity = Vector3.Zero;
            pk.Carrier = shooter.Id;
            pk.LastTouch = shooter.Id;
            pk.Position = start;
            pk.Velocity = Vector3.Zero;
            List<MatchEvent> ev = Run(w, _ => false, 1.5f * Game.Step);
            pk.Carrier = -1;
            pk.Velocity = velocity;
            int step = 0;
            ev.AddRange(
                Run(w, x => x.Match.Phase != MatchPhase.Play, x => midFlight(x, step++), 2)
            );
            return ev;
        }

        private static List<MatchEvent> Flip(
            World w,
            Player shooter,
            Vector3 start,
            Vector3 velocity
        )
        {
            return Flip(w, shooter, start, velocity, (_, _) => { });
        }

        /// <summary>A high flip from a spot toward the near boards (+z): well over the glass when it gets there.</summary>
        private static readonly Vector3 OverTheGlass = new(0, 12, 15);

        private static bool DelayOfGameOn(World w, int playerId)
        {
            return w.Match.Penalties.Any(p =>
                p.PlayerId == playerId
                && string.Equals(p.Infraction, Infraction.DelayOfGame, StringComparison.Ordinal)
            );
        }

        private static string At(Vector3 v)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{v}");
        }

        /// <summary>The rule switches each preset starts with, and the rows on the rules page.</summary>
        [Test]
        public void Presets()
        {
            Check(
                RuleSet.Shl is { PuckOverGlass: true, OverGlassMinor: true },
                "SHL: the puck can leave over the glass, a minor from the defending zone"
            );
            Check(
                RuleSet.Nhl is { PuckOverGlass: true, OverGlassMinor: true },
                "NHL: the puck can leave over the glass, a minor from the defending zone"
            );
            Check(
                RuleSet.Arcade is { PuckOverGlass: false, OverGlassMinor: false },
                "Arcade: netting keeps the puck in play, no minor"
            );
            foreach (
                string key in new[]
                {
                    nameof(RuleSet.PuckOverGlass),
                    nameof(RuleSet.OverGlassMinor),
                }
            )
            {
                RuleRow row = RuleRows.All.First(r =>
                    string.Equals(r.Key, key, StringComparison.Ordinal)
                );
                RuleSet custom = RuleChoice.Of(RulePreset.Arcade).With(row, "On").Resolve();
                Check(
                    RuleRows.Shown.Contains(row)
                        && string.Equals(row.Get(custom), "On", StringComparison.Ordinal)
                        && custom.Custom,
                    $"{key} is a rule on the rules page that can be switched on for Arcade"
                );
            }
        }

        /// <summary>A clear straight over the glass from the defending zone: a delay-of-game minor and the draw in the offender's end under the SHL and NHL; in Arcade the netting keeps it in play.</summary>
        [Test]
        [Arguments(RulePreset.Shl)]
        [Arguments(RulePreset.Nhl)]
        [Arguments(RulePreset.Arcade)]
        public void ClearFromDefendingZone(RulePreset preset)
        {
            RuleSet rules = RuleSet.Of(preset);
            World w = InPlay(rules);
            Player d = Skater(w, Side.Home);
            List<MatchEvent> ev = Flip(w, d, new Vector3(-20, 0.1f, 8), OverTheGlass);
            List<PuckOverGlass> over = [.. ev.OfType<PuckOverGlass>()];
            if (!rules.PuckOverGlass)
            {
                Check(
                    over.Count == 0 && w.Match.Phase == MatchPhase.Play && !DelayOfGameOn(w, d.Id),
                    $"{rules.Name}: the netting catches it, play goes on"
                );
                Check(
                    MathF.Abs(w.Puck.Position.Z) < RinkSpec.HalfWidth,
                    $"{rules.Name}: the puck stays in the rink ({At(w.Puck.Position)})"
                );
                return;
            }

            Check(
                over.Count == 1 && over[0].PlayerId == d.Id && over[0].Direct,
                $"{rules.Name}: the puck goes over the glass, straight off the clear"
            );
            Check(
                over.Count == 1 && over[0].Position.Y > RinkSpec.GlassTop,
                $"{rules.Name}: it crossed the boards above the glass"
            );
            Check(
                ev.OfType<PenaltyCalled>().Where(p => p.PlayerId == d.Id).Take(2).Count() == 1
                    && DelayOfGameOn(w, d.Id),
                $"{rules.Name}: a delay-of-game minor on the defender"
            );
            Vector3 own = FaceoffSpots.EndDot(w.Match, Side.Home, 1);
            Check(
                w.Match.Phase == MatchPhase.Stopped && MatchFlow.FaceoffDot(w) == own,
                $"{rules.Name}: play stops for a faceoff at home's end dot on the puck's side ({At(MatchFlow.FaceoffDot(w))})"
            );
            _ = Run(w, x => x.Match.Phase == MatchPhase.Faceoff);
            Check(
                w.Puck.Position == own,
                $"{rules.Name}: the puck comes back for the faceoff ({At(w.Puck.Position)})"
            );
        }

        /// <summary>From the attacking zone or the neutral zone the puck over the glass stops play with no minor; the dot is chosen against the club that put it out.</summary>
        [Test]
        [Arguments(RulePreset.Shl)]
        [Arguments(RulePreset.Nhl)]
        public void NoMinorOutsideDefendingZone(RulePreset preset)
        {
            RuleSet rules = RuleSet.Of(preset);
            (Vector3 From, Vector3 Dot, string Zone)[] cases =
            [
                (new Vector3(15, 0.1f, 8), new Vector3(6.1f, 0, 6.7f), "attacking zone"),
                (new Vector3(-3, 0.1f, 8), new Vector3(-6.1f, 0, 6.7f), "neutral zone"),
            ];
            foreach ((Vector3 start, Vector3 dot, string zone) in cases)
            {
                World w = InPlay(rules);
                Player f = Skater(w, Side.Home);
                List<MatchEvent> ev = Flip(w, f, start, OverTheGlass);
                Check(
                    ev.OfType<PuckOverGlass>()
                        .Where(o => o.Direct && o.PlayerId == f.Id)
                        .Take(2)
                        .Count() == 1,
                    $"{rules.Name}, {zone}: the shot goes straight over the glass"
                );
                Check(
                    !ev.OfType<PenaltyCalled>().Any() && !DelayOfGameOn(w, f.Id),
                    $"{rules.Name}, {zone}: no minor"
                );
                Check(
                    w.Match.Phase == MatchPhase.Stopped && MatchFlow.FaceoffDot(w) == dot,
                    $"{rules.Name}, {zone}: play stops, faceoff at {At(dot)} ({At(MatchFlow.FaceoffDot(w))})"
                );
            }
        }

        /// <summary>A deflection on the way out, or a puck off the glass first, is not straight out: a stoppage, no minor.</summary>
        [Test]
        [Arguments(RulePreset.Shl)]
        [Arguments(RulePreset.Nhl)]
        public void DeflectionDrawsNoMinor(RulePreset preset)
        {
            RuleSet rules = RuleSet.Of(preset);
            World w = InPlay(rules);
            Player d = Skater(w, Side.Home);
            Player tip = Skater(w, Side.Away);
            // the opponent gets a piece of it a few steps into the flight (a stick or a block)
            List<MatchEvent> ev = Flip(
                w,
                d,
                new Vector3(-20, 0.1f, 8),
                OverTheGlass,
                (x, step) =>
                {
                    if (step == 3)
                    {
                        x.Puck.LastTouch = tip.Id;
                    }
                }
            );
            List<PuckOverGlass> over = [.. ev.OfType<PuckOverGlass>()];
            Check(
                over.Count == 1 && over[0].PlayerId == tip.Id && !over[0].Direct,
                $"{rules.Name}: the deflected puck goes over the glass off the deflector"
            );
            Check(
                !ev.OfType<PenaltyCalled>().Any()
                    && !DelayOfGameOn(w, d.Id)
                    && !DelayOfGameOn(w, tip.Id),
                $"{rules.Name}: no minor for a deflection"
            );
            Check(
                w.Match.Phase == MatchPhase.Stopped,
                $"{rules.Name}: play stops for the puck out of play"
            );

            // a body in the way deflects it: not clean either
            PuckPlay play = new();
            play.Before(w);
            play.Hit(new PuckImpact(PuckHit.Body, Vector3.Zero, 5, tip.Id));
            Check(
                !play.Clean && play.PlayerId == tip.Id,
                "a body hit makes the skater hit the deflector"
            );
            play.Hit(new PuckImpact(PuckHit.Glass, Vector3.Zero, 5));
            Check(!play.Clean, "a glass hit is not straight out");
        }

        /// <summary>With penalties off in the settings the puck over the glass still stops play, with no minor.</summary>
        [Test]
        public void PenaltiesOff()
        {
            World w = InPlay(RuleSet.Shl, penalties: false);
            Player d = Skater(w, Side.Home);
            List<MatchEvent> ev = Flip(w, d, new Vector3(-20, 0.1f, 8), OverTheGlass);
            Check(
                ev.OfType<PuckOverGlass>().Take(2).Count() == 1
                    && !ev.OfType<PenaltyCalled>().Any()
                    && w.Match.Phase == MatchPhase.Stopped
                    && MatchFlow.FaceoffDot(w) == new Vector3(-20.7f, 0, 6.7f),
                "SHL, penalties off: a stoppage, no minor, the draw at home's end dot"
            );
        }

        /// <summary>A goalie's delay of game is served by the nearest skater of his club on the ice.</summary>
        [Test]
        public void GoalieMinorServedByASkater()
        {
            World w = InPlay(RuleSet.Nhl);
            Player g = w.OnIce(Side.Home).First(p => p.IsGoalie);
            Check(
                DelayOfGame.Server(w, g).TryGet(out Player server)
                    && !server.IsGoalie
                    && server.Side == Side.Home,
                "a skater of the goalie's club serves his minor"
            );
            Check(DelayOfGame.Call(w, g.Id, "test"), "the goalie's minor is called");
            Check(
                w.Match.Penalties.Where(p => p.Side == Side.Home).Take(2).Count() == 1
                    && w.Match.Penalties.All(p => !w.Players[p.PlayerId].IsGoalie)
                    && w.Match.Phase == MatchPhase.Stopped,
                "the skater goes to the box and play stops"
            );
        }

        /// <summary>The wall's top in the puck motion: a puck wholly above it at the boards leaves the rink; under it, or with netting, it bounces back.</summary>
        [Test]
        public void WallTop()
        {
            foreach (
                (float y, float top, bool leaves) in new[]
                {
                    (3.5f, RinkSpec.GlassTop, true),
                    (2.5f, RinkSpec.GlassTop, false),
                    (3.5f, float.PositiveInfinity, false),
                }
            )
            {
                Vector3 p = new(0, y, RinkSpec.HalfWidth - 0.1f);
                Vector3 v = new(0, 0, 10);
                int inNet = 0;
                bool left = PuckMotion.Step(ref p, ref v, ref inNet, Game.Step, [], [], top);
                Check(
                    left == leaves && (leaves ? v.Z > 0 : v.Z < 0),
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"a puck {y} m up at the boards with the wall top at {top}: {(leaves ? "leaves" : "bounces back")}"
                    )
                );
            }
        }
    }
}
