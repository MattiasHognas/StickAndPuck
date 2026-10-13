using System.Globalization;
using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Replays;

/// <summary>
/// W67: one 64-bit number for the match state after a step: every player, the puck, the score,
/// clock and flow, the penalties and the step's events, float by float to the bit. Two runs that
/// agree on it step after step played the same match; the first step where they differ is where
/// they split.
/// </summary>
public static class WorldChecksum
{
    const ulong Offset = 14695981039346656037UL, Prime = 1099511628211UL;

    /// <summary>Gets the checksum of the world as it is now.</summary>
    /// <param name="world">The match.</param>
    /// <returns>The checksum.</returns>
    public static ulong Of(World world)
    {
        ulong h = Offset;
        Add(ref h, world.Tick);
        Add(ref h, world.Time);
        foreach (var p in world.Players)
        {
            Add(ref h, p.Id);
            Add(ref h, p.Position);
            Add(ref h, p.Velocity);
            Add(ref h, p.Heading);
            Add(ref h, (p.OnIce ? 1 : 0) | ((int)p.Action << 1));
            Add(ref h, p.Slot);
            Add(ref h, p.ActionTime);
            Add(ref h, p.Energy);
            Add(ref h, p.Command);
        }
        var puck = world.Puck;
        Add(ref h, puck.Position);
        Add(ref h, puck.Velocity);
        Add(ref h, puck.Carrier);
        Add(ref h, puck.LastTouch);
        Add(ref h, puck.Live ? 1 : 0);
        var m = world.Match;
        Add(ref h, (int)m.Phase);
        Add(ref h, m.Period);
        Add(ref h, m.Clock);
        Add(ref h, m.Score[0]);
        Add(ref h, m.Score[1]);
        Add(ref h, m.Shots[0]);
        Add(ref h, m.Shots[1]);
        Add(ref h, m.Paused ? 1 : 0);
        foreach (var pen in m.Penalties)
        {
            Add(ref h, pen.PlayerId);
            Add(ref h, pen.SecondsLeft);
        }
        foreach (var e in world.Events.Current)
        {
            Add(ref h, (int)e.Kind);
            Add(ref h, e.Side is { } side ? (int)side : -1);
            Add(ref h, e.PlayerId);
            Add(ref h, e.Position);
            Add(ref h, e.Strength);
        }
        return h;
    }

    /// <summary>Writes a checksum the way replay files and checksum logs show it.</summary>
    /// <param name="sum">The checksum.</param>
    /// <returns>16 hex digits.</returns>
    public static string Text(ulong sum) => sum.ToString("x16", CultureInfo.InvariantCulture);

    static void Add(ref ulong h, long v)
    {
        for (int i = 0; i < 8; i++)
        {
            h ^= (byte)(v >> (8 * i));
            h *= Prime;
        }
    }

    static void Add(ref ulong h, float v) => Add(ref h, BitConverter.SingleToInt32Bits(v));

    static void Add(ref ulong h, Vector3 v)
    {
        Add(ref h, v.X);
        Add(ref h, v.Y);
        Add(ref h, v.Z);
    }

    static void Add(ref ulong h, PlayerCommand c)
    {
        Add(ref h, c.Move.X);
        Add(ref h, c.Move.Y);
        int bits = (c.Sprint ? 1 : 0) | (c.Shoot ? 2 : 0) | (c.Pass ? 4 : 0) | (c.Check ? 8 : 0) | (c.Deke ? 16 : 0) | (c.SwitchPlayer ? 32 : 0)
            | (c.ChangeLine ? 64 : 0) | (c.Punch ? 128 : 0) | (c.Block ? 256 : 0) | (c.AcceptFight ? 512 : 0) | (c.Pause ? 1024 : 0);
        Add(ref h, bits);
    }
}
