using System.Numerics;
using StickAndPuck.App.AI.Difficulty;
using StickAndPuck.App.AI.Positioning;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players.Checks;
using StickAndPuck.App.Players.Movement;
using StickAndPuck.App.Players.Puck;

namespace StickAndPuck.App.AI.Decisions
{
    internal sealed partial class DecisionSystem
    {
        /// <summary>The teammate a pass is meant for skates onto its line to meet it.</summary>
        private static bool MeetPass(World world, DecisionBoard board, Player p)
        {
            Puck pk = world.Puck;
            if (!board.Pass.TryGet(out InFlight pass) || pass.Receiver != p.Id || pk.Carrier >= 0)
            {
                return false;
            }

            Vector2 v = Flat(pk.Velocity);
            if (v.Length() < 1.5f)
            {
                return false;
            }

            Vector2 from = Flat(pk.Position);
            Vector2 blade = Flat(PuckHandling.Blade(p));
            Vector2 dir = Vector2.Normalize(v);
            float along = Intercept(p, blade, from, dir, v.Length()).TryGet(out float reach)
                ? reach
                : Math.Max(0.5f, Vector2.Dot(blade - from, dir));
            Vector2 meet = TeamShape.Inside(from + (dir * along), 0.6f);
            AiCommand.Steer(
                world,
                p,
                AiLayer.Decisions,
                Steering.Toward(p, Steering.BodyForBlade(p, meet), 0.6f),
                "meet the pass"
            );
            return true;
        }

        /// <summary>Skates the human receiver of a human's pass to meet it, over the stick still held from
        /// the pass, until the player steers him with fresh input (see the class remarks).</summary>
        private static void MeetForHuman(World world, DecisionBoard board)
        {
            Puck pk = world.Puck;
            if (
                !board.Meet.TryGet(out HumanMeet meet)
                || meet.HandedBack
                || !board.Pass.TryGet(out InFlight pass)
                || pass.Receiver != meet.Receiver
                || pk.Carrier >= 0
            )
            {
                return;
            }

            Player p = world.Players[meet.Receiver];
            if (p.Slot < 0 || !p.OnIce || p.Action is PlayerAction.Fallen or PlayerAction.Fight)
            {
                return; // an AI receiver meets it in the loop
            }

            if (pk.LastTouch != pass.Passer)
            {
                meet.HandedBack = true;
                return;
            } // one-timed, tipped or saved: the pass is over

            Vector2 stick = p.Command.Move;
            if (stick.Length() < Pushed)
            {
                meet.Released = true;
            }
            else if (
                meet.Released
                || Vector2.Dot(Vector2.Normalize(stick), Vector2.Normalize(meet.Held))
                    < SimMath.Cos(FreshTurn)
            )
            {
                meet.HandedBack = true;
                return;
            }

            PlayerCommand move = Receive(p, pk).TryGet(out PlayerCommand receive)
                ? receive
                : Steering.Toward(
                    p,
                    Steering.BodyForBlade(p, TeamShape.Inside(Flat(pk.Position), 0.6f)),
                    0.6f
                );
            p.Command = p.Command with { Move = move.Move, Sprint = move.Sprint };
        }

        /// <summary>Where the human receiver goes to take the pass: the point on the puck's slide nearest
        /// where his blade drifts on his current skating, among those he can reach in time, and his
        /// body shifted so the blade, at the heading he has, sits on it. A receiver on the line stays
        /// put; one led by the pass skates on into it. Chasing the earliest point instead (as an AI
        /// receiver does) turns him toward the puck and swings his blade off its line. None when the
        /// puck has all but stopped.</summary>
        private static Maybe<PlayerCommand> Receive(Player p, Puck pk)
        {
            const float step = 1 / 60f;
            Vector2 v = Flat(pk.Velocity);
            float speed = v.Length();
            if (speed < 1.5f)
            {
                return Maybe.None<PlayerCommand>();
            }

            Vector2 dir = v / speed;
            Vector2 puck = Flat(pk.Position);
            Vector2 blade = Flat(PuckHandling.Blade(p));
            Vector2 drift = Flat(p.Velocity);
            float top = SkatingModel.TopSpeed(p.Data.Traits, p.Energy) * 0.6f; // from a glide, turning: well short of top speed
            float best = float.MaxValue;
            Vector2 meet = puck;
            for (float t = step; speed > 0.1f && t < 3f; t += step)
            {
                puck += dir * speed * step;
                speed -=
                    (Physics.PuckTuning.IceFriction + (Physics.PuckTuning.Drag * speed)) * step;
                float gap = Vector2.Distance(puck, blade + (drift * t));
                float score = gap + (10f * Math.Max(0f, gap - (top * t)));
                if (score < best)
                {
                    best = score;
                    meet = puck;
                }
            }
            return Steering.Toward(
                p,
                TeamShape.Inside(Flat(p.Position) + meet - blade, 0.6f),
                0.6f
            );
        }

        /// <summary>The earliest point along the puck's slide the receiver's blade can reach before the puck
        /// does, in metres from the puck, or None when he cannot get to it in time anywhere. Going to
        /// the point across from where he stands instead braked him sideways while a pass led ahead of
        /// him ran on, so the more a smart passer led him the more passes he missed.</summary>
        private static Maybe<float> Intercept(
            Player p,
            Vector2 blade,
            Vector2 from,
            Vector2 dir,
            float speed
        )
        {
            const float step = 1 / 60f;
            float top = SkatingModel.TopSpeed(p.Data.Traits, p.Energy),
                along = 0f,
                t = 0f;
            while (speed > 0.1f && t < 5f)
            {
                along += speed * step;
                speed -=
                    (Physics.PuckTuning.IceFriction + (Physics.PuckTuning.Drag * speed)) * step;
                t += step;
                if (along >= 0.5f && Vector2.Distance(blade, from + (dir * along)) <= top * t)
                {
                    return along;
                }
            }
            return Maybe.None<float>();
        }

        /// <summary>The chaser close to a slow loose puck skates right over it, easing off, so the puck is
        /// taken at the skates or the blade; going for the blade at speed only circles the puck.</summary>
        private static void Pounce(World world, Player p)
        {
            Puck pk = world.Puck;
            Vector2 puck = Flat(pk.Position);
            if (Flat(pk.Velocity).Length() > 4f || Vector2.Distance(puck, Flat(p.Position)) > 4f)
            {
                return;
            }

            AiCommand.Steer(
                world,
                p,
                AiLayer.Decisions,
                Steering.Toward(p, TeamShape.Inside(puck, SkatingModel.BodyRadius), 1.5f),
                "pounce"
            );
        }

        /// <summary>After a shot, the forward of the shooting side nearest the net who is not chasing goes to the net for a rebound.</summary>
        private static int Crasher(World world, DecisionBoard board)
        {
            if (
                !board.Shot.TryGet(out (Side Side, float At) shot)
                || world.Time - shot.At > 1.6f
                || world.Puck.Carrier >= 0
            )
            {
                return -1;
            }

            int end = PuckHandling.AttackEnd(world, shot.Side);
            Vector3 net = new(end * RinkSpec.GoalLineX, 0, 0);
            return world
                    .OnIce(shot.Side)
                    .Where(o =>
                        !o.IsGoalie
                        && o.Slot < 0
                        && !Roles.IsDefence(PositioningModel.RoleOf(world, o))
                        && PositioningModel.JobOf(world, o) != Job.Chase
                        && Vector3.Distance(o.Position, net) < 14f
                    )
                    .MinBy(o => Vector3.Distance(o.Position, net))
                    ?.Id
                ?? -1;
        }

        private static void CrashNet(World world, Player p)
        {
            int end = PuckHandling.AttackEnd(world, p.Side);
            float z = world.Puck.Position.Z > 0 ? -0.9f : 0.9f;
            AiCommand.Steer(
                world,
                p,
                AiLayer.Decisions,
                Steering.Toward(
                    p,
                    new Vector2(end * (RinkSpec.GoalLineX - 2.4f), z),
                    0.8f,
                    sprint: true
                ),
                "crash the net"
            );
        }

        /// <summary>The skater pressing a carrier decides once per approach whether to hit, then skates through and checks.</summary>
        private void Hit(World world, DecisionBoard board, Player p, Brain brain)
        {
            Puck pk = world.Puck;
            if (
                pk.Carrier < 0
                || pk.Carrier >= world.Players.Count
                || p.Action != PlayerAction.None
            )
            {
                return;
            }

            Player carrier = world.Players[pk.Carrier];
            if (
                carrier.Side == p.Side
                || carrier.IsGoalie
                || PositioningModel.JobOf(world, p) != Job.Pressure
            )
            {
                return;
            }

            float d = Vector3.Distance(p.Position, carrier.Position);

            if (world.Time >= brain.CheckReady && d < CheckFrom)
            {
                float checking = Traits.Unit(p.Data.Traits.Checking);
                brain.HitUntil =
                    rng.NextSingle()
                    < (0.04f + (0.15f * checking)) * HumanEdge.HitChance(world, carrier)
                        ? world.Time + 0.9f
                        : float.NegativeInfinity;
                brain.CheckReady = world.Time + 4f;
            }
            if (world.Time > brain.HitUntil)
            {
                return;
            }

            Vector3 lead = carrier.Position + (carrier.Velocity * 0.2f);
            AiCommand.Steer(
                world,
                p,
                AiLayer.Decisions,
                Steering.Toward(p, Flat(lead), 0.1f, sprint: true),
                "line up a hit"
            );
            if (CheckingModel.Target(world, p) == carrier)
            {
                AiCommand.Act(world, p, p.Command with { Check = true }, "hit");
                brain.HitUntil = float.NegativeInfinity;
                board.Tally[(int)p.Side].Checks++;
            }
        }
    }
}
