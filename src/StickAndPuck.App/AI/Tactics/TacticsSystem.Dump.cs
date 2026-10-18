using System.Numerics;
using StickAndPuck.App.AI.Decisions;
using StickAndPuck.App.AI.Positioning;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players.Puck;

namespace StickAndPuck.App.AI.Tactics
{
    internal sealed partial class TacticsSystem
    {
        /// <summary>Dump and chase: once per run with the puck, a carrier coming up to the red line with
        /// defenders ahead decides to dump it. He takes it wide, and once the puck will leave his
        /// stick over the red line and too far out to count as a shot at the net, he fires it into the
        /// corner the defence has left emptier.</summary>
        private void Entry(World world, TacticsBoard board, Player p)
        {
            int end = PuckHandling.AttackEnd(world, p.Side);
            Vector2 at = Flat(world.Puck.Position);
            float a = end * at.X;
            Brain brain = DecisionState.Board(world).For(p.Id);
            (int Id, float GotAt) run = (p.Id, brain.GotAt);
            if (board.Dump.TryGet(out DumpPlan plan) && (plan.Player, plan.GotAt) != run)
            {
                board.Dump = Maybe.None<DumpPlan>();
            }

            if (!board.Dump.HasValue)
            {
                if (a is < -8f or > 0.5f || board.Weighed == run)
                {
                    return;
                }

                board.Weighed = run;
                List<Player> opponents = Skaters(world, Other(p.Side));
                bool covered = opponents.Exists(o =>
                    end * o.Position.X > a && Vector3.Distance(o.Position, p.Position) < 12f
                );
                if (rng.NextSingle() >= (covered ? 0.9f : 0.35f))
                {
                    return; // open ice ahead: carry it in
                }

                float wide =
                    (MathF.Abs(at.Y) > 2f)
                        ? MathF.Sign(at.Y)
                        : ((rng.NextSingle() < 0.5f) ? (-1f) : 1f);
                board.Dump = new DumpPlan(p.Id, brain.GotAt, wide);
            }

            if (!board.Dump.TryGet(out DumpPlan dump))
            {
                return; // set just above
            }

            if (a > RinkSpec.BlueLineX - 0.5f)
            {
                board.Dump = Maybe.None<DumpPlan>();
                return;
            } // missed the moment: carry it in
            if (AiCommand.Committed(world, p))
            {
                return;
            }

            AiCommand.Hold(world, p); // no pass or shot of decisions' own meanwhile

            Vector2 then = at + (Flat(p.Velocity) * Release);
            Vector2 net = new(end * RinkSpec.GoalLineX, 0);
            if (end * then.X < 0.6f || Vector2.Distance(then, net) < NoShotRange)
            {
                // take it wide toward the red line, where a dump is not icing and not a shot on goal
                Vector2 lane = TeamShape.Inside(new Vector2(end * (a + 6f), dump.Wide * WideZ));
                AiCommand.Steer(
                    world,
                    p,
                    AiLayer.Tactics,
                    Steering.Toward(p, lane, 1f, sprint: true),
                    "dump: take it wide"
                );
                return;
            }

            Vector2 aim = Vector2.Normalize(DumpTarget(world, p.Side, end) - at);
            AiCommand.Shoot(world, p, AiLayer.Tactics, aim, 3, "dump"); // a tap: a wrist shot along the ice
            TacticsState.Tally(world, p.Side).Dumps++;
            board.Dump = Maybe.None<DumpPlan>();
            board.Chasing = new ChaseWatch(
                p.Side,
                p.Id,
                world.Time + Release + ChaseFor,
                Released: false
            );
        }

        /// <summary>Where a dump goes: deep in the corner the defence has left emptier, along the boards and short of the goal line, away from the goalie's stick.</summary>
        private static Vector2 DumpTarget(World world, Side side, int end)
        {
            return new Vector2(
                end * (RinkSpec.GoalLineX - 3f),
                Emptier(world, side, end) * (RinkSpec.HalfWidth - 1f)
            );
        }

        /// <summary>The side of the attacked zone (z sign) with fewer defenders in it.</summary>
        private static float Emptier(World world, Side side, int end)
        {
            float lean = 0;
            foreach (Player o in world.OnIce(Other(side)))
            {
                if (!o.IsGoalie && end * o.Position.X > RinkSpec.BlueLineX - 3f)
                {
                    lean += MathF.Sign(o.Position.Z);
                }
            }

            return lean > 0 ? -1f : 1f;
        }

        /// <summary>After a dump, the forward nearest the loose puck, besides positioning's chaser, goes in after it too.</summary>
        private static void Chase(World world, TacticsBoard board, Side side, float dt)
        {
            Vector2 puck = Flat(world.Puck.Position);
            if (
                Skaters(world, side)
                    .Where(p =>
                        p.Slot < 0
                        && !Roles.IsDefence(PositioningModel.RoleOf(world, p))
                        && PositioningModel.JobOf(world, p) != Job.Chase
                    )
                    .MinBy(p => Vector2.Distance(Flat(p.Position), puck))
                is not { } second
            )
            {
                return;
            }

            int end = PuckHandling.AttackEnd(world, side);
            // never ahead of the puck over the blue line: that is offside
            Vector2 spot =
                end * puck.X > RinkSpec.BlueLineX
                    ? puck
                    : new Vector2(end * Math.Min(end * puck.X, RinkSpec.BlueLineX - 0.8f), puck.Y);
            Take(world, board, second, TacticTask.Chase, TeamShape.Inside(spot), dt, sprint: true);
        }
    }
}
