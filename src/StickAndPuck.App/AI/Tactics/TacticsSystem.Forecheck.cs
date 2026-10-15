using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players;

namespace StickAndPuck.App.AI;

internal sealed partial class TacticsSystem
{
    /// <summary>2-1-2: with the puck in the other side's half, a second forward takes the carrier's nearest
    /// outlet, the third sits high, and the defence pinch at the blue line (or stand up at the red
    /// line while the puck is still outside the zone). The man on the puck stays positioning's and
    /// defence's.</summary>
    void Aggressive(World world, TacticsBoard board, Side side, Player carrier, float dt)
    {
        int end = PuckHandling.AttackEnd(world, side);
        var c = Flat(carrier.Position);
        float ca = end * c.X;
        if (ca <= 0f) return;
        var team = Skaters(world, side).Where(p => p.Slot < 0 && !OnCarrier(world, p)).ToList();
        var forwards = team.Where(p => !Roles.IsDefence(Positioning.RoleOf(world, p))).ToList();
        var defence = team.Where(p => Roles.IsDefence(Positioning.RoleOf(world, p))).ToList();

        if (Skaters(world, carrier.Side).Where(p => p != carrier).MinBy(p => Vector3.Distance(p.Position, carrier.Position)) is { } outlet && forwards.Count > 0)
        {
            var o = Flat(outlet.Position);
            var second = forwards.MinBy(p => Vector2.Distance(Flat(p.Position), o));
            forwards.Remove(second);
            var between = o + SafeNormal(c - o) * 1.5f;
            Take(world, board, second, TacticTask.Outlet, TeamShape.Inside(between), dt, sprint: true);
        }

        bool deep = ca > RinkSpec.BlueLineX;
        foreach (var p in forwards)
        {
            var high = deep ? new Vector2(end * (RinkSpec.BlueLineX + 4f), c.Y * 0.3f) : new Vector2(end * Math.Max(ca - 4f, 0.5f), c.Y * 0.3f);
            Take(world, board, p, TacticTask.High, TeamShape.Inside(high), dt, sprint: false);
        }
        foreach (var p in defence)
        {
            float s = TeamShape.Lateral(Positioning.RoleOf(world, p), end);
            var pinch = deep ? new Vector2(end * (RinkSpec.BlueLineX + 0.8f), s * 5.5f + c.Y * 0.2f) : new Vector2(0f, s * 4f + c.Y * 0.2f);
            Take(world, board, p, TacticTask.Pinch, TeamShape.Inside(pinch), dt, sprint: false);
        }
    }

    /// <summary>1-3-1: with the puck in the other side's zone (or just out of it), the man nearest the
    /// carrier angles him at their blue line without going in deep, three wait in a line across
    /// the red line and the last defenceman stays back by his own blue line.</summary>
    void Trap(World world, TacticsBoard board, Side side, Player carrier, float dt)
    {
        int end = PuckHandling.AttackEnd(world, side);
        var c = Flat(carrier.Position);
        float ca = end * c.X;
        if (ca <= RinkSpec.BlueLineX - 2f) return;
        var team = Skaters(world, side).Where(p => p.Slot < 0).ToList();
        if (team.Count == 0) return;

        // the first forward: whoever positioning sent at the carrier, else the nearest forward
        if ((team.FirstOrDefault(p => OnCarrier(world, p))
            ?? team.Where(p => !Roles.IsDefence(Positioning.RoleOf(world, p))).MinBy(p => Vector2.Distance(Flat(p.Position), c))) is { } first)
        {
            team.Remove(first);
            var angle = new Vector2(end * Math.Min(ca - 3f, RinkSpec.BlueLineX + 2f), c.Y * 0.6f);
            Take(world, board, first, TacticTask.Angle, TeamShape.Inside(angle), dt, sprint: false);
        }

        // the defenceman furthest back stays back; the other three make the wall
        if (team.Where(p => Roles.IsDefence(Positioning.RoleOf(world, p))).MinBy(p => end * p.Position.X) is { } back && team.Count > 3)
        {
            team.Remove(back);
            var spot = new Vector2(end * (-RinkSpec.BlueLineX - 2f), c.Y * 0.2f);
            Take(world, board, back, TacticTask.Back, TeamShape.Inside(spot), dt, sprint: false);
        }
        float[] lanes = team.Count switch { 1 => [0f], 2 => [-4f, 4f], _ => [-7f, 0f, 7f] };
        var wall = team.OrderBy(p => p.Position.Z).Take(lanes.Length).ToList();
        for (int i = 0; i < wall.Count; i++)
        {
            var spot = new Vector2(end * -1.5f, lanes[i] + c.Y * 0.25f);
            Take(world, board, wall[i], TacticTask.Wall, TeamShape.Inside(spot), dt, sprint: false);
        }
    }

    /// <summary>Gets whether positioning or defence has this skater on the carrier.</summary>
    static bool OnCarrier(World world, Player p)
    {
        return Positioning.JobOf(world, p) == Job.Pressure || DefenceSystem.DutyOf(world, p) is Duty.Contain or Duty.Engage;
    }

    /// <summary>Takes a skater over from positioning and defence and skates him to his tactical spot, read by his hockey IQ.</summary>
    static void Take(World world, TacticsBoard board, Player p, TacticTask task, Vector2 spot, float dt, bool sprint)
    {
        board.Tasks[p.Id] = task;
        float iq = p.Data.Traits.Iq;
        var read = board.Reads.TryGetValue(p.Id, out var r) && world.Tick - r.Tick <= 2 ? r.At : Flat(p.Position);
        read += (spot - read) * Math.Min(1f, (2f + 6f * iq) * dt);
        board.Reads[p.Id] = (read, world.Tick);
        var target = sprint ? spot : read;
        float far = Vector2.Distance(target, Flat(p.Position));
        var move = sprint ? Steering.Toward(p, target, 0.8f, sprint: true) : Steering.Toward(p, target, 1.6f, sprint: far > 4f);
        AiCommand.Take(world, p, AiLayer.Tactics, target, move, Reason(task));
    }
}
