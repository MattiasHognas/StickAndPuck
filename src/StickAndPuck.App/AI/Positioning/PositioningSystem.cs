using System.Numerics;
using StickAndPuck.App.AI.Decisions;
using StickAndPuck.App.AI.Positioning.Dev;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players.Puck;

namespace StickAndPuck.App.AI.Positioning
{
    /// <summary>Where each AI skater skates, by role, in attack and defence, from hockey IQ.
    /// Writes Move and Sprint into the <see cref="Player.Command"/> of every skater with no human
    /// slot (the other buttons are cleared for decisions to fill; <see cref="AiCommand.Base"/>); goalies are the goalie system's.
    /// <list type="bullet">
    /// <item>Each side's skaters get a role (<see cref="Roles"/>) and the side a phase: attacking when
    /// it has the puck or touched it last, else defending.</item>
    /// <item>Jobs: the quickest skater of each side to a loose puck chases it, the defender nearest
    /// the carrier closes him down from the net side, an AI carrier skates the puck up ice, and the
    /// rest hold their role's spot in the <see cref="TeamShape"/>. At a faceoff they line up on the dot.</item>
    /// <item>Smart positioning: a smart skater reads where the puck is going, follows the play quickly
    /// and finds the exact spot; a poor one trails the play and settles a few metres off it.</item>
    /// <item>Teammates keep a couple of metres apart, so a shape never collapses into a pile.</item>
    /// <item>With a teammate on the puck, off-puck skaters get open (<see cref="GetOpen"/>), and
    /// in the attacking zone the forwards cycle corner, behind the net and slot while the defence
    /// hold the points (<see cref="Cycle"/>); a cycling carrier skates the puck round the net.</item>
    /// </list>
    /// Decisions run next and add passes, shots and checks; see <see cref="PositioningModel"/>.</summary>
    /// <remarks>Initializes the system with its own randomness, so tests repeat.</remarks>
    internal sealed partial class PositioningSystem(Random rng) : ISystem
    {
        private const float Spacing = 2.4f; // teammates closer than this push their spots apart
        private const float PressureGap = 1.3f; // how far goal side of the carrier the pressure skater stands

        /// <summary>The dice seed without a match seed.</summary>
        internal const int Fallback = 1;

        private readonly Random rng = rng;

        public PositioningSystem()
            : this(Seeds.Dice(Seeds.Environment, "positioning", Fallback)) { }

        public void Update(World world, float dt)
        {
            // the shootout (src/Rules/Overtime) skates its own shooter
            if (world.Match.Phase == MatchPhase.Shootout)
            {
                return;
            }

            StandInPlay.Before(world, dt);
            if (!PositioningModel.Drives(world))
            {
                return;
            }

            PositioningBoard board = PositioningModel.Board(world);
            AssignRoles(world, board);
            StepView step = Read(world, board);
            foreach (Side side in new[] { Side.Home, Side.Away })
            {
                Cycle.Update(world, board, side, step.Carrier, step.Play, rng, dt);
            }

            foreach (Player p in world.Players)
            {
                if (p.OnIce && !p.IsGoalie)
                {
                    Plan(world, board.For(p.Id), p, step, dt);
                }
            }

            Space(world, board);
            foreach (Player p in world.Players)
            {
                if (p.OnIce && !p.IsGoalie && p.Slot < 0)
                {
                    Steer(world, board.For(p.Id), p);
                }
            }
        }

        /// <summary>This step's reading of the play: who has the puck, whether play is on, and who chases and presses for each side.</summary>
        private sealed record StepView(
            Maybe<Player> Carrier,
            bool Play,
            Maybe<int>[] Chaser,
            Maybe<int>[] Presser
        );

        private static StepView Read(World world, PositioningBoard board)
        {
            Puck pk = world.Puck;
            Maybe<Player> carrier =
                pk.Carrier >= 0 && pk.Carrier < world.Players.Count
                    ? world.Players[pk.Carrier]
                    : Maybe.None<Player>();
            Maybe<Player> lastTouch =
                pk.LastTouch >= 0 && pk.LastTouch < world.Players.Count
                    ? world.Players[pk.LastTouch]
                    : Maybe.None<Player>();
            bool play = world.Match.Phase is MatchPhase.Play or MatchPhase.Overtime && pk.Live;

            SideOrNone owner = carrier.TryGet(out Player holder)
                ? holder.Side.OrNone()
                : (lastTouch.TryGet(out Player toucher) ? toucher.Side.OrNone() : SideOrNone.None);
            foreach (Side side in new[] { Side.Home, Side.Away })
            {
                board.Phase[(int)side] = owner.Is(side)
                    ? TeamPhase.Attack
                    : ((owner == SideOrNone.None) ? Loose(world, side) : TeamPhase.Defend);
            }

            StepView view = new(carrier, play, new Maybe<int>[2], new Maybe<int>[2]);
            if (play && !carrier.HasValue && Chaseable(pk))
            {
                view.Chaser[0] = Quickest(world, Side.Home);
                view.Chaser[1] = Quickest(world, Side.Away);
            }
            if (play && carrier.TryGet(out Player onPuck))
            {
                Side defending = onPuck.Side == Side.Home ? Side.Away : Side.Home;
                view.Presser[(int)defending] = Nearest(world, defending, onPuck);
            }
            return view;
        }

        /// <summary>Picks a skater's job and target for this step.</summary>
        private void Plan(World world, SkaterPlan plan, Player p, StepView step, float dt)
        {
            Puck pk = world.Puck;
            int end = PuckHandling.AttackEnd(world, p.Side);
            float smart = Traits.Unit(p.Data.Traits.HockeyIq);
            TeamPhase teamPhase = PositioningModel.Board(world).Phase[(int)p.Side];
            plan.Ideal = TeamShape.Spot(
                plan.Role,
                teamPhase,
                end,
                new Vector2(pk.Position.X, pk.Position.Z)
            );

            plan.Job = ChooseJob(world, plan, p, step);

            CycleState cycle = PositioningModel.Board(world).Cycle[(int)p.Side];
            plan.Station =
                cycle.Active && cycle.Stations.TryGetValue(p.Id, out Station station)
                    ? station
                    : Maybe.None<Station>();
            Vector2 at = new(p.Position.X, p.Position.Z);
            plan.Target = ComputeTarget(
                world,
                plan,
                p,
                step,
                end,
                teamPhase,
                cycle,
                smart,
                dt,
                at,
                pk
            );

            if (plan.Job == Job.Shape && plan.Open.HasValue)
            {
                plan.Job = Job.Support;
            }

            if (plan.Job is Job.Shape or Job.Support && plan.Station.HasValue)
            {
                plan.Target = Cycle.AroundNet(at, plan.Target, end);
            }

            if (plan.Job is not (Job.Shape or Job.Support))
            {
                plan.Read = Maybe.None<Vector2>();
                plan.Open = Maybe.None<Vector2>();
            }
        }

        private static Job ChooseJob(World world, SkaterPlan plan, Player p, StepView step)
        {
            return p.Slot >= 0 ? Job.Human
                : world.Tick - plan.RequestTick <= 1 && plan.Request.HasValue ? Job.Requested
                : world.Match.Phase == MatchPhase.Faceoff ? Job.Faceoff
                : !step.Play ? Job.Idle
                : step.Carrier.TryGet(out Player carrier) && carrier == p ? Job.Carry
                : step.Chaser[(int)p.Side] == p.Id ? Job.Chase
                : step.Presser[(int)p.Side] == p.Id ? Job.Pressure
                : Job.Shape;
        }

        private Vector2 ComputeTarget(
            World world,
            SkaterPlan plan,
            Player p,
            StepView step,
            int end,
            TeamPhase teamPhase,
            CycleState cycle,
            float smart,
            float dt,
            Vector2 at,
            Puck pk
        )
        {
            return plan.Job switch
            {
                Job.Requested => plan.Request.Or(at), // Requested only with a request
                Job.Faceoff => TeamShape.Faceoff(
                    plan.Role,
                    end,
                    new Vector2(pk.Position.X, pk.Position.Z)
                ),
                Job.Carry when plan.Station.TryGet(out Station own) => Cycle.AroundNet(
                    at,
                    Cycle.Spot(own, end, cycle.CornerSide),
                    end
                ),
                Job.Carry => CarryTarget(world, p, end),
                Job.Chase => ChaseTarget(p, pk, smart),
                Job.Pressure when step.Carrier.TryGet(out Player pressed) => PressureTarget(
                    pressed,
                    end,
                    smart
                ), // a presser only with a carrier
                Job.Shape => ShapeTarget(
                    world,
                    plan,
                    p,
                    OpenSpot(world, plan, p, step.Carrier, teamPhase, end, cycle, Read(pk, smart)),
                    smart,
                    dt
                ),
                Job.Idle or Job.Support or Job.Human => at,
                _ => throw new NotSupportedException("Unhandled job type"),
            };
        }

        /// <summary>The spot an off-puck skater holds. Defending, or with nobody of his own on the puck,
        /// it is his role's spot in the shape. With a teammate on the puck he gets open: around his
        /// cycle station in the attacking zone, or his shape spot elsewhere, he goes where a pass would
        /// get through to a spot worth having (<see cref="GetOpen"/>). Each read is rolled against his
        /// hockey IQ and lasts a moment before the next, shorter for a smart skater.</summary>
        private Vector2 OpenSpot(
            World world,
            SkaterPlan plan,
            Player p,
            Maybe<Player> onPuck,
            TeamPhase phase,
            int end,
            CycleState cycle,
            Vector2 puckRead
        )
        {
            Vector2 shape = TeamShape.Spot(plan.Role, phase, end, puckRead);
            if (
                phase != TeamPhase.Attack
                || !onPuck.TryGet(out Player carrier)
                || carrier.Side != p.Side
            )
            {
                plan.Open = Maybe.None<Vector2>();
                return shape;
            }

            Vector2 spot = plan.Station.TryGet(out Station station)
                ? Cycle.Spot(station, end, cycle.CornerSide)
                : shape;
            (float radius, float minA, float maxA) = OpenBounds(plan, world, spot, end);

            bool stale =
                !plan.Open.TryGet(out Vector2 open) || Vector2.Distance(open, spot) > radius * 1.6f;
            if (!stale && world.Time < plan.OpenNext)
            {
                return open;
            }

            int iq = Jitter.Roll(world, p, rng).Iq;
            Vector2 best = GetOpen.Best(world, p, spot, radius, minA, maxA, iq, rng, plan.Open);
            plan.Open = best;
            plan.OpenNext =
                world.Time
                + (
                    Traits.Scale(p.Data.Traits.HockeyIq, 0.7f, 0.3f)
                    * (0.8f + (0.4f * rng.NextSingle()))
                );
            return best;
        }

        private static (float radius, float minA, float maxA) OpenBounds(
            SkaterPlan plan,
            World world,
            Vector2 spot,
            int end
        )
        {
            float radius;
            float minA = -RinkSpec.HalfLength,
                maxA = RinkSpec.HalfLength;
            float puckA = end * world.Puck.Position.X;
            bool inZone = Cycle.InZone(
                end,
                new Vector2(world.Puck.Position.X, world.Puck.Position.Z)
            );

            if (Roles.IsDefence(plan.Role))
            {
                // the defence hold the points in the zone, and stay behind the play outside it
                radius = inZone ? 2.5f : 3.5f;
                if (inZone)
                {
                    (minA, maxA) = (RinkSpec.BlueLineX + 0.6f, RinkSpec.BlueLineX + 4.5f);
                }
                else
                {
                    maxA = (end * spot.X) + 2f;
                }
            }
            else if (plan.Station.HasValue)
            {
                radius = 2.2f;
                minA = RinkSpec.BlueLineX + 1.5f;
            }
            else
            {
                radius = 4f;
                if (puckA <= RinkSpec.BlueLineX)
                {
                    maxA = RinkSpec.BlueLineX - 0.8f; // stay onside
                }
            }

            return (radius, minA, maxA);
        }

        /// <summary>Writes the command that takes an AI skater to its target; decisions may add a shot, pass or check.</summary>
        private static void Steer(World world, SkaterPlan plan, Player p)
        {
            Vector2 at = new(p.Position.X, p.Position.Z);
            // The cages are solid, so a skater whose way runs through a net heads for a
            // post first instead of pushing into the cage, and does not ease off at that post
            Vector2 target = Steering.AroundNets(at, plan.Target);
            bool round = target != plan.Target;
            PlayerCommand command = plan.Job switch
            {
                Job.Idle => Steering.Toward(p, at),
                Job.Chase or Job.Pressure => Steering.Toward(p, target, 0.8f, sprint: true),
                Job.Carry when plan.Station.HasValue => Steering.Toward(p, target, 1f),
                Job.Carry => Steering.Toward(p, target, 1.5f, sprint: true),
                Job.Requested => Steering.Toward(
                    p,
                    target,
                    round ? 0.5f : Steering.Arrive,
                    sprint: plan.RequestSprint
                ),
                Job.Faceoff => Steering.Toward(p, target, round ? 0.5f : 1.5f),
                Job.Shape or Job.Support or Job.Human => Steering.Toward(
                    p,
                    target,
                    round ? 0.5f : Steering.Arrive,
                    sprint: Vector2.Distance(plan.Target, at) > 12f
                ),
                _ => throw new NotSupportedException("Unhandled job type"),
            };
            AiCommand.Base(world, p, command, Reason(plan.Job));
        }

        /// <summary>The words a debug trace shows for a job.</summary>
        private static string Reason(Job job)
        {
            return job switch
            {
                Job.Idle => "idle",
                Job.Shape => "hold shape",
                Job.Support => "get open",
                Job.Faceoff => "faceoff spot",
                Job.Carry => "carry",
                Job.Chase => "chase the puck",
                Job.Pressure => "pressure the carrier",
                Job.Requested => "requested spot",
                Job.Human => "human",
                _ => throw new NotSupportedException("Unhandled job type"),
            };
        }

        public void DrawScene(World world, RenderContext render)
        {
            StandInPlay.DrawTargets(world);
        }

        public void DrawHud(World world, RenderContext render)
        {
            StandInPlay.DrawHud(world);
        }

        /// <summary>Hands out roles again whenever the skaters on the ice change (line changes, penalties).</summary>
        private static void AssignRoles(World world, PositioningBoard board)
        {
            string lineup = string.Join(
                ',',
                world.Players.Where(p => p.OnIce && !p.IsGoalie).Select(p => p.Id)
            );
            if (string.Equals(lineup, board.Lineup, StringComparison.Ordinal))
            {
                return;
            }

            board.Lineup = lineup;
            foreach (Side side in new[] { Side.Home, Side.Away })
            {
                foreach (
                    (int id, Role role) in Roles.Assign([
                        .. world.OnIce(side).Where(p => !p.IsGoalie),
                    ])
                )
                {
                    board.For(id).Role = role;
                }
            }
        }

        /// <summary>With nobody having touched the puck, the side that can get to it first plays as if attacking.</summary>
        private static TeamPhase Loose(World world, Side side)
        {
            Side other = side == Side.Home ? Side.Away : Side.Home;
            return BestTime(world, side) <= BestTime(world, other)
                ? TeamPhase.Attack
                : TeamPhase.Defend;
        }

        /// <summary>The role's spot as this skater reads it: trailing the ideal spot and a little off it, by hockey IQ.</summary>
        private Vector2 ShapeTarget(
            World world,
            SkaterPlan plan,
            Player p,
            Vector2 spot,
            float smart,
            float dt
        )
        {
            if (world.Time >= plan.ErrorUntil)
            {
                float angle = rng.NextSingle() * MathF.Tau,
                    size = (1f - smart) * 3.5f * MathF.Sqrt(rng.NextSingle());
                plan.Error = new Vector2(SimMath.Cos(angle), SimMath.Sin(angle)) * size;
                plan.ErrorUntil = world.Time + 4f + (rng.NextSingle() * 4f);
            }
            float follow = Math.Min(1f, (1.2f + (4.8f * smart)) * dt);
            Vector2 read = plan.Read.TryGet(out Vector2 was)
                ? was
                : new Vector2(p.Position.X, p.Position.Z);
            read += (spot - read) * follow;
            plan.Read = read;
            return TeamShape.Inside(read + plan.Error);
        }

        /// <summary>Pushes the spots of teammates holding shape apart, so nobody stacks on another.</summary>
        private static void Space(World world, PositioningBoard board)
        {
            foreach (Side side in new[] { Side.Home, Side.Away })
            {
                List<Player> team = [.. world.OnIce(side).Where(p => !p.IsGoalie)];
                foreach (Player p in team)
                {
                    SkaterPlan plan = board.For(p.Id);
                    if (plan.Job is not (Job.Shape or Job.Support))
                    {
                        continue;
                    }

                    Vector2 push = Vector2.Zero;
                    foreach (Player mate in team)
                    {
                        if (mate == p)
                        {
                            continue;
                        }

                        SkaterPlan other = board.For(mate.Id);
                        Vector2 away =
                            plan.Target
                            - (
                                other.Job is Job.Shape or Job.Support
                                    ? other.Target
                                    : new Vector2(mate.Position.X, mate.Position.Z)
                            );
                        float d = away.Length();
                        if (d >= Spacing)
                        {
                            continue;
                        }

                        Vector2 dir =
                            d > 1e-3f
                                ? away / d
                                : new Vector2(SimMath.Cos(p.Id), SimMath.Sin(p.Id));
                        push += dir * (Spacing - d) * 0.6f;
                    }
                    plan.Target = TeamShape.Inside(plan.Target + push);
                }
            }
        }
    }
}
