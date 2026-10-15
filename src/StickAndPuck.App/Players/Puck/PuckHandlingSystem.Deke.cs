using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Players;

internal sealed partial class PuckHandlingSystem
{
    /// <summary>Starts a deke when the carrier presses deke, or by itself for a human carrier when auto-deke is on and a defender reaches the sweet spot.</summary>
    void Deke(World world, Player carrier)
    {
        bool down = carrier.Command.Deke, pressed = down && !dekeHeld.Contains(carrier.Id);
        if (down) dekeHeld.Add(carrier.Id); else dekeHeld.Remove(carrier.Id);
        if (carrier.Action != PlayerAction.None || pendingShot.HasValue || deking.ContainsKey(carrier.Id)) return;
        if (world.Time - dekedAt.GetValueOrDefault(carrier.Id, float.NegativeInfinity) < DekeModel.Cooldown) return;

        var threat = DekeModel.Threat(world, carrier);
        if (!threat.HasValue) autoTried.Remove(carrier.Id);   // the next defender to come gets a fresh auto-deke
        int pc = carrier.Data.Traits.PuckControl;
        float chance;
        if (pressed) chance = threat.TryGet(out var t) ? DekeModel.Chance(DekeModel.Timing(t.Distance), pc, AI.HumanEdge.DekeFloor(world, carrier)) : 1f;
        else if (world.Settings.AutoDeke && carrier.Slot >= 0 && threat.TryGet(out var a) && a.Distance <= DekeModel.SweetFar && autoTried.Add(carrier.Id))
            chance = DekeModel.AutoChance(pc);
        else return;

        var defender = threat.Select(static x => x.Defender);
        bool beaten = rng.NextSingle() < chance;
        dekedAt[carrier.Id] = world.Time;
        deking[carrier.Id] = (world.Time, DekeModel.StepSide(carrier, defender), DekeModel.StepSpeed(pc, beaten || !defender.HasValue));
        if (!defender.TryGet(out var beat)) return;   // a shimmy in open ice: nothing to beat, nothing to lose
        if (beaten) Bite(carrier, beat);
        else Fumble(world, carrier);
    }

    /// <summary>The defender buys the fake: they lunge where the puck was, past the carrier, and are a moment recovering.</summary>
    static void Bite(Player carrier, Player defender)
    {
        if (defender.Action is PlayerAction.None or PlayerAction.Check)
        {
            // a check already past its contact window: the checking system lets them recover without hitting anyone
            defender.Begin(PlayerAction.Check);
            defender.ActionTime = CheckingModel.Window + 0.01f;
        }
        var fake = -DekeModel.StepSide(carrier, defender);
        defender.Velocity = defender.Velocity * 0.5f + new Vector3(fake.X, 0, fake.Y) * 2f;
    }

    /// <summary>A failed deke: the puck squirts off the blade ahead, free for whoever gets there first (the defender, usually).</summary>
    void Fumble(World world, Player carrier)
    {
        var travel = DekeModel.Travel(carrier);
        var side = new Vector2(-travel.Y, travel.X) * (rng.NextSingle() * 2f - 1f) * 1.5f;
        var push = travel * 2f + side;
        Release(world, carrier, carrier.Velocity * 0.6f + new Vector3(push.X, 0, push.Y));
    }

    /// <summary>Moves dekers sideways for the deke's length, inside the boards.</summary>
    void Sidestep(World world, float dt)
    {
        foreach (var (id, d) in deking.ToList())
        {
            var p = world.Players[id];
            if (world.Time - d.At >= DekeModel.Duration || p.Action is PlayerAction.Fallen or PlayerAction.Fight) { deking.Remove(id); continue; }
            var step = d.Side * d.Speed * dt;
            p.Position = PuckHandling.InsideBoards(p.Position + new Vector3(step.X, 0, step.Y));
        }
    }

    /// <summary>Where the puck is pulled during a deke: across the body and back again.</summary>
    Vector3 DekePull(World world, int carrier)
    {
        if (!deking.TryGetValue(carrier, out var d)) return Vector3.Zero;
        float k = SimMath.Sin(MathF.PI * Math.Clamp((world.Time - d.At) / DekeModel.Duration, 0f, 1f)) * 0.6f;
        return new Vector3(d.Side.X, 0, d.Side.Y) * k;
    }
}
