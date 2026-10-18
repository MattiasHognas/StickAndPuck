using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Penalties;

namespace StickAndPuck.App.Fights
{
    internal sealed partial class FightSystem
    {
        private static void BeginExchange(World world, FightState s, Fight fight)
        {
            fight.Stage = FightStage.Exchange;
            fight.StageTime = 0;
            foreach (Fighter f in fight.Both)
            {
                f.Punched = false;
                f.PunchAt = float.NaN;
                f.AiPunchAt =
                    f.Slot < 0 && s.AiPunches
                        ? FightModel.AiPunchAt(world.Players[f.PlayerId], s.Rng)
                        : float.NaN;
            }
        }

        private static void Exchange(World world, FightState s, Fight fight)
        {
            float t = fight.Sweep;
            UpdateExchangeActions(world, s, fight, t);
            ResolveExchangeResults(world, fight);

            if (ShouldContinueExchange(fight))
            {
                return;
            }

            fight.Exchange++;
            if (fight.Exchange < FightTuning.MaxExchanges)
            {
                BeginExchange(world, s, fight);
                return;
            }
            // the linesmen step in: less balance loses, the instigating checker on a tie
            Fighter loser =
                fight.Victim.Balance < fight.Checker.Balance ? fight.Victim : fight.Checker;
            Decide(world, fight, loser, knockdown: false);
        }

        private static void UpdateExchangeActions(World world, FightState s, Fight fight, float t)
        {
            foreach (Fighter f in fight.Both)
            {
                bool block,
                    punch;
                if (f.Slot >= 0)
                {
                    PlayerCommand cmd = world.Controlled(f.Slot).TryGet(out Player me)
                        ? me.Command
                        : default;
                    block = cmd.Block;
                    punch = cmd.Punch;
                }
                else
                {
                    block = !float.IsNaN(f.AiBlockAt) && world.Time >= f.AiBlockAt;
                    if (block && world.Time >= f.AiBlockAt + 0.5f)
                    {
                        f.AiBlockAt = float.NaN;
                        block = false;
                    }
                    punch = !float.IsNaN(f.AiPunchAt) && t >= f.AiPunchAt;
                }

                if (block && !f.Blocking)
                {
                    f.BlockSince = world.Time;
                    SetClip(world, f, "block");
                }
                if (!block && f.Blocking && float.IsNaN(f.LandsAt))
                {
                    SetClip(world, f, "grab");
                }

                f.Blocking = block;

                if (punch && !f.Punched && !block && t < 1f)
                {
                    Throw(world, s, fight, f, t);
                }
            }
        }

        private static void ResolveExchangeResults(World world, Fight fight)
        {
            foreach (Fighter f in fight.Both)
            {
                if (!float.IsNaN(f.LandsAt) && world.Time >= f.LandsAt)
                {
                    Land(world, fight, f);
                }

                if (fight.Stage != FightStage.Exchange)
                {
                    return;
                }

                if (
                    string.Equals(f.Clip, "punch", StringComparison.Ordinal)
                    && world.Time - f.ClipSince > 0.6f
                )
                {
                    SetClip(world, f, f.Blocking ? "block" : "grab");
                }
            }
        }

        private static bool ShouldContinueExchange(Fight fight)
        {
            return fight.StageTime < FightTuning.ExchangeSeconds
                || fight.Both.Any(f => !float.IsNaN(f.LandsAt));
        }

        private static void Throw(World world, FightState s, Fight fight, Fighter f, float t)
        {
            f.Punched = true;
            f.PunchAt = t;
            f.LandsAt = world.Time + FightTuning.Windup;
            SetClip(world, f, "punch");
            Fighter other = fight.Other(f);
            if (
                other.Slot < 0
                && s.AiBlocks
                && s.Rng.NextSingle() < FightModel.AiBlockChance(world.Players[other.PlayerId])
            )
            {
                other.AiBlockAt = world.Time + 0.04f + (s.Rng.NextSingle() * 0.14f);
            }
        }

        private static void Land(World world, Fight fight, Fighter f)
        {
            f.LandsAt = float.NaN;
            Fighter other = fight.Other(f);
            float quality = FightModel.Quality(f.PunchAt);
            if (quality <= 0)
            {
                f.Missed++;
                return;
            }
            if (other.Blocking && world.Time - other.BlockSince <= FightTuning.BlockFresh)
            {
                f.Blocked++;
                return;
            }
            Player puncher = world.Players[f.PlayerId];
            Player target = world.Players[other.PlayerId];
            other.Balance -= FightModel.Damage(puncher, target, quality);
            f.Landed++;
            fight.LandedAt = world.Time;
            // a landed punch sounds like a body check (audio is the only reader after this system)
            world.Events.Publish(
                new CheckLanded(
                    world.Time,
                    puncher.Side,
                    puncher.Id,
                    target.Position,
                    0.6f + (0.6f * quality)
                )
            );
            if (other.Balance <= 0)
            {
                Decide(world, fight, other, knockdown: true);
            }
        }

        private static void Decide(World world, Fight fight, Fighter loser, bool knockdown)
        {
            fight.Stage = FightStage.Over;
            fight.StageTime = 0;
            fight.LoserId = loser.PlayerId;
            fight.Knockdown = knockdown;
            Fighter winner = fight.Other(loser);
            SetClip(world, winner, "celebrate");
            SetClip(world, loser, knockdown ? "fall" : "idle");
            FightState.Log(
                world,
                $"{PenaltyCalls.Name(world, world.Players[winner.PlayerId])} wins {(knockdown ? "by knockdown" : "on balance")}"
            );
        }
    }
}
