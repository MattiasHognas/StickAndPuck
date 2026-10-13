using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players;

namespace StickAndPuck.App.AI;

internal sealed partial class SpecialTeamsSystem
{
    /// <summary>A killer with the puck in his own half clears it: after a beat (shorter for a smart one,
    /// longer on a fumbled read) he fires it down the ice through the least crowded lane, and flips
    /// it high when every lane has a stick in it. Decisions hold the shot (the slap shot hold).</summary>
    void Clear(World world, SpecialTeamsBoard board, Player p)
    {
        if (p.Slot >= 0) return;
        int end = PuckHandling.AttackEnd(world, p.Side);
        var at = Flat(world.Puck.Position);
        if (end * at.X > 0f) { board.ClearCarrier = -1; return; }   // over the red line: a short-handed rush is decisions' play
        if (board.ClearCarrier != p.Id)
        {
            board.ClearCarrier = p.Id;
            var (outcome, iq) = Jitter.Roll(world, p, rng);
            float beat = Traits.Scale(iq, 0.5f, 0.12f);
            if (outcome == ReadOutcome.Fumble) beat += 0.5f + 0.5f * rng.NextSingle();
            board.ClearAt = world.Time + beat;
        }
        if (AiCommand.Committed(world, p)) return;
        AiCommand.Hold(world, p);   // no carry, pass or shot of decisions' own in the meantime
        if (world.Time < board.ClearAt) return;

        float side = at.Y >= 0 ? 1f : -1f;
        // up the boards on either side, never straight at the far net
        Vector2[] lanes = [new(end, 0.45f * side), new(end, 0.2f * side), new(end, -0.3f * side)];
        var opponents = world.OnIce(p.Side == Side.Home ? Side.Away : Side.Home).Where(o => !o.IsGoalie).Select(o => Flat(o.Position)).ToList();
        var best = lanes.Select(Vector2.Normalize).MinBy(d => Crowd(at, d, opponents) + MathF.Abs(MathF.Abs(d.Y) - 0.41f));
        bool flip = Crowd(at, best, opponents) > 0;
        AiCommand.Shoot(world, p, AiLayer.SpecialTeams, best, flip ? 20 : 4, "kill clear");   // a flip is a short slap wind-up, lifted (a held shot), a clear along the ice a tap
        board.Clearing = new ClearWatch(p.Side, p.Id, flip, world.Time);
        board.ClearCarrier = -1;
    }

    /// <summary>Counts a clear once the puck leaves the killer's stick (a wind-up broken by a check is no
    /// clear), and as out once it crosses the killers' blue line, going away, within three seconds.</summary>
    static void TrackClear(World world, SpecialTeamsBoard board)
    {
        if (!board.Clearing.TryGet(out var c)) return;
        var pk = world.Puck;
        var tally = board.Tally[(int)c.Side];
        if (c.Released < 0)
        {
            if (pk.Carrier == c.Player && world.Time - c.At < 1.5f) return;
            if (pk.Carrier >= 0 || pk.LastTouch != c.Player || !pk.Live) { board.Clearing = Maybe<ClearWatch>.None; return; }
            tally.Clears++;
            if (c.Flip) tally.Flips++;
            board.Clearing = c with { Released = world.Time };
            return;
        }
        if (world.Time - c.Released > 3f) { board.Clearing = Maybe<ClearWatch>.None; return; }
        if (PuckHandling.AttackEnd(world, c.Side) * pk.Position.X > -RinkSpec.BlueLineX)
        {
            tally.ClearedOut++;
            board.Clearing = Maybe<ClearWatch>.None;
        }
    }

    /// <summary>How many opponents stand near a lane in its first 14 metres.</summary>
    static int Crowd(Vector2 from, Vector2 dir, List<Vector2> opponents)
    {
        int n = 0;
        foreach (var o in opponents)
        {
            float along = Vector2.Dot(o - from, dir);
            if (along is < 0.5f or > 14f) continue;
            if (Vector2.Distance(from + dir * along, o) < 1.8f) n++;
        }
        return n;
    }
}
