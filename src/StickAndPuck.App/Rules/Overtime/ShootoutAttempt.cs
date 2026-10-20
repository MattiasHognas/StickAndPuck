using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players.Movement;
using StickAndPuck.App.Players.Puck;

namespace StickAndPuck.App.Rules.Overtime
{
    /// <summary>One shooter alone from centre against the goalie: a shootout attempt, and a penalty shot
    /// (<see cref="Penalties.PenaltyShot"/>) the same way. <see cref="Hold"/> keeps the shooter and the dead puck at
    /// centre, <see cref="Go"/> puts the puck live on his stick, and <see cref="Step"/> watches the attempt to its end,
    /// skating in and shooting for an AI shooter. It ends on a goal, the goalie covering the puck, the puck going
    /// dead, away or behind the goal line, or time running out; no rebounds.</summary>
    internal sealed class ShootoutAttempt
    {
        /// <summary>Gets or sets how long the attempt has run.</summary>
        public float Time { get; set; }

        /// <summary>Gets or sets how long the puck has been off the shooter's stick.</summary>
        public float LooseTime { get; set; }

        /// <summary>Gets or sets the AI shooter's lane (z).</summary>
        public float Lane { get; set; }

        /// <summary>Gets or sets the AI shooter's distance to the goal line when shooting.</summary>
        public float ShootFrom { get; set; }

        /// <summary>Gets or sets the side an AI shooter's deke cuts to (-1 or +1), or 0 for a straight shot.</summary>
        public int Deke { get; set; }

        /// <summary>Keeps the shooter and the dead puck at centre until the attempt starts.</summary>
        public static void Hold(World world, Player shooter)
        {
            int attack = PuckHandling.AttackEnd(world, shooter.Side);
            shooter.Position = RinkSpec.CentreDot - new Vector3(attack * 1.2f, 0, 0);
            shooter.Velocity = Vector3.Zero;
            shooter.Heading = attack > 0 ? MathF.PI / 2 : -MathF.PI / 2;
            Puck pk = world.Puck;
            pk.Live = false;
            pk.Carrier = -1;
            pk.Position = RinkSpec.CentreDot;
            pk.Velocity = Vector3.Zero;
        }

        /// <summary>Starts the attempt: the puck is live on the shooter's stick; an AI shooter picks a lane and a distance to shoot from.</summary>
        public void Go(World world, Player shooter, Random rng)
        {
            Puck pk = world.Puck;
            pk.Live = true;
            pk.Carrier = shooter.Id;
            pk.LastTouch = shooter.Id;
            pk.Position = PuckHandling.Blade(shooter);
            Time = 0;
            LooseTime = 0;
            Lane = ((rng.NextSingle() * 2) - 1) * OvertimeTuning.LaneWidth;
            ShootFrom =
                OvertimeTuning.ShootNear
                + (rng.NextSingle() * (OvertimeTuning.ShootFar - OvertimeTuning.ShootNear));
            Deke = 0;
            float deke =
                OvertimeTuning.DekeChance
                + (
                    (OvertimeTuning.DekeChanceBest - OvertimeTuning.DekeChance)
                    * Traits.Unit(shooter.Data.Traits.PuckControl)
                );
            if (rng.NextSingle() < deke)
            {
                // start wide on one side and cut across to the other
                Deke = (rng.Next(2) * 2) - 1;
                Lane = -Deke * 1.6f;
                ShootFrom = 1.8f;
            }
            world.Events.Publish(new PuckDropped(world.Time, RinkSpec.CentreDot));
        }

        /// <summary>Watches the attempt one step; skates and shoots for an AI shooter.</summary>
        /// <returns>None while the attempt goes on; whether it scored once it is over.</returns>
        public Maybe<bool> Step(World world, Player shooter, Random rng, float dt)
        {
            Puck pk = world.Puck;
            Time += dt;

            // physics ran earlier this step, so a goal shows up here
            if (world.Events.Of<GoalScored>().Any(e => e.Side == shooter.Side))
            {
                return true;
            }

            bool carried = pk.Carrier == shooter.Id;
            if (pk.Carrier >= 0 && !carried)
            {
                return false; // the goalie covered it
            }

            if (carried && LooseTime > 0)
            {
                return false; // no rebounds
            }

            if (!carried)
            {
                LooseTime += dt;
            }

            int attack = PuckHandling.AttackEnd(world, shooter.Side);
            bool behind = attack * pk.Position.X > RinkSpec.GoalLineX + 0.3f;
            bool dead =
                !carried && LooseTime > 0.3f && pk.Velocity.Length() < OvertimeTuning.DeadSpeed;
            bool away = !carried && LooseTime > 0.3f && attack * pk.Velocity.X < -1f;
            if (
                behind
                || dead
                || away
                || LooseTime > OvertimeTuning.LooseSeconds
                || Time > OvertimeTuning.AttemptSeconds
            )
            {
                return false;
            }

            if (carried && shooter.Slot < 0)
            {
                SkateIn(world, shooter, attack, rng, dt);
            }

            return Maybe.None<bool>();
        }

        /// <summary>An AI shooter skates at the net along its lane and shoots from its distance.</summary>
        private void SkateIn(World world, Player shooter, int attack, Random rng, float dt)
        {
            Traits traits = shooter.Data.Traits;
            float toLine = RinkSpec.GoalLineX - (attack * shooter.Position.X);
            Vector2 net = new(attack * RinkSpec.GoalLineX, 0);
            Vector2 here = new(shooter.Position.X, shooter.Position.Z);
            // a deke cuts across the crease and shoots to the side it is moving to, behind the goalie's shuffle
            bool cutting = Deke != 0 && toLine <= OvertimeTuning.DekeFrom;
            if (
                toLine <= ShootFrom
                || (cutting && Deke * shooter.Position.Z >= OvertimeTuning.DekeShootZ)
            )
            {
                Shoot(world, shooter, net, here, Deke != 0 ? Deke : 0, rng);
                return;
            }

            // skate the lane toward where the shot or the cut starts
            float dekeFrom = Deke != 0 ? OvertimeTuning.DekeFrom : 0;
            float shootFrom = Math.Max(ShootFrom, dekeFrom);
            Vector2 target = cutting
                ? new Vector2(attack * (RinkSpec.GoalLineX - 1.5f), Deke * 1.1f)
                : new Vector2(attack * (RinkSpec.GoalLineX - shootFrom + 1f), Lane);
            Vector2 dir = Vector2.Normalize(target - here);
            float top = SkatingModel.TopSpeed(traits, shooter.Energy) * OvertimeTuning.SkateIn;
            float speed = Math.Min(
                top,
                shooter.Speed + (SkatingModel.Push(traits, shooter.Energy) * dt)
            );
            shooter.Velocity = new Vector3(dir.X * speed, 0, dir.Y * speed);
            shooter.Heading = SimMath.Atan2(dir.X, dir.Y);
        }

        /// <summary>A wrist shot at a corner: the far side when coming in from a wing, or the given side after a deke.</summary>
        private static void Shoot(
            World world,
            Player shooter,
            Vector2 net,
            Vector2 here,
            int side,
            Random rng
        )
        {
            Vector2 toNet = Vector2.Normalize(net - here);
            shooter.Heading = SimMath.Atan2(toNet.X, toNet.Y);
            // the puck leaves the blade lifted, so the stick that fired it cannot take it straight back
            // (puck handling guards only the shots it fires itself)
            Puck pk = world.Puck;
            Vector3 from = pk.Position with { Y = PuckHandling.StickHeight + 0.02f };
            float corner =
                (side != 0)
                    ? side
                    : (
                        (MathF.Abs(from.Z) > 0.5f) ? (-MathF.Sign(from.Z)) : ((rng.Next(2) * 2) - 1)
                    );
            // aim like a human pushing the stick toward that corner
            shooter.Command = shooter.Command with
            {
                Move = Vector2.Normalize(toNet + new Vector2(0, corner * 0.8f)),
            };
            Vector3 velocity = PuckHandling.ShotVelocity(
                world,
                shooter,
                from,
                ShotKind.Wrist,
                PuckHandling.WristSpeed(shooter.Data.Traits),
                rng
            );
            shooter.Command = default;
            velocity.Y = Math.Max(velocity.Y, 1.5f);
            pk.Position = from;
            pk.Carrier = -1;
            pk.LastTouch = shooter.Id;
            pk.Velocity = velocity;
            shooter.Begin(PlayerAction.Shoot);
            world.Events.Publish(
                new ShotTaken(world.Time, shooter.Side, shooter.Id, from, velocity.Length())
            );
        }
    }
}
