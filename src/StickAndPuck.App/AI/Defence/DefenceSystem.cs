using System.Numerics;
using System.Runtime.InteropServices;
using StickAndPuck.App.AI.Positioning;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players.Puck;

namespace StickAndPuck.App.AI.Defence
{
    /// <summary>The defending side's reads while the other side carries the puck. Runs after positioning
    /// and before decisions: it takes over the AI skaters of the side without the puck,
    /// sets their job to <see cref="Job.Requested"/> with its own target, and writes the move, so
    /// decisions leave them alone. Loose pucks, faceoffs and the attacking side stay positioning's.
    /// <list type="bullet">
    /// <item>The skater positioning sends at the carrier contains him: goal side, stick on the puck, at a
    /// gap that closes nearer the net. Once per approach he decides whether to play the body, likelier
    /// along the boards and for a good checker; only then is he handed back as positioning's presser,
    /// so decisions may finish the check.</item>
    /// <item>The defenceman away from the puck stays home in front of the net, goal side of whoever is
    /// in the slot.</item>
    /// <item>The rest mark a man each, most dangerous first, standing goal side and leaning into the
    /// passing lane from the carrier, so the open man is covered rather than everyone chasing the puck.</item>
    /// <item>Hockey IQ sets the odds: a smart defender leads his man and sticks to the right spot; a
    /// poor one reads late, stands a little off, and now and then turns to watch the puck instead.</item>
    /// </list></summary>
    /// <remarks>Initializes the system with its own randomness, so tests repeat.</remarks>
    internal sealed class DefenceSystem(Random rng) : ISystem
    {
        private const float Spacing = 2.2f; // defenders closer than this push their spots apart

        /// <summary>The dice seed without a match seed.</summary>
        internal const int Fallback = 2;

        private readonly Random rng = rng;

        public DefenceSystem()
            : this(Seeds.Dice(Seeds.Environment, "defence", Fallback)) { }

        /// <summary>Gets what a defender is doing this step, for tests and the HUD.</summary>
        public static Duty DutyOf(World world, Player p)
        {
            return Board(world).Get(p.Id).TryGet(out DefencePlan plan) ? plan.Duty : Duty.None;
        }

        public void Update(World world, float dt)
        {
            DefenceBoard board = Board(world);
            foreach (DefencePlan plan in board.All)
            {
                plan.Duty = Duty.None;
            }

            if (
                world.Match.Phase is not (MatchPhase.Play or MatchPhase.Overtime)
                || !PositioningModel.Drives(world)
            )
            {
                return;
            }

            Puck pk = world.Puck;
            if (!pk.Live || pk.Carrier < 0 || pk.Carrier >= world.Players.Count)
            {
                return;
            }

            Player carrier = world.Players[pk.Carrier];
            if (carrier.IsGoalie)
            {
                return;
            }

            Side side = carrier.Side == Side.Home ? Side.Away : Side.Home;
            Defend(world, board, side, carrier, dt);
        }

        private void Defend(World world, DefenceBoard board, Side side, Player carrier, float dt)
        {
            int end = PuckHandling.AttackEnd(world, side);
            View view = new(
                Coverage.OwnNet(end),
                Flat(carrier.Position),
                Coverage.ZoneOf(end, Flat(carrier.Position))
            );
            List<Player> team =
            [
                .. world
                    .OnIce(side)
                    .Where(p =>
                        !p.IsGoalie && p.Action is not (PlayerAction.Fallen or PlayerAction.Fight)
                    ),
            ];
            List<Player> free = [.. team.Where(p => p.Slot < 0)];
            List<Player> attackers =
            [
                .. world.OnIce(carrier.Side).Where(p => !p.IsGoalie && p != carrier),
            ];
            Dictionary<int, Vector2> spots = [];

            Press(world, board, view, carrier, free, spots);

            // past the far blue line the forecheck is positioning's shape; only the man on the puck is ours
            if (view.Zone != DefenceZone.Forecheck)
            {
                if (view.Zone == DefenceZone.Own)
                {
                    StayHome(world, board, view, free, attackers, spots);
                }

                Mark(world, board, view, free, attackers, spots);
            }

            Space(team, spots);
            foreach (Player p in team)
            {
                if (spots.TryGetValue(p.Id, out Vector2 spot))
                {
                    Steer(world, board.For(p.Id), p, spot, view, dt);
                }
            }
        }

        /// <summary>What the defending side reads this step: its net, the carrier and his zone.</summary>
        [StructLayout(LayoutKind.Auto)]
        private readonly record struct View(Vector2 Net, Vector2 Carrier, DefenceZone Zone);

        /// <summary>The skater positioning sent at the carrier contains him, or plays the body on this approach.</summary>
        private void Press(
            World world,
            DefenceBoard board,
            View view,
            Player carrier,
            List<Player> free,
            Dictionary<int, Vector2> spots
        )
        {
            if (
                free.FirstOrDefault(p => PositioningModel.JobOf(world, p) == Job.Pressure)
                is not { } presser
            )
            {
                return;
            }

            _ = free.Remove(presser);
            DefencePlan plan = board.For(presser.Id);
            if (plan.On != carrier.Id || world.Time >= plan.DecideAt)
            {
                // a new approach: decide once whether to finish a check on it
                plan.On = carrier.Id;
                plan.DecideAt = world.Time + 3f;
                float chance =
                    Coverage.EngageChance(
                        view.Zone,
                        view.Net,
                        view.Carrier,
                        presser.Data.Traits.Checking
                    )
                    * Coverage.HitPace(
                        Decisions.DecisionState.Tally(world, presser.Side).Checks,
                        Played(world)
                    )
                    * Rivals.HitFactor(world.Setup); // a rivalry game plays the body more

                plan.Engage = rng.NextSingle() < chance;
            }
            if (plan.Engage)
            {
                plan.Duty = Duty.Engage; // positioning's pressure job stands: decisions may hit
                return;
            }
            plan.Duty = Duty.Contain;
            Vector2 led = Flat(
                carrier.Position + (carrier.Velocity * (0.35f * presser.Data.Traits.Iq))
            );
            spots[presser.Id] = Coverage.ContainSpot(view.Zone, view.Net, led);
        }

        /// <summary>In the own zone the defenceman furthest across from the puck stays home, on the man in front of the net.</summary>
        private static void StayHome(
            World world,
            DefenceBoard board,
            View view,
            List<Player> free,
            List<Player> attackers,
            Dictionary<int, Vector2> spots
        )
        {
            if (
                free.Where(p => Roles.IsDefence(PositioningModel.RoleOf(world, p)))
                    .MaxBy(p => MathF.Abs(p.Position.Z - view.Carrier.Y))
                is not { } home
            )
            {
                return;
            }

            _ = free.Remove(home);
            Maybe<Player> threat = Maybe.From(
                attackers
                    .Where(a => Vector2.Distance(Flat(a.Position), view.Net) < 8f)
                    .MinBy(a => Vector2.Distance(Flat(a.Position), view.Net))
            );
            if (threat.TryGet(out Player man))
            {
                _ = attackers.Remove(man);
            }

            board.For(home.Id).Duty = Duty.Home;
            spots[home.Id] = Coverage.HomeSpot(
                view.Net,
                view.Carrier,
                threat.TryGet(out Player near) ? Lead(near, home) : Maybe.None<Vector2>()
            );
        }

        /// <summary>The rest mark a man each, most dangerous first, the nearest free defender taking him.</summary>
        private static void Mark(
            World world,
            DefenceBoard board,
            View view,
            List<Player> free,
            List<Player> attackers,
            Dictionary<int, Vector2> spots
        )
        {
            foreach (
                Player a in attackers.OrderByDescending(a =>
                    Coverage.Danger(view.Net, Flat(a.Position))
                )
            )
            {
                if (free.Count == 0)
                {
                    break;
                }

                Vector2 ideal = Coverage.MarkSpot(
                    view.Zone,
                    view.Net,
                    view.Carrier,
                    Flat(a.Position)
                );
                Player marker = free.MinBy(p =>
                    Vector2.DistanceSquared(PositioningModel.IdealOf(world, p), ideal)
                );
                _ = free.Remove(marker);
                DefencePlan plan = board.For(marker.Id);
                plan.Duty = Duty.Mark;
                plan.Man = a.Id;
                Vector2 mark = Coverage.MarkSpot(
                    view.Zone,
                    view.Net,
                    view.Carrier,
                    Lead(a, marker)
                );
                spots[marker.Id] = Coverage.Leash(
                    PositioningModel.IdealOf(world, marker),
                    mark,
                    view.Zone
                );
            }
        }

        /// <summary>Takes the skater over from positioning and skates him to his spot.</summary>
        private void Steer(
            World world,
            DefencePlan plan,
            Player p,
            Vector2 spot,
            View view,
            float dt
        )
        {
            Vector2 target =
                plan.Duty == Duty.Contain ? spot : Read(world, plan, p, spot, view.Carrier, dt);
            // outside the own zone only the man on the puck goes to the carrier
            if (plan.Duty == Duty.Mark && view.Zone != DefenceZone.Own)
            {
                target = Coverage.Room(target, view.Carrier);
            }

            float far = Vector2.Distance(target, Flat(p.Position));
            PlayerCommand move =
                plan.Duty == Duty.Contain
                    ? Steering.Toward(p, target, 0.8f, sprint: true)
                    : Steering.Toward(p, target, 1.6f, sprint: far > 6f);
            AiCommand.Take(
                world,
                p,
                AiLayer.Defence,
                target,
                move,
                plan.Duty switch
                {
                    Duty.Contain => "contain",
                    Duty.Home => "stay home",
                    Duty.None or Duty.Engage or Duty.Mark => "mark",
                    _ => throw new NotSupportedException($"Unknown duty {plan.Duty}"),
                }
            );
        }

        /// <summary>Where a defender reads his man to be: a smart one leads him by where he is skating.</summary>
        private static Vector2 Lead(Player attacker, Player defender)
        {
            return Flat(
                attacker.Position + (attacker.Velocity * (0.45f * defender.Data.Traits.Iq))
            );
        }

        /// <summary>The spot as this defender reads it: trailing the ideal one (slower for a poor reader), a little
        /// off it, and now and then, for a poor reader, drawn toward the puck instead.</summary>
        private Vector2 Read(
            World world,
            DefencePlan plan,
            Player p,
            Vector2 spot,
            Vector2 carrier,
            float dt
        )
        {
            float iq = p.Data.Traits.Iq;
            if (world.Time >= plan.ErrorUntil)
            {
                float angle = rng.NextSingle() * MathF.Tau,
                    size = (1f - iq) * 2.2f * MathF.Sqrt(rng.NextSingle());
                plan.Error = new Vector2(SimMath.Cos(angle), SimMath.Sin(angle)) * size;
                plan.Watching = rng.NextSingle() < 0.3f * (1f - iq);
                plan.ErrorUntil = world.Time + 2.5f + (rng.NextSingle() * 3f);
            }
            if (plan.Watching)
            {
                spot = SimMath.Lerp(spot, carrier, 0.4f);
            }

            float follow = Math.Min(1f, (2f + (6f * iq)) * dt);
            Vector2 read =
                plan.Read.TryGet(out Vector2 r) && world.Tick - plan.ReadTick <= 2
                    ? r
                    : Flat(p.Position);
            read += (spot - read) * follow;
            plan.Read = read;
            plan.ReadTick = world.Tick;
            return TeamShape.Inside(read + plan.Error);
        }

        /// <summary>Pushes the defenders' spots apart, so two never stack on one man.</summary>
        private static void Space(List<Player> team, Dictionary<int, Vector2> spots)
        {
            foreach (Player p in team)
            {
                if (!spots.TryGetValue(p.Id, out Vector2 at))
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

                    Vector2 other = spots.TryGetValue(mate.Id, out Vector2 s)
                        ? s
                        : Flat(mate.Position);
                    Vector2 away = at - other;
                    float d = away.Length();
                    if (d >= Spacing)
                    {
                        continue;
                    }

                    Vector2 dir =
                        d > 1e-3f ? away / d : new Vector2(SimMath.Cos(p.Id), SimMath.Sin(p.Id));
                    push += dir * (Spacing - d) * 0.5f;
                }
                spots[p.Id] = TeamShape.Inside(at + push);
            }
        }

        /// <summary>The share of regulation played so far.</summary>
        private static float Played(World world)
        {
            float period = world.Setup.PeriodSeconds;
            int periods = world.Setup.Periods;
            return Math.Min(
                1f,
                (((world.Match.Period - 1) * period) + (period - world.Match.Clock))
                    / (periods * period)
            );
        }

        private static DefenceBoard Board(World world)
        {
            return world.Defence.Get(() => new DefenceBoard());
        }

        private static Vector2 Flat(Vector3 v)
        {
            return new Vector2(v.X, v.Z);
        }
    }
}
