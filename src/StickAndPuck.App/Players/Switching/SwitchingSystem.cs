using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players.Dev;

namespace StickAndPuck.App.Players;

/// <summary>Moves each human slot between its club's skaters (sets <see cref="Player.Slot"/>) as
/// <see cref="Settings.Switching"/> says. Control following a pass happens inside puck handling at
/// the moment of the pass; this system covers the rest.
/// <list type="bullet">
/// <item>The switch button (C, or gamepad Y) moves control to the teammate nearest the puck, never
/// while you carry it. It works in every mode.</item>
/// <item>Auto and PassesOnly: when a teammate picks up the puck, you control the carrier.</item>
/// <item>Auto: when the other club gets the puck, control goes to your skater nearest it; while
/// defending, it moves again if another teammate gets clearly nearer, but not more often than
/// <see cref="Settle"/>. If your player is knocked down, control goes to the nearest one standing.</item>
/// </list>
/// A slot that controls nobody (a demo scene) is left alone. The slot's stick carries over to the new player this step and the old player's command is
/// cleared, so they glide on rather than keep skating on the human's last input.</summary>
internal sealed class SwitchingSystem : ISystem
{
    /// <summary>Seconds after a switch before defence switching moves control again.</summary>
    public const float Settle = 0.8f;

    /// <summary>How much nearer the puck another teammate must be, in metres, to take control on defence.</summary>
    public const float Nearer = 3f;

    readonly bool[] defending = new bool[MatchSetup.Slots];
    readonly float[] switchedAt = [.. Enumerable.Repeat(float.NegativeInfinity, MatchSetup.Slots)];

    public void Update(World world, float dt)
    {
        ChecksDemo.Update(world);
        var inputs = world.Setup.Inputs;
        var pk = world.Puck;
        var carrier = pk.Carrier >= 0 && pk.Carrier < world.Players.Count ? world.Players[pk.Carrier] : Maybe<Player>.None;
        var mode = world.Settings.Switching;

        for (int slot = 0; slot < MatchSetup.Slots && slot < inputs.Count; slot++)
        {
            if (inputs[slot].Device == InputDevice.None) continue;
            var side = inputs[slot].Side;
            var controlled = world.Controlled(slot);
            bool defence = carrier.TryGet(out var held) && held.Side != side;
            bool newDefence = defence && !defending[slot];
            defending[slot] = defence;

            if (!controlled.TryGet(out var me)) continue;   // a scene with no human player is left to itself

            var want = Maybe<Player>.None;
            if (me.Command.SwitchPlayer)
            {
                if (carrier != me) want = Nearest(world, side, slot, me);
            }
            else if (mode != ControlSwitching.Manual && carrier.TryGet(out var c) && !c.IsGoalie && c.Side == side && c != me && c.Slot < 0)
            {
                want = c;
            }
            else if (mode == ControlSwitching.Auto && me.Action == PlayerAction.Fallen && carrier != me)
            {
                want = Nearest(world, side, slot, me);
            }
            else if (mode == ControlSwitching.Auto && defence &&
                Nearest(world, side, slot, Maybe<Player>.None).TryGet(out var near) && near != me &&
                (newDefence || (world.Time - switchedAt[slot] >= Settle && ToPuck(world, near) + Nearer < ToPuck(world, me))))
            {
                want = near;
            }

            if (want.TryGet(out var to) && to != me)
            {
                Move(me, to, slot);
                switchedAt[slot] = world.Time;
            }
        }
    }

    /// <summary>Gets the slot's club skater nearest the puck: on the ice, not a goalie, not another slot's, standing if any is.</summary>
    /// <param name="world">The match.</param>
    /// <param name="side">The slot's club.</param>
    /// <param name="slot">The slot.</param>
    /// <param name="except">A player to leave out, usually the one already controlled; None leaves nobody out.</param>
    internal static Maybe<Player> Nearest(World world, Side side, int slot, Maybe<Player> except)
    {
        return world.Players
            .Where(p => p.Side == side && p.OnIce && !p.IsGoalie && p != except && (p.Slot < 0 || p.Slot == slot))
            .OrderBy(p => p.Action == PlayerAction.Fallen)
            .ThenBy(p => ToPuck(world, p))
            .FirstOrNone();
    }

    static float ToPuck(World world, Player p)
    {
        var d = world.Puck.Position - p.Position;
        return new Vector2(d.X, d.Z).Length();
    }

    static void Move(Player from, Player to, int slot)
    {
        var carried = from.Command;
        from.Slot = -1;
        from.Command = default;
        to.Command = carried with { Pass = false, Check = false, SwitchPlayer = false, Shoot = false };
        to.Slot = slot;
    }
}
