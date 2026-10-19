using System.Numerics;
using StickAndPuck.App.AI.Positioning;
using StickAndPuck.App.Core;
using StickAndPuck.App.Intermission;
using StickAndPuck.App.Players.Movement;
using StickAndPuck.App.Players.Puck;

namespace StickAndPuck.App.AI.Decisions
{
    internal sealed partial class DecisionSystem
    {
        /// <summary>Now and then, a skater at the point with a clear lane and time winds up from distance, for a deflection or a rebound.</summary>
        private bool PointShot(int end, Vector2 at, float lane, bool pressed, float held)
        {
            float distance = RinkSpec.GoalLineX - (end * at.X);
            return !pressed
                && held > 1.2f
                && lane > 0.9f
                && distance is > 12f and < 20f
                && MathF.Abs(at.Y) < 8f
                && rng.NextSingle() < 0.15f;
        }

        /// <summary>The opponents' sticks as this carrier sees them: misread by up to a few metres for a poor passer, re-read every look.</summary>
        private List<(Vector2 At, float Speed)> Defenders(World world, Player p, int smart)
        {
            float misread = Choices.Misread(smart);
            List<(Vector2 At, float Speed)> seen = [];
            foreach (Player o in world.Players)
            {
                if (!o.OnIce || o.Side == p.Side || o.Action == PlayerAction.Fallen)
                {
                    continue;
                }

                Vector2 off =
                    new Vector2(PuckHandling.Gauss(rng), PuckHandling.Gauss(rng)) * misread * 0.6f;
                seen.Add(
                    (
                        Flat(o.IsGoalie ? o.Position : PuckHandling.Blade(o)) + off,
                        o.IsGoalie ? 3f : SkatingModel.TopSpeed(o.Data.Traits, o.Energy)
                    )
                );

                // A pass into a defender's skates is lost as surely as one into his stick
                if (!o.IsGoalie)
                {
                    seen.Add(
                        (Flat(o.Position) + off, SkatingModel.TopSpeed(o.Data.Traits, o.Energy))
                    );
                }
            }
            return seen;
        }

        /// <summary>The best pass the carrier sees, or None with nobody to pass to.</summary>
        internal static Maybe<PassOption> BestPass(
            World world,
            Player p,
            int end,
            Vector2 at,
            List<(Vector2 At, float Speed)> seen,
            int smart,
            bool cycling
        )
        {
            Maybe<PassOption> best = Maybe.None<PassOption>();
            float vision = Choices.Vision(smart);
            float risk = TrailingTalk.TurnoverWeight(world, p.Side); // a pushing club risks more, a patient one less
            float safest = TrailingTalk.SafestPass(world, p.Side);
            foreach (Player mate in world.OnIce(p.Side))
            {
                if (mate == p || mate.IsGoalie || mate.Action == PlayerAction.Fallen)
                {
                    continue;
                }

                (Vector3 target3, float speed) = PuckHandling.Lead(
                    world.Puck.Position,
                    mate,
                    smart
                );
                Vector2 target = Flat(target3);
                float d = Vector2.Distance(at, target);
                if (d < Choices.ShortestPass || d > vision)
                {
                    continue;
                }

                if (smart >= 50 && Choices.Offside(end, at, Flat(mate.Position)))
                {
                    continue;
                }

                if (Cycle.ThroughNet(at, target))
                {
                    continue; // the cage stops a pass through it
                }

                // the stick has to point so puck handling picks this teammate; asked without touching his command
                if (PuckHandling.PickReceiver(world, p, Vector2.Normalize(target - at)) != mate)
                {
                    continue;
                }

                float success = Choices.LaneSuccess(at, target, speed, seen, smart);
                if (success < safest)
                {
                    continue; // too risky for a club told to stay patient
                }

                float value =
                    (
                        success
                        * Choices.SpotValue(end, target)
                        * (0.5f + (0.5f * Choices.Room(target, seen)))
                    ) - ((1f - success) * (cycling ? Choices.CycleTurnover : 0.08f) * risk);
                if (!best.TryGet(out PassOption top) || value > top.Value)
                {
                    best = new PassOption(mate, target, success, value);
                }
            }
            return best;
        }

        /// <summary>A wrist shot holds shoot up to this many steps, short of a slap shot's wind-up, so it leaves the ice now and then.</summary>
        private const int WristLift = 16;

        private void Shoot(World world, Player p, int end, Vector2 at, bool pressed)
        {
            Vector2 toNet = new Vector2(end * RinkSpec.GoalLineX, 0) - at;
            float distance = toNet.Length();
            Maybe<Player> goalie = world
                .OnIce(p.Side == Side.Home ? Side.Away : Side.Home)
                .FirstOrNone(o => o.IsGoalie);
            int side =
                goalie.TryGet(out Player keeper)
                && Goalie.GoalieModel.WeakSide(keeper, end) is var weak and not 0
                && rng.NextSingle() < p.Data.Traits.Iq
                    ? weak
                    : Choices.ShotSide(at, goalie.Select(g => g.Position.Z));
            ReadOutcome roll = Jitter.Roll(world, p, rng).Outcome;

            // an accurate shooter points the stick at the side of the net to beat the goalie; anyone
            // else, and any slap shot, just aims at the net and lets the shot find the far side
            bool slap = distance > 13f && !pressed && rng.NextSingle() < 0.75f;
            Vector2 aim = Vector2.Normalize(new Vector2(end * 0.8f, side * 0.6f));
            bool picksCorner =
                !slap
                && (Traits.Unit(p.Data.Traits.ShotAccuracy) >= 0.45f || roll == ReadOutcome.Lucky);
            if (roll == ReadOutcome.Fumble && !slap)
            {
                // Misread the goalie and pick the side he covers
                aim = Vector2.Normalize(new Vector2(end * 0.8f, -side * 0.6f));
                picksCorner = true;
            }
            if (
                !picksCorner
                || Vector2.Dot(aim, Vector2.Normalize(toNet)) < SimMath.Cos(70f * MathF.PI / 180f)
            )
            {
                aim = Vector2.Normalize(toNet);
            }

            AiCommand.Shoot(
                world,
                p,
                AiLayer.Decisions,
                aim,
                slap ? 28 + rng.Next(22) : rng.Next(WristLift),
                slap ? "slap shot" : "wrist shot"
            );
        }

        /// <summary>Makes a pass; a fumbled execution holds it a beat too long, so the lane may close and the receiver skate on.</summary>
        private void Pass(World world, Player p, Brain brain, PassOption pass, Vector2 at)
        {
            if (Jitter.Roll(world, p, rng).Outcome == ReadOutcome.Fumble)
            {
                brain.LatePass = pass;
                brain.PassAt = world.Time + 0.2f + (0.3f * rng.NextSingle());
                return;
            }
            Release(world, p, brain, pass, at);
        }

        /// <summary>Lets a fumbled pass go once its beat is over, at the receiver as he is then, with the old read of the lane.</summary>
        private static void LatePass(World world, Player p, Brain brain)
        {
            if (!brain.LatePass.TryGet(out PassOption pass))
            {
                return;
            }

            if (p.Action != PlayerAction.None || !pass.Receiver.OnIce)
            {
                brain.LatePass = Maybe.None<PassOption>();
                return;
            }
            if (world.Time < brain.PassAt)
            {
                return;
            }

            brain.LatePass = Maybe.None<PassOption>();
            Vector2 at = Flat(world.Puck.Position);
            (Vector3 target, float _) = PuckHandling.Lead(
                world.Puck.Position,
                pass.Receiver,
                p.Data.Traits.HockeyIq
            );
            if (Vector2.DistanceSquared(Flat(target), at) < 1f)
            {
                return;
            }

            Release(world, p, brain, pass with { Target = Flat(target) }, at);
        }

        private static void Release(World world, Player p, Brain brain, PassOption pass, Vector2 at)
        {
            brain.PassTo = pass.Receiver.Id;
            AiCommand.Act(
                world,
                p,
                p.Command with
                {
                    Move = Vector2.Normalize(pass.Target - at),
                    Pass = true,
                },
                "pass"
            );
        }
    }
}
