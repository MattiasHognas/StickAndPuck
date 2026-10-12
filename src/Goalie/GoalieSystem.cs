using System.Numerics;
using StickAndPuck.Core;

namespace StickAndPuck.Goalie;

/// <summary>
/// W5: goalies hold the crease, react to shots from goalie reflexes, and catch or leave rebounds.
/// Runs before puck physics: physics lets the puck pass through goalies, so this system decides
/// every save. A goalie squares up to the puck and cuts the angle (<see cref="GoalieModel.Spot"/>);
/// when a shot comes it reacts after its reaction time and pushes or drops toward where the puck
/// will cross. A puck that meets a covered part of the goalie is stopped, except a share that
/// squeezes through, more for slow reflexes and hard shots. A stopped puck is caught (the goalie
/// carries it: <see cref="Puck.Carrier"/> is the goalie, which rules read as a freeze to whistle)
/// or left as a rebound. Every stop publishes <see cref="MatchEventKind.Save"/> with the goalie's
/// side and id and the shot speed as Strength. With no whistle, a held puck is played to the corner.
/// </summary>
internal sealed class GoalieSystem : ISystem, IDisposable
{
    readonly Dictionary<int, GoalieState> states = [];
    readonly GoalieRenderer renderer = new();

    /// <summary>Gets or sets the dice for squeezes and rebounds; tests seed it.</summary>
    public Random Dice { get; set; } = new(5);

    public void Start(World world)
    {
        renderer.Load();
    }

    public void Update(World world, float dt)
    {
        GoalieDemo.Update(world);
        foreach (var g in world.Players)
        {
            if (!g.OnIce || !g.IsGoalie) continue;
            if (!states.TryGetValue(g.Id, out var state)) states[g.Id] = state = new GoalieState();
            Step(world, g, state, dt);
            renderer.Animate(g, state, dt, Rules.Celebration.Slumps(world, g));
        }
    }

    void Step(World world, Player g, GoalieState state, float dt)
    {
        int end = GoalieModel.End(world, g);
        var pk = world.Puck;
        state.Rise(world.Time);
        if (pk.Carrier == g.Id)
        {
            Hold(world, g, state, end);
            Move(g, GoalieModel.Spot(end, pk.Position with { X = -end * 10 }), GoalieTuning.ShuffleSpeed, dt);
            return;
        }
        bool watching = pk.Carrier < 0 && pk.Live && world.Time >= state.IgnoreUntil;
        if (watching) Track(world, g, state, end);
        else state.SeenAt = float.NaN;

        var spot = GoalieModel.Spot(end, pk.Position);
        if (state.Reacting) Move(g, spot with { Z = state.ReactZ }, GoalieModel.SaveSpeed(g.Data.Traits), dt);
        else Move(g, spot, GoalieTuning.ShuffleSpeed, dt);
        Face(g, end, pk.Position);

        if (watching) Contact(world, g, state, end, dt);
    }

    /// <summary>Watches for a shot; after the reaction time, aims the push at where it will cross the goalie's plane.</summary>
    static void Track(World world, Player g, GoalieState state, int end)
    {
        var pk = world.Puck;
        if (!GoalieModel.IsShot(end, pk.Position, pk.Velocity))
        {
            state.SeenAt = float.NaN;
            state.ShotFrom = float.NaN;
            if (!state.Down) state.ReactZ = float.NaN;
            return;
        }
        if (float.IsNaN(state.SeenAt))
        {
            state.SeenAt = world.Time;
            state.ShotFrom = new Vector2(pk.Position.X - end * RinkSpec.GoalLineX, pk.Position.Z).Length();
        }
        if (world.Time - state.SeenAt < GoalieModel.ReactionTime(g.Data.Traits)) return;
        var at = GoalieModel.Cross(pk.Position, pk.Velocity, g.Position.X);
        if (float.IsInfinity(at.Offset)) return;
        float limit = RinkSpec.NetWidth / 2;
        state.ReactZ = Math.Clamp(at.Offset, -limit, limit);
        if (at.Height < GoalieTuning.LowShot) state.Drop(world.Time);
    }

    /// <summary>Checks the puck's path this step against the goalie's plane, or smothers a slow puck at its feet.</summary>
    void Contact(World world, Player g, GoalieState state, int end, float dt)
    {
        var pk = world.Puck;
        float from = pk.Position.X * end, to = (pk.Position.X + pk.Velocity.X * dt) * end, plane = g.Position.X * end;
        if (from < plane && to >= plane)
        {
            var at = GoalieModel.Cross(pk.Position, pk.Velocity, g.Position.X);
            float cover = GoalieModel.Coverage(at.Offset - g.Position.Z, at.Height, state.Down) / GoalieModel.CloseRead(state.ShotFrom);
            if (cover <= 1f && Dice.NextSingle() < CleanChance(g, pk.Velocity.Length(), cover))
                Save(world, g, state, end, new Vector3(g.Position.X, at.Height, at.Offset));
            return;
        }
        var flat = new Vector2(pk.Position.X - g.Position.X, pk.Position.Z - g.Position.Z);
        if (pk.Velocity.Length() < GoalieTuning.SmotherSpeed && flat.Length() < GoalieTuning.SmotherReach && pk.Position.Y < GoalieTuning.LowShot)
        {
            state.Drop(world.Time);
            Catch(world, g, state, pk.Velocity.Length());
        }
    }

    /// <summary>The chance a covered shot is stopped: reflexes, the shot's speed, and how near the edge of the reach it is.</summary>
    static float CleanChance(Player g, float speed, float cover)
    {
        float chance = GoalieModel.ByReflexes(g.Data.Traits, GoalieTuning.CleanSlow, GoalieTuning.CleanFast);
        chance -= GoalieTuning.HardShotPenalty * Math.Clamp((speed - GoalieTuning.HardShot) / (45f - GoalieTuning.HardShot), 0f, 1f);
        if (cover > 0.75f) chance -= (cover - 0.75f) * 0.6f;
        return chance;
    }

    void Save(World world, Player g, GoalieState state, int end, Vector3 at)
    {
        var pk = world.Puck;
        float speed = pk.Velocity.Length();
        var t = g.Data.Traits;
        float catchChance = GoalieModel.ByReflexes(t, GoalieTuning.CatchSlow, GoalieTuning.CatchFast);
        if (at.Y < GoalieTuning.LowShot) catchChance *= 0.5f;                       // pads kick out rebounds
        if (speed > GoalieTuning.HardShot) catchChance *= 0.6f;
        if (speed < 12f) catchChance = MathF.Max(catchChance, 0.8f);                // soft shots are easy to hold
        pk.Position = at;
        if (Dice.NextSingle() < catchChance)
        {
            Catch(world, g, state, speed);
            return;
        }

        // rebound: back out, slower, off to one side; slow reflexes leave it in the slot, fast ones steer it wide
        float keep = GoalieModel.ByReflexes(t, GoalieTuning.ReboundSlow, GoalieTuning.ReboundFast);
        float wide = GoalieModel.ByReflexes(t, 0.1f, 0.75f);
        float angle = (wide + Dice.NextSingle() * (1.2f - wide)) * (Dice.Next(2) == 0 ? -1 : 1);
        var dir = new Vector3(-end * MathF.Cos(angle), 0, MathF.Sin(angle));
        pk.Position = at with { X = at.X - end * 0.12f };
        pk.Velocity = dir * speed * keep + new Vector3(0, MathF.Min(pk.Velocity.Y, 0) * 0.2f, 0);
        pk.LastTouch = g.Id;
        state.IgnoreUntil = world.Time + GoalieTuning.IgnoreAfterSave;
        state.SeenAt = float.NaN;
        world.Events.Publish(new MatchEvent(MatchEventKind.Save, world.Time, g.Side, g.Id, at, speed));
        GoalieDemo.Saved(caught: false);
    }

    static void Catch(World world, Player g, GoalieState state, float speed)
    {
        var pk = world.Puck;
        pk.Carrier = g.Id;
        pk.LastTouch = g.Id;
        pk.Velocity = Vector3.Zero;
        state.HeldSince = world.Time;
        state.SeenAt = float.NaN;
        world.Events.Publish(new MatchEvent(MatchEventKind.Save, world.Time, g.Side, g.Id, pk.Position, speed));
        GoalieDemo.Saved(caught: true);
    }

    /// <summary>Keeps a held puck in the glove; with no whistle after <see cref="GoalieTuning.HoldSeconds"/>, plays it to the nearer corner.</summary>
    static void Hold(World world, Player g, GoalieState state, int end)
    {
        var pk = world.Puck;
        pk.Position = g.Position + new Vector3(-end * 0.3f, 0, 0);
        pk.Velocity = Vector3.Zero;
        if (!pk.Live || world.Match.Phase != MatchPhase.Play || world.Time - state.HeldSince < GoalieTuning.HoldSeconds) return;
        float side = g.Position.Z < 0 ? -1 : 1;
        var dir = Vector3.Normalize(new Vector3(end * 0.35f, 0, side));
        pk.Carrier = -1;
        pk.Position = g.Position + new Vector3(0, 0, side * 0.5f);
        pk.Velocity = dir * GoalieTuning.PlayOutSpeed;
        state.IgnoreUntil = world.Time + GoalieTuning.IgnoreAfterSave * 2;
    }

    static void Move(Player g, Vector3 target, float speed, float dt)
    {
        var delta = target - g.Position;
        delta.Y = 0;
        float distance = delta.Length();
        float step = MathF.Min(distance, speed * dt);
        var velocity = distance > 1e-4f ? delta / distance * (step / dt) : Vector3.Zero;
        g.Velocity = velocity;
        g.Position += velocity * dt;
    }

    /// <summary>Turns to the puck, but never more than 70 degrees off square to the ice.</summary>
    static void Face(Player g, int end, Vector3 puck)
    {
        float square = -end * MathF.PI / 2;
        var d = puck - g.Position;
        float want = d.LengthSquared() < 0.01f ? square : MathF.Atan2(d.X, d.Z);
        float off = MathF.IEEERemainder(want - square, 2 * MathF.PI);
        g.Heading = square + Math.Clamp(off, -1.22f, 1.22f);
    }

    public void DrawScene(World world, RenderContext render)
    {
        renderer.DrawScene(world, render);
    }

    public void DrawMask(World world, RenderContext render)
    {
        renderer.DrawMask(world, render);
    }

    public void DrawHud(World world, RenderContext render)
    {
        GoalieDemo.DrawHud(world);
    }

    public void Dispose()
    {
        renderer.Dispose();
    }
}

/// <summary>What one goalie is doing between steps.</summary>
internal sealed class GoalieState
{
    /// <summary>Gets or sets when the current shot was first seen, or NaN.</summary>
    public float SeenAt { get; set; } = float.NaN;

    /// <summary>Gets or sets how far from the net the current shot was first seen, or NaN.</summary>
    public float ShotFrom { get; set; } = float.NaN;

    /// <summary>Gets or sets the z the goalie pushes to for the current shot, or NaN.</summary>
    public float ReactZ { get; set; } = float.NaN;

    /// <summary>Gets a value indicating whether the goalie is pushing toward a shot.</summary>
    public bool Reacting => !float.IsNaN(ReactZ);

    /// <summary>Gets or sets when the goalie dropped into the butterfly, or NaN when standing.</summary>
    public float DownSince { get; set; } = float.NaN;

    /// <summary>Gets a value indicating whether the goalie is down in the butterfly.</summary>
    public bool Down { get; private set; }

    /// <summary>Gets or sets when the goalie caught the puck it holds.</summary>
    public float HeldSince { get; set; }

    /// <summary>Gets or sets the time before which the goalie ignores the puck.</summary>
    public float IgnoreUntil { get; set; }

    /// <summary>Drops into the butterfly, or stays down longer.</summary>
    /// <param name="now">The world time.</param>
    public void Drop(float now)
    {
        if (!Down) DownSince = now;
        Down = true;
        Until = now + GoalieTuning.ButterflySeconds;
    }

    /// <summary>Gets when the goalie gets back up.</summary>
    public float Until { get; private set; }

    /// <summary>Stands back up once the butterfly time is over.</summary>
    /// <param name="now">The world time.</param>
    public void Rise(float now)
    {
        if (Down && now >= Until)
        {
            Down = false;
            DownSince = float.NaN;
            ReactZ = float.NaN;
        }
    }
}
