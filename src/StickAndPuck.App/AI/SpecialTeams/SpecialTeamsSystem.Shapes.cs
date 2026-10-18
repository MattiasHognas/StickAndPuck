using System.Numerics;
using StickAndPuck.App.AI.Decisions;
using StickAndPuck.App.AI.Defence;
using StickAndPuck.App.AI.Positioning;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.AI.SpecialTeams
{
    internal sealed partial class SpecialTeamsSystem
    {
        /// <summary>The kill reads the power play's shape as the puck comes in: the right answer by the odds of its hockey IQ, the wrong one on a fumble.</summary>
        private Formation ReadShape(Formation pp, List<Player> killers)
        {
            int iq = killers.Count == 0 ? 50 : (int)killers.Average(p => p.Data.Traits.HockeyIq);
            (ReadOutcome outcome, int read) = Jitter.Roll(rng, iq);
            Formation right = Formations.Answer(pp);
            Formation wrong = right == Formation.Box ? Formation.Diamond : Formation.Box;
            return (outcome == ReadOutcome.Fumble)
                ? wrong
                : ((rng.NextSingle() < 0.35f + (0.6f * Traits.Unit(read))) ? right : Formation.Box);
        }

        /// <summary>The power play's AI skaters take their spots; the carrier drifts toward his, leaving the play with the puck to decisions.</summary>
        private void PowerPlay(
            World world,
            SpecialTeamsBoard board,
            Side side,
            List<Player> team,
            int end,
            Vector2 puck,
            Maybe<Player> carrier,
            float dt
        )
        {
            IReadOnlyList<Vector2> spots = Formations.Spots(board.PpShape, team.Count, end, puck);
            List<(Player P, Vector2 Spot)> pairs = Pair(world, board, side, team, spots);
            SpecialTeamsTally tally = board.Tally[(int)side];
            tally.Seconds += dt;
            if (board.PpShape == Formation.Umbrella)
            {
                tally.Umbrella += dt;
            }
            else
            {
                tally.OneThreeOne += dt;
            }

            int held = 0;
            foreach ((Player p, Vector2 spot) in pairs)
            {
                if (Vector2.Distance(Flat(p.Position), spot) < OnSpot)
                {
                    held++;
                }

                if (p.Slot >= 0 || PositioningModel.JobOf(world, p) == Job.Chase)
                {
                    continue;
                }

                board.Spots[(int)side][p.Id] = spot;
                if (carrier.TryGet(out Player holder) && holder == p)
                {
                    // the carrier is positioning's and decisions': he skates and passes as usual, and shoots more readily
                    Shoot(world, board, p, end);
                    continue;
                }
                Take(
                    world,
                    p,
                    carrier.TryGet(out Player _)
                        ? Open(world, board, p, spot)
                        : Read(world, board, p, spot, 1.2f),
                    "power play spot"
                );
            }
            tally.OnSpot += dt * held / Math.Max(1, pairs.Count);
        }

        /// <summary>The kill holds its box or diamond; the killer on the carrier presses him while he is near his spot.</summary>
        private void Kill(
            World world,
            SpecialTeamsBoard board,
            Side side,
            List<Player> team,
            int end,
            Vector2 puck,
            Maybe<Player> carrier,
            float dt
        )
        {
            IReadOnlyList<Vector2> spots = Formations.Spots(board.KillShape, team.Count, end, puck);
            List<(Player P, Vector2 Spot)> pairs = Pair(world, board, side, team, spots);
            SpecialTeamsTally tally = board.Tally[(int)side];
            tally.KillSeconds += dt;
            if (board.KillShape == Formation.Box)
            {
                tally.Box += dt;
            }
            else
            {
                tally.Diamond += dt;
            }

            int held = 0;
            foreach ((Player p, Vector2 spot) in pairs)
            {
                board.Spots[(int)side][p.Id] = spot;
                bool presses =
                    carrier.TryGet(out Player onPuck)
                    && DefenceSystem.DutyOf(world, p) is Duty.Contain or Duty.Engage
                    && Vector2.Distance(Flat(onPuck.Position), spot) < PressFrom;
                if (presses || Vector2.Distance(Flat(p.Position), spot) < OnSpot)
                {
                    held++;
                }

                if (presses || p.Slot >= 0 || PositioningModel.JobOf(world, p) == Job.Chase)
                {
                    continue;
                }

                Take(world, p, Read(world, board, p, spot, 1.6f), "kill spot");
            }
            tally.KillOnSpot += dt * held / Math.Max(1, pairs.Count);
        }

        /// <summary>Pairs a club's skaters with the spots, keeping the old pairing unless a new one saves real distance.</summary>
        private static List<(Player P, Vector2 Spot)> Pair(
            World world,
            SpecialTeamsBoard board,
            Side side,
            List<Player> team,
            IReadOnlyList<Vector2> spots
        )
        {
            List<(Vector2, bool)> who =
            [
                .. team.Select(p =>
                    (Flat(p.Position), Roles.IsDefence(PositioningModel.RoleOf(world, p)))
                ),
            ];
            (int[] fresh, float freshCost) = Formations.Assign(who, spots);
            Dictionary<int, int> old = board.Pairing[(int)side];
            float oldCost = 0;
            bool valid =
                old.Count == Math.Min(team.Count, spots.Count)
                && team.Take(spots.Count).All(p => old.ContainsKey(p.Id))
                && old.Values.Distinct().Take(old.Count + 1).Count() == old.Count
                && old.Values.All(s => s < spots.Count);
            if (valid)
            {
                for (int i = 0; i < team.Count; i++)
                {
                    if (old.TryGetValue(team[i].Id, out int s))
                    {
                        oldCost += Formations.Cost(who[i], spots[s], s);
                    }
                }
            }

            if (!valid || freshCost < oldCost - Switch)
            {
                old.Clear();
                for (int i = 0; i < team.Count; i++)
                {
                    if (fresh[i] >= 0)
                    {
                        old[team[i].Id] = fresh[i];
                    }
                }
            }
            return [.. team.Where(p => old.ContainsKey(p.Id)).Select(p => (p, spots[old[p.Id]]))];
        }

        /// <summary>The spot as this skater reads it: a smart one finds it, a poor one stands a little off, re-read every few seconds.</summary>
        private Vector2 Read(
            World world,
            SpecialTeamsBoard board,
            Player p,
            Vector2 spot,
            float off
        )
        {
            SpotPlan plan = board.For(p.Id);
            if (world.Time >= plan.ErrorUntil)
            {
                float angle = rng.NextSingle() * MathF.Tau,
                    size = (1f - p.Data.Traits.Iq) * off * MathF.Sqrt(rng.NextSingle());
                plan.Error = new Vector2(SimMath.Cos(angle), SimMath.Sin(angle)) * size;
                plan.ErrorUntil = world.Time + 3f + (rng.NextSingle() * 3f);
            }
            return TeamShape.Inside(spot + plan.Error);
        }

        /// <summary>A power play shoots more than at even strength: the carrier looks every moment, and with a
        /// clear lane he lets a shot go from the slot, and now and then from the point or the half wall,
        /// a slap shot when he has time, for a tip or a rebound. Decisions hold the shot.</summary>
        private void Shoot(World world, SpecialTeamsBoard board, Player p, int end)
        {
            SpotPlan plan = board.For(p.Id);
            Brain brain = DecisionState.Board(world).For(p.Id);
            if (
                p.Slot >= 0
                || AiCommand.Committed(world, p)
                || brain.LatePass.HasValue
                || world.Time < plan.ShotLook
            )
            {
                return;
            }

            plan.ShotLook =
                world.Time
                + (Choices.ThinkEvery(p.Data.Traits.HockeyIq) * (0.8f + (0.4f * rng.NextSingle())));
            if (world.Time - brain.GotAt < 0.25f)
            {
                return;
            }

            Vector2 at = Flat(world.Puck.Position);
            List<Vector2> blockers =
            [
                .. world
                    .OnIce(p.Side == Side.Home ? Side.Away : Side.Home)
                    .Where(o => !o.IsGoalie)
                    .Select(o => Flat(o.Position)),
            ];
            float lane = Choices.ShotLane(end, at, blockers);
            float quality = Choices.ShotQuality(end, at);
            bool pressed = blockers.Exists(b => Vector2.Distance(b, at) < 2.6f);
            (ReadOutcome outcome, int _) = Jitter.Roll(world, p, rng);
            if (outcome == ReadOutcome.Fumble || lane < 0.85f)
            {
                return;
            }

            if (quality < SlotShot && !(quality > 0.05f && rng.NextSingle() < PerimeterShot))
            {
                return;
            }

            Vector2 net = new(end * RinkSpec.GoalLineX, 0);
            float distance = Vector2.Distance(net, at);
            int side = at.Y > 0 ? -1 : 1;
            Vector2 aim = Vector2.Normalize(net - at + new Vector2(0, side * 0.6f));
            AiCommand.Shoot(
                world,
                p,
                AiLayer.SpecialTeams,
                aim,
                distance > 13f && !pressed ? 30 + rng.Next(20) : 2,
                "power play shot"
            );
            brain.NextThink = Math.Max(brain.NextThink, world.Time + 0.2f);
            board.Tally[(int)p.Side].Shots++;
        }

        /// <summary>With a teammate on the puck, an off-puck skater looks for the open ice around his spot, where
        /// a pass gets through (<see cref="GetOpen"/>), read again every moment, quicker for a smart one.</summary>
        private Vector2 Open(World world, SpecialTeamsBoard board, Player p, Vector2 spot)
        {
            SpotPlan plan = board.For(p.Id);
            bool stale =
                !plan.Open.TryGet(out Vector2 open)
                || Vector2.Distance(open, spot) > OpenRadius * 1.6f;
            if (!stale && world.Time < plan.OpenNext)
            {
                return open;
            }

            int iq = Jitter.Roll(world, p, rng).Iq;
            float minA = RinkSpec.BlueLineX + 0.8f,
                maxA = RinkSpec.HalfLength;
            Vector2 best = GetOpen.Best(world, p, spot, OpenRadius, minA, maxA, iq, rng, plan.Open);
            plan.Open = best;
            plan.OpenNext =
                world.Time
                + (
                    Traits.Scale(p.Data.Traits.HockeyIq, 0.7f, 0.3f)
                    * (0.8f + (0.4f * rng.NextSingle()))
                );
            return best;
        }

        /// <summary>Takes a skater over from positioning and the defence, as the defence does, and skates him to his spot.</summary>
        private static void Take(World world, Player p, Vector2 target, string reason)
        {
            float far = Vector2.Distance(target, Flat(p.Position));
            AiCommand.Take(
                world,
                p,
                AiLayer.SpecialTeams,
                target,
                Steering.Toward(p, target, 1.6f, sprint: far > 6f),
                reason
            );
        }
    }
}
