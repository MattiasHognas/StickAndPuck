using System.Numerics;
using StickAndPuck.App.AI.Defence;
using StickAndPuck.App.AI.Positioning;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players.Puck;

namespace StickAndPuck.App.AI.Tactics
{
    internal sealed partial class TacticsSystem
    {
        /// <summary>2-1-2: with the puck in the other side's half, a second forward takes the carrier's nearest
        /// outlet, the third sits high, and the defence pinch at the blue line (or stand up at the red
        /// line while the puck is still outside the zone). The man on the puck stays positioning's and
        /// defence's.</summary>
        private static void Aggressive(
            World world,
            TacticsBoard board,
            Side side,
            Player carrier,
            float dt
        )
        {
            int end = PuckHandling.AttackEnd(world, side);
            Vector2 c = Flat(carrier.Position);
            float ca = end * c.X;
            if (ca <= 0f)
            {
                return;
            }

            List<Player> team =
            [
                .. Skaters(world, side).Where(p => p.Slot < 0 && !OnCarrier(world, p)),
            ];
            List<Player> forwards =
            [
                .. team.Where(p => !Roles.IsDefence(PositioningModel.RoleOf(world, p))),
            ];
            List<Player> defence =
            [
                .. team.Where(p => Roles.IsDefence(PositioningModel.RoleOf(world, p))),
            ];

            TakeOutlet(world, board, carrier, c, forwards, dt);
            TakeHighAndPinch(world, board, c, end, ca, forwards, defence, dt);
        }

        private static void TakeOutlet(
            World world,
            TacticsBoard board,
            Player carrier,
            Vector2 c,
            List<Player> forwards,
            float dt
        )
        {
            if (
                Skaters(world, carrier.Side)
                    .Where(p => p != carrier)
                    .MinBy(p => Vector3.Distance(p.Position, carrier.Position))
                    is { } outlet
                && forwards.Count > 0
            )
            {
                Vector2 o = Flat(outlet.Position);
                Player second = forwards.MinBy(p => Vector2.Distance(Flat(p.Position), o));
                _ = forwards.Remove(second);
                Vector2 between = o + (SafeNormal(c - o) * 1.5f);
                Take(
                    world,
                    board,
                    second,
                    TacticTask.Outlet,
                    TeamShape.Inside(between),
                    dt,
                    sprint: true
                );
            }
        }

        private static void TakeHighAndPinch(
            World world,
            TacticsBoard board,
            Vector2 c,
            int end,
            float ca,
            List<Player> forwards,
            List<Player> defence,
            float dt
        )
        {
            bool deep = ca > RinkSpec.BlueLineX;
            foreach (Player p in forwards)
            {
                Vector2 high = deep
                    ? new Vector2(end * (RinkSpec.BlueLineX + 6f), c.Y * 0.3f)
                    : new Vector2(end * Math.Max(ca - 4f, 0.5f), c.Y * 0.3f);
                Take(world, board, p, TacticTask.High, TeamShape.Inside(high), dt, sprint: false);
            }
            foreach (Player p in defence)
            {
                float s = TeamShape.Lateral(PositioningModel.RoleOf(world, p), end);
                Vector2 pinch = deep
                    ? new Vector2(end * (RinkSpec.BlueLineX + 0.8f), (s * 5.5f) + (c.Y * 0.2f))
                    : new Vector2(0f, (s * 4f) + (c.Y * 0.2f));
                Take(world, board, p, TacticTask.Pinch, TeamShape.Inside(pinch), dt, sprint: false);
            }
        }

        /// <summary>1-3-1: with the puck in the other side's zone (or just out of it), the man nearest the
        /// carrier angles him at their blue line without going in deep, three wait in a line across
        /// the red line and the last defenceman stays back by his own blue line.</summary>
        private static void Trap(
            World world,
            TacticsBoard board,
            Side side,
            Player carrier,
            float dt
        )
        {
            int end = PuckHandling.AttackEnd(world, side);
            Vector2 c = Flat(carrier.Position);
            float ca = end * c.X;
            if (ca <= RinkSpec.BlueLineX - 2f)
            {
                return;
            }

            List<Player> team = [.. Skaters(world, side).Where(p => p.Slot < 0)];
            if (team.Count == 0)
            {
                return;
            }

            if (GetAnglePlayer(world, team, c) is { } first)
            {
                _ = team.Remove(first);
                Vector2 angle = new(end * Math.Min(ca - 3f, RinkSpec.BlueLineX + 2f), c.Y * 0.6f);
                Take(
                    world,
                    board,
                    first,
                    TacticTask.Angle,
                    TeamShape.Inside(angle),
                    dt,
                    sprint: false
                );
            }

            if (GetBackDefender(world, team, end) is { } back && team.Count > 3)
            {
                _ = team.Remove(back);
                Vector2 spot = new(end * (-RinkSpec.BlueLineX - 2f), c.Y * 0.2f);
                Take(
                    world,
                    board,
                    back,
                    TacticTask.Back,
                    TeamShape.Inside(spot),
                    dt,
                    sprint: false
                );
            }

            TakeWall(world, board, team, end, c, dt);
        }

        private static Player GetAnglePlayer(World world, List<Player> team, Vector2 c)
        {
            return team.FirstOrDefault(p => OnCarrier(world, p))
                ?? team.Where(p => !Roles.IsDefence(PositioningModel.RoleOf(world, p)))
                    .MinBy(p => Vector2.Distance(Flat(p.Position), c))
                ?? default;
        }

        private static Player GetBackDefender(World world, List<Player> team, int end)
        {
            return team.Where(p => Roles.IsDefence(PositioningModel.RoleOf(world, p)))
                    .MinBy(p => end * p.Position.X)
                ?? default;
        }

        private static void TakeWall(
            World world,
            TacticsBoard board,
            List<Player> team,
            int end,
            Vector2 c,
            float dt
        )
        {
            float[] lanes = team.Count switch
            {
                1 => [0f],
                2 => [-4f, 4f],
                _ => [-7f, 0f, 7f],
            };
            List<Player> wall = [.. team.OrderBy(p => p.Position.Z).Take(lanes.Length)];
            for (int i = 0; i < wall.Count; i++)
            {
                Vector2 spot = new(end * -1.5f, lanes[i] + (c.Y * 0.25f));
                Take(
                    world,
                    board,
                    wall[i],
                    TacticTask.Wall,
                    TeamShape.Inside(spot),
                    dt,
                    sprint: false
                );
            }
        }

        /// <summary>Gets whether positioning or defence has this skater on the carrier.</summary>
        private static bool OnCarrier(World world, Player p)
        {
            return PositioningModel.JobOf(world, p) == Job.Pressure
                || DefenceSystem.DutyOf(world, p) is Duty.Contain or Duty.Engage;
        }

        /// <summary>Takes a skater over from positioning and defence and skates him to his tactical spot, read by his hockey IQ.</summary>
        private static void Take(
            World world,
            TacticsBoard board,
            Player p,
            TacticTask task,
            Vector2 spot,
            float dt,
            bool sprint
        )
        {
            board.Tasks[p.Id] = task;
            float iq = p.Data.Traits.Iq;
            Vector2 read =
                board.Reads.TryGetValue(p.Id, out (Vector2 At, long Tick) r)
                && world.Tick - r.Tick <= 2
                    ? r.At
                    : Flat(p.Position);
            read += (spot - read) * Math.Min(1f, (2f + (6f * iq)) * dt);
            board.Reads[p.Id] = (read, world.Tick);
            Vector2 target = sprint ? spot : read;
            float far = Vector2.Distance(target, Flat(p.Position));
            PlayerCommand move = sprint
                ? Steering.Toward(p, target, 0.8f, sprint: true)
                : Steering.Toward(p, target, 1.6f, sprint: far > 4f);
            AiCommand.Take(world, p, AiLayer.Tactics, target, move, Reason(task));
        }
    }
}
