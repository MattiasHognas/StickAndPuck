using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players.Checks;

namespace StickAndPuck.App.Players.Puck
{
    internal sealed partial class PuckHandlingSystem
    {
        /// <summary>Starts a deke when the carrier presses deke, or by itself for a human carrier when auto-deke is on and a defender reaches the sweet spot.</summary>
        private void Deke(World world, Player carrier)
        {
            bool down = carrier.Command.Deke,
                pressed = down && !dekeHeld.Contains(carrier.Id);
            _ = down ? dekeHeld.Add(carrier.Id) : dekeHeld.Remove(carrier.Id);

            if (ShouldSkipDeke(world, carrier))
            {
                return;
            }

            Maybe<(Player Defender, float Distance)> threat = DekeModel.Threat(world, carrier);
            if (!threat.HasValue)
            {
                _ = autoTried.Remove(carrier.Id); // the next defender to come gets a fresh auto-deke
            }

            float chance = GetDekeChance(world, carrier, pressed, threat);
            if (chance < 0f)
            {
                return;
            }

            Maybe<Player> defender = threat.Select(static x => x.Defender);
            bool beaten = rng.NextSingle() < chance;
            StartDeke(world, carrier, defender, beaten);
            if (!defender.TryGet(out Player beat))
            {
                return; // a shimmy in open ice: nothing to beat, nothing to lose
            }

            if (beaten)
            {
                Bite(carrier, beat);
            }
            else
            {
                Fumble(world, carrier);
            }
        }

        private bool ShouldSkipDeke(World world, Player carrier)
        {
            return carrier.Action != PlayerAction.None
                || pendingShot.HasValue
                || deking.ContainsKey(carrier.Id)
                || world.Time - dekedAt.GetValueOrDefault(carrier.Id, float.NegativeInfinity)
                    < DekeModel.Cooldown;
        }

        private float GetDekeChance(
            World world,
            Player carrier,
            bool pressed,
            Maybe<(Player Defender, float Distance)> threat
        )
        {
            int pc = carrier.Data.Traits.PuckControl;

            return pressed
                ? threat.TryGet(out (Player Defender, float Distance) t)
                    ? DekeModel.Chance(
                        DekeModel.Timing(t.Distance),
                        pc,
                        AI.Difficulty.HumanEdge.DekeFloor(world, carrier)
                    )
                    : 1f
                : world.Settings.AutoDeke
                && carrier.Slot >= 0
                && threat.TryGet(out (Player Defender, float Distance) a)
                && a.Distance <= DekeModel.SweetFar
                && autoTried.Add(carrier.Id)
                    ? DekeModel.AutoChance(pc)
                    : -1f;
        }

        private void StartDeke(World world, Player carrier, Maybe<Player> defender, bool beaten)
        {
            dekedAt[carrier.Id] = world.Time;
            deking[carrier.Id] = (
                world.Time,
                DekeModel.StepSide(carrier, defender),
                DekeModel.StepSpeed(carrier.Data.Traits.PuckControl, beaten || !defender.HasValue)
            );
        }

        /// <summary>The defender buys the fake: they lunge where the puck was, past the carrier, and are a moment recovering.</summary>
        private static void Bite(Player carrier, Player defender)
        {
            if (defender.Action is PlayerAction.None or PlayerAction.Check)
            {
                // a check already past its contact window: the checking system lets them recover without hitting anyone
                defender.Begin(PlayerAction.Check);
                defender.ActionTime = CheckingModel.Window + 0.01f;
            }
            Vector2 fake = -DekeModel.StepSide(carrier, defender);
            defender.Velocity = (defender.Velocity * 0.5f) + (new Vector3(fake.X, 0, fake.Y) * 2f);
        }

        /// <summary>A failed deke: the puck squirts off the blade ahead, free for whoever gets there first (the defender, usually).</summary>
        private void Fumble(World world, Player carrier)
        {
            Vector2 travel = DekeModel.Travel(carrier);
            Vector2 side = new Vector2(-travel.Y, travel.X) * ((rng.NextSingle() * 2f) - 1f) * 1.5f;
            Vector2 push = (travel * 2f) + side;
            Release(world, carrier, (carrier.Velocity * 0.6f) + new Vector3(push.X, 0, push.Y));
        }

        /// <summary>Moves dekers sideways for the deke's length, inside the boards.</summary>
        private void Sidestep(World world, float dt)
        {
            foreach ((int id, (float At, Vector2 Side, float Speed) d) in deking.ToList())
            {
                Player p = world.Players[id];
                if (
                    world.Time - d.At >= DekeModel.Duration
                    || p.Action is PlayerAction.Fallen or PlayerAction.Fight
                )
                {
                    _ = deking.Remove(id);
                    continue;
                }
                Vector2 step = d.Side * d.Speed * dt;
                p.Position = PuckHandling.InsideBoards(p.Position + new Vector3(step.X, 0, step.Y));
            }
        }

        /// <summary>Where the puck is pulled during a deke: across the body and back again.</summary>
        private Vector3 DekePull(World world, int carrier)
        {
            if (!deking.TryGetValue(carrier, out (float At, Vector2 Side, float Speed) d))
            {
                return Vector3.Zero;
            }

            float k =
                SimMath.Sin(MathF.PI * Math.Clamp((world.Time - d.At) / DekeModel.Duration, 0f, 1f))
                * 0.6f;
            return new Vector3(d.Side.X, 0, d.Side.Y) * k;
        }
    }
}
