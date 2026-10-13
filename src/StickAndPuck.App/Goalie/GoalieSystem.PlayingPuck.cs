using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Goalie;

internal sealed partial class GoalieSystem
{
    /// <summary>Keeps a held puck in the glove; with no whistle after <see cref="GoalieTuning.HoldSeconds"/>,
    /// plays it out: a good puck handler passes to an open teammate, anyone else rims it to the nearer
    /// corner. Puck handling also sets how hard and how true the puck goes.</summary>
    void Hold(World world, Player g, GoalieState state, int end)
    {
        var pk = world.Puck;
        pk.Position = g.Position + new Vector3(-end * 0.3f, 0, 0);
        pk.Velocity = Vector3.Zero;
        if (!pk.Live || world.Match.Phase != MatchPhase.Play || world.Time - state.HeldSince < GoalieTuning.HoldSeconds) return;
        float side = g.Position.Z < 0 ? -1 : 1;
        int handling = g.Data.Traits.GoaliePuckHandling;
        var mate = Dice.NextSingle() < Traits.Scale(handling, GoalieTuning.PassOutPoor, GoalieTuning.PassOutGood) ? OpenMate(world, g, end) : Maybe<Player>.None;
        var start = g.Position + new Vector3(0, 0, side * 0.5f);
        var dir = mate.TryGet(out var to) ? Flat(to.Position - start) : Vector3.Normalize(new Vector3(end * 0.35f, 0, side));
        pk.Carrier = -1;
        Release(world, g, state, start, dir, handling, pass: mate.HasValue);
        state.PassedOut = mate.HasValue;
    }

    /// <summary>Leaves the crease for a loose, slowish puck behind the net when the goalie gets there first,
    /// as often as its puck handling says (rolled once per loose puck), then moves it on to an open
    /// teammate, or up the boards with none. Gives up when an opponent closes in or it takes too long.</summary>
    bool PlayPuck(World world, Player g, GoalieState state, int end, float dt)
    {
        var pk = world.Puck;
        bool behind = pk.Carrier < 0 && pk.Live && world.Match.Phase == MatchPhase.Play
            && pk.Position.X * end > RinkSpec.GoalLineX + 0.2f
            && pk.Position.Y < GoalieTuning.LowShot
            && new Vector2(pk.Position.X - end * RinkSpec.GoalLineX, pk.Position.Z).Length() < GoalieTuning.PlayReach
            && pk.Velocity.Length() < GoalieTuning.PlaySpeed
            && world.Time >= state.IgnoreUntil;
        if (!behind)
        {
            state.Playing = false;
            state.Rolled = false;
            return false;
        }
        float mine = Vector3.Distance(Flat(g.Position), Flat(pk.Position));
        float theirs = world.Players.Where(o => o.OnIce && o.Side != g.Side && !o.IsGoalie)
            .Select(o => Vector3.Distance(Flat(o.Position), Flat(pk.Position))).DefaultIfEmpty(99f).Min();
        if (!state.Playing)
        {
            if (state.Rolled || theirs < mine + 1.5f) return false;
            state.Rolled = true;
            int handling = g.Data.Traits.GoaliePuckHandling;
            float chance = Traits.Scale(handling, GoalieTuning.PlayChancePoor, GoalieTuning.PlayChanceGood);
            if (chance <= 0f || Dice.NextSingle() >= chance) return false;
            state.Playing = true;
            state.PlaySince = world.Time;
        }
        if (theirs < GoalieTuning.PlayPressure || world.Time - state.PlaySince > GoalieTuning.PlaySeconds)
        {
            state.Playing = false;
            state.OutOfCrease = true;
            return false;
        }
        state.OutOfCrease = true;
        state.SeenAt = float.NaN;
        state.ReactZ = float.NaN;
        Move(g, Flat(pk.Position + pk.Velocity * 0.15f), GoalieTuning.PlaySkate, dt);
        Face(g, end, pk.Position);
        if (mine > GoalieTuning.PlayTouch) return true;

        var mate = OpenMate(world, g, end);
        var dir = mate.TryGet(out var to) ? Flat(to.Position - pk.Position) : Vector3.Normalize(new Vector3(-end * 0.6f, 0, pk.Position.Z < 0 ? -1 : 1));
        Release(world, g, state, Flat(pk.Position), dir, g.Data.Traits.GoaliePuckHandling, pass: mate.HasValue);
        state.Playing = false;
        state.PassedOut = mate.HasValue;
        return true;
    }

    /// <summary>Sends the puck off the goalie's stick, off line by up to the handling's error, and publishes a pass.</summary>
    void Release(World world, Player g, GoalieState state, Vector3 start, Vector3 dir, int handling, bool pass)
    {
        var pk = world.Puck;
        float error = Traits.Scale(handling, GoalieTuning.PassErrorPoor, GoalieTuning.PassErrorGood) * (Dice.NextSingle() * 2 - 1);
        float speed = pass ? Traits.Scale(handling, GoalieTuning.PassSpeedPoor, GoalieTuning.PassSpeedGood) : GoalieTuning.PlayOutSpeed;
        dir = Flat(dir);
        dir = dir.LengthSquared() < 1e-6f ? new Vector3(1, 0, 0) : Vector3.Normalize(dir);
        float c = SimMath.Cos(error), s = SimMath.Sin(error);
        dir = new Vector3(dir.X * c - dir.Z * s, 0, dir.X * s + dir.Z * c);
        pk.Position = start with { Y = 0 };
        pk.Velocity = dir * speed;
        pk.LastTouch = g.Id;
        state.IgnoreUntil = world.Time + GoalieTuning.IgnoreAfterSave * 2;
        state.OutOfCrease = true;
        if (pass) world.Events.Publish(new PassMade(world.Time, g.Side, g.Id, start, speed));
    }

    /// <summary>The teammate a goalie passes to: the nearest skater in front of the goal line with no opponent near the lane, or None.</summary>
    static Maybe<Player> OpenMate(World world, Player g, int end)
    {
        var pk = world.Puck;
        var best = Maybe<Player>.None;
        float bestDistance = float.MaxValue;
        foreach (var m in world.Players)
        {
            if (!m.OnIce || m.Side != g.Side || m.IsGoalie || m.Action is PlayerAction.Fallen or PlayerAction.Fight) continue;
            if (m.Position.X * end > RinkSpec.GoalLineX - 1f) continue;
            float distance = Vector3.Distance(Flat(m.Position), Flat(pk.Position));
            if (distance < 3f || distance > 25f || distance >= bestDistance) continue;
            bool open = world.Players.All(o => !o.OnIce || o.Side == g.Side || o.IsGoalie
                || Lane(Flat(pk.Position), Flat(m.Position), Flat(o.Position)) > 1.2f);
            if (!open) continue;
            best = m;
            bestDistance = distance;
        }
        return best;
    }

    /// <summary>The distance from a point to the segment from a to b.</summary>
    static float Lane(Vector3 a, Vector3 b, Vector3 p)
    {
        var ab = b - a;
        float t = Math.Clamp(Vector3.Dot(p - a, ab) / MathF.Max(ab.LengthSquared(), 1e-6f), 0f, 1f);
        return Vector3.Distance(a + ab * t, p);
    }
}
