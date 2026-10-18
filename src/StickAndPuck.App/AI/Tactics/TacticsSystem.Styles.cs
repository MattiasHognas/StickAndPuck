using System.Numerics;
using StickAndPuck.App.AI.Positioning;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players.Puck;

namespace StickAndPuck.App.AI.Tactics
{
    internal sealed partial class TacticsSystem
    {
        private const float ShotLook = 0.5f; // seconds between a shooting club's carrier's looks at a shot from distance
        private const float FarthestShot = 24f; // metres from the net a shooting club still shoots from
        private const float HuntFrom = 7f; // metres from the carrier a physical club's skater goes at him to hit

        /// <summary>How much likelier a physical club's skater on the carrier is to line up a hit each time
        /// he weighs one.</summary>
        internal const float PhysicalHits = 3f;

        /// <summary>Plays both clubs' styles around a skater carrying the puck, after the forecheck, so a
        /// shell or a physical club's hunter has the last word on its skaters.</summary>
        private void Styles(World world, TacticsBoard board, Player carrier, float dt)
        {
            if (
                TacticsState.Of(world, carrier.Side).Style == PlayStyle.ShootFromAnywhere
                && carrier.Slot < 0
            )
            {
                ShootFromAnywhere(world, board, carrier);
            }

            Side defending = Other(carrier.Side);
            switch (TacticsState.Of(world, defending).Style)
            {
                case PlayStyle.DefensiveShell:
                    Shell(world, board, defending, carrier, dt);
                    break;
                case PlayStyle.Physical:
                    Physical(world, board, defending, carrier, dt);
                    break;
                case PlayStyle.Balanced:
                case PlayStyle.ShootFromAnywhere:
                    break;
                default:
                    break;
            }
        }

        /// <summary>Shoot from anywhere: an AI carrier inside the attacking zone, out of the corners and with
        /// a lane to the net, often lets it go at once rather than working it in for a better chance:
        /// a slap shot with room, a quick wrist shot under pressure.</summary>
        private void ShootFromAnywhere(World world, TacticsBoard board, Player p)
        {
            int end = PuckHandling.AttackEnd(world, p.Side);
            Vector2 at = Flat(world.Puck.Position);
            float a = end * at.X;
            Vector2 toNet = new Vector2(end * RinkSpec.GoalLineX, 0) - at;
            float distance = toNet.Length();
            float dx = RinkSpec.GoalLineX - a;
            if (
                a < RinkSpec.BlueLineX + 0.5f
                || dx < 1.5f
                || distance > FarthestShot
                || MathF.Atan2(MathF.Abs(at.Y), dx) > 1.1f
                || AiCommand.Committed(world, p)
                || world.Time < board.ShotLook
            )
            {
                return;
            }

            board.ShotLook = world.Time + ShotLook;
            IEnumerable<Vector2> blockers = Skaters(world, Other(p.Side))
                .Select(o => Flat(o.Position));
            if (Decisions.Choices.ShotLane(end, at, blockers) < 0.6f || rng.NextSingle() >= 0.3f)
            {
                return;
            }

            bool pressed = Skaters(world, Other(p.Side))
                .Exists(o => Vector3.Distance(o.Position, p.Position) < 2.6f);
            bool slap = distance > TacticsTally.LongShot && !pressed;
            AiCommand.Hold(world, p);
            AiCommand.Shoot(
                world,
                p,
                AiLayer.Tactics,
                Vector2.Normalize(toNet),
                slap ? 28 + rng.Next(16) : 3,
                slap ? "shoot from anywhere: slap shot" : "shoot from anywhere: wrist shot"
            );
        }

        /// <summary>Physical: the club's skater nearest the other side's carrier, if he is close, skates
        /// straight at him to finish the check (decisions roll the hit, far likelier for a physical
        /// club: <see cref="TacticsState.HitScale"/>). Not in front of its own net, where the defence
        /// keeps its shape.</summary>
        private static void Physical(
            World world,
            TacticsBoard board,
            Side side,
            Player carrier,
            float dt
        )
        {
            int end = PuckHandling.AttackEnd(world, side);
            if (end * carrier.Position.X < -RinkSpec.GoalLineX + 8f)
            {
                return;
            }

            if (
                Skaters(world, side)
                    .Where(p =>
                        p.Slot < 0
                        && Vector3.Distance(p.Position, carrier.Position) < HuntFrom
                        && !Trailing(carrier, p)
                    )
                    .MinBy(p => Vector3.Distance(p.Position, carrier.Position))
                is not { } hunter
            )
            {
                return;
            }

            Vector2 lead = Flat(carrier.Position + (carrier.Velocity * 0.25f));
            Take(world, board, hunter, TacticTask.Hit, TeamShape.Inside(lead), dt, sprint: false);
        }

        /// <summary>Gets whether a skater is behind a moving carrier, where going at him is a hook, not a hit.</summary>
        private static bool Trailing(Player carrier, Player p)
        {
            Vector2 v = Flat(carrier.Velocity);
            return v.LengthSquared() > 4f
                && Vector2.Dot(Vector2.Normalize(v), Flat(p.Position - carrier.Position)) < -0.5f;
        }

        /// <summary>Defensive shell: with the other side carrying in the club's own zone, every skater not on
        /// the carrier drops into a box in front of the net, giving up the outside. With a balanced
        /// forecheck, while the other side breaks out of its zone, nobody goes in: forwards wait at the
        /// red line and the defence inside its own blue line.</summary>
        private static void Shell(
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
            List<Player> team = [.. Skaters(world, side).Where(p => p.Slot < 0)];
            if (ca < -RinkSpec.BlueLineX + 4f)
            {
                float lean = c.Y * 0.15f;
                Vector2[] house =
                [
                    new(-RinkSpec.GoalLineX + 2.5f, lean - 2.2f),
                    new(-RinkSpec.GoalLineX + 2.5f, lean + 2.2f),
                    new(-RinkSpec.GoalLineX + 6.5f, lean - 3.2f),
                    new(-RinkSpec.GoalLineX + 6.5f, lean + 3.2f),
                ];
                List<Player> free = [.. team.Where(p => !OnCarrier(world, p))];
                foreach (Vector2 spot in house)
                {
                    Vector2 s = new(end * spot.X, spot.Y);
                    if (free.MinBy(p => Vector2.Distance(Flat(p.Position), s)) is not { } p)
                    {
                        return;
                    }

                    _ = free.Remove(p);
                    Take(world, board, p, TacticTask.House, s, dt, sprint: false);
                }
                return;
            }

            if (
                ca <= RinkSpec.BlueLineX - 2f
                || TacticsState.Of(world, side).Forecheck != Forecheck.Balanced
            )
            {
                return;
            }

            float[] lanes = [-6f, 0f, 6f];
            List<Player> forwards =
            [
                .. team.Where(p => !Roles.IsDefence(PositioningModel.RoleOf(world, p)))
                    .OrderBy(p => p.Position.Z),
            ];
            for (int i = 0; i < forwards.Count; i++)
            {
                Vector2 spot = new(end * -1f, lanes[Math.Min(i, lanes.Length - 1)] + (c.Y * 0.2f));
                Take(
                    world,
                    board,
                    forwards[i],
                    TacticTask.Retreat,
                    TeamShape.Inside(spot),
                    dt,
                    sprint: false
                );
            }
            foreach (
                Player p in team.Where(p => Roles.IsDefence(PositioningModel.RoleOf(world, p)))
            )
            {
                float s = TeamShape.Lateral(PositioningModel.RoleOf(world, p), end);
                Vector2 spot = new(end * (-RinkSpec.BlueLineX - 1.5f), (s * 4f) + (c.Y * 0.2f));
                Take(
                    world,
                    board,
                    p,
                    TacticTask.Retreat,
                    TeamShape.Inside(spot),
                    dt,
                    sprint: false
                );
            }
        }
    }
}
