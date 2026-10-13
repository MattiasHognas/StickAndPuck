using StickAndPuck.App.Core;
using StickAndPuck.App.Rules;

namespace StickAndPuck.App.Fights;

internal sealed partial class FightSystem
{
    static void BeginExchange(World world, FightState s, Fight fight)
    {
        fight.Stage = FightStage.Exchange;
        fight.StageTime = 0;
        foreach (var f in fight.Both)
        {
            f.Punched = false;
            f.PunchAt = float.NaN;
            f.AiPunchAt = f.Slot < 0 && s.AiPunches ? FightModel.AiPunchAt(world.Players[f.PlayerId], s.Rng) : float.NaN;
        }
    }

    static void Exchange(World world, FightState s, Fight fight)
    {
        float t = fight.Sweep;
        foreach (var f in fight.Both)
        {
            bool block, punch;
            if (f.Slot >= 0)
            {
                var cmd = world.Controlled(f.Slot).TryGet(out var me) ? me.Command : default;
                block = cmd.Block;
                punch = cmd.Punch;
            }
            else
            {
                block = !float.IsNaN(f.AiBlockAt) && world.Time >= f.AiBlockAt;
                if (block && world.Time >= f.AiBlockAt + 0.5f) { f.AiBlockAt = float.NaN; block = false; }
                punch = !float.IsNaN(f.AiPunchAt) && t >= f.AiPunchAt;
            }

            if (block && !f.Blocking) { f.BlockSince = world.Time; SetClip(world, f, "block"); }
            if (!block && f.Blocking && float.IsNaN(f.LandsAt)) SetClip(world, f, "grab");
            f.Blocking = block;

            if (punch && !f.Punched && !block && t < 1f) Throw(world, s, fight, f, t);
        }

        foreach (var f in fight.Both)
        {
            if (!float.IsNaN(f.LandsAt) && world.Time >= f.LandsAt) Land(world, s, fight, f);
            if (fight.Stage != FightStage.Exchange) return;
            if (string.Equals(f.Clip, "punch", StringComparison.Ordinal) && world.Time - f.ClipSince > 0.6f) SetClip(world, f, f.Blocking ? "block" : "grab");
        }

        if (fight.StageTime < FightTuning.ExchangeSeconds || fight.Both.Any(f => !float.IsNaN(f.LandsAt))) return;
        fight.Exchange++;
        if (fight.Exchange < FightTuning.MaxExchanges)
        {
            BeginExchange(world, s, fight);
            return;
        }
        // the linesmen step in: less balance loses, the instigating checker on a tie
        var loser = fight.Victim.Balance < fight.Checker.Balance ? fight.Victim : fight.Checker;
        Decide(world, s, fight, loser, knockdown: false);
    }

    static void Throw(World world, FightState s, Fight fight, Fighter f, float t)
    {
        f.Punched = true;
        f.PunchAt = t;
        f.LandsAt = world.Time + FightTuning.Windup;
        SetClip(world, f, "punch");
        var other = fight.Other(f);
        if (other.Slot < 0 && s.AiBlocks && s.Rng.NextSingle() < FightModel.AiBlockChance(world.Players[other.PlayerId]))
            other.AiBlockAt = world.Time + 0.04f + (s.Rng.NextSingle() * 0.14f);
    }

    static void Land(World world, FightState s, Fight fight, Fighter f)
    {
        f.LandsAt = float.NaN;
        var other = fight.Other(f);
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
        var puncher = world.Players[f.PlayerId];
        var target = world.Players[other.PlayerId];
        other.Balance -= FightModel.Damage(puncher, target, quality);
        f.Landed++;
        fight.LandedAt = world.Time;
        // a landed punch sounds like a body check (audio is the only reader after this system)
        world.Events.Publish(new CheckLanded(world.Time, puncher.Side, puncher.Id, target.Position, 0.6f + (0.6f * quality)));
        if (other.Balance <= 0) Decide(world, s, fight, other, knockdown: true);
    }

    static void Decide(World world, FightState s, Fight fight, Fighter loser, bool knockdown)
    {
        fight.Stage = FightStage.Over;
        fight.StageTime = 0;
        fight.LoserId = loser.PlayerId;
        fight.Knockdown = knockdown;
        var winner = fight.Other(loser);
        SetClip(world, winner, "celebrate");
        SetClip(world, loser, knockdown ? "fall" : "idle");
        s.Log(world, $"{Penalties.Name(world, world.Players[winner.PlayerId])} wins {(knockdown ? "by knockdown" : "on balance")}");
    }
}
