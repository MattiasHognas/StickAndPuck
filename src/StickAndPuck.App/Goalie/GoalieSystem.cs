using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Goalie;

/// <summary>
/// W5: goalies hold the crease, react to shots from goalie reflexes, and catch or leave rebounds.
/// Runs before puck physics: physics lets the puck pass through goalies, so this system decides
/// every save. A goalie squares up to the puck and cuts the angle (<see cref="GoalieModel.Spot(int, Vector3, int)"/>);
/// when a shot comes it reacts after its reaction time and pushes or drops toward where the puck
/// will cross. A puck that meets a covered part of the goalie is stopped, except a share that
/// squeezes through, more for slow reflexes and hard shots. A stopped puck is caught (the goalie
/// carries it: <see cref="Puck.Carrier"/> is the goalie, which rules read as a freeze to whistle)
/// or left as a rebound. Every stop publishes <see cref="ShotSaved"/> with the goalie's
/// side and id and the shot speed as Strength. With no whistle, a held puck is played out.
/// W34 goalie traits: positioning (the goalie's hockey IQ) sets how far out and how square the
/// goalie stands and how fast it shuffles; glove and blocker set the reach and the clean-stop chance
/// on each side of the body, so a weak hand is a weak side; rebound control sets how often a shot
/// is held and how hard and how central the rebounds are; puck handling sets whether a held puck goes
/// to a teammate or the corner, how true that pass is, and whether the goalie leaves the crease to
/// stop a loose puck behind the net and move it on.
/// </summary>
internal sealed class GoalieSystem : ISystem, IDisposable
{
    readonly Dictionary<int, GoalieState> states = [];
    readonly GoalieRenderer renderer = new();

    /// <summary>The dice seed without a match seed (W67).</summary>
    internal const int Fallback = 5;

    /// <summary>Gets or sets the dice for squeezes and rebounds; tests seed it.</summary>
    public Random Dice { get; set; } = Seeds.Dice(0, "goalie", Fallback);

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
            renderer.Animate(g, state, world.Time, dt, Rules.Celebration.Slumps(world, g));
        }
    }

    void Step(World world, Player g, GoalieState state, float dt)
    {
        int end = GoalieModel.End(world, g);
        var pk = world.Puck;
        state.Rise(world.Time);
        int positioning = g.Data.Traits.HockeyIq;
        float shuffle = GoalieTuning.ShuffleSpeed * Traits.Scale(positioning, GoalieTuning.ShuffleWorst, GoalieTuning.ShuffleBest);
        if (pk.Carrier == g.Id)
        {
            state.Playing = false;
            Hold(world, g, state, end);
            Move(g, GoalieModel.Spot(end, pk.Position with { X = -end * 10 }, positioning), shuffle, dt);
            return;
        }
        if (PlayPuck(world, g, state, end, dt)) return;
        bool watching = pk.Carrier < 0 && pk.Live && world.Time >= state.IgnoreUntil;
        if (watching) Track(world, g, state, end);
        else state.SeenAt = float.NaN;

        var spot = GoalieModel.Spot(end, pk.Position, positioning);
        if (state.Reacting) Move(g, spot with { Z = state.ReactZ }, GoalieModel.SaveSpeed(g.Data.Traits), dt);
        else Move(g, spot, state.OutOfCrease ? GoalieTuning.PlaySkate : shuffle, dt);
        if (state.OutOfCrease && Vector3.Distance(g.Position, spot) < 0.2f) state.OutOfCrease = false;
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
            float offset = at.Offset - g.Position.Z;
            int hand = GoalieModel.HandTrait(g.Data, end, offset < 0 ? -1 : 1);
            bool hands = GoalieModel.HandHeight(at.Height);
            float cover = GoalieModel.Coverage(offset, at.Height, state.Down, GoalieModel.HandScale(hand))
                / GoalieModel.CloseRead(state.ShotFrom, g.Data.Traits.HockeyIq);
            if (cover <= 1f && Dice.NextSingle() < CleanChance(g, pk.Velocity.Length(), cover, hands ? hand : 50))
            {
                if (hands) state.Pose(world.Time, offset < 0 ? -1 : 1, GoalieModel.GloveSide(g.Data, end));
                Save(world, g, state, end, new Vector3(g.Position.X, at.Height, at.Offset));
            }
            return;
        }
        var flat = new Vector2(pk.Position.X - g.Position.X, pk.Position.Z - g.Position.Z);
        if (pk.Velocity.Length() < GoalieTuning.SmotherSpeed && flat.Length() < GoalieTuning.SmotherReach && pk.Position.Y < GoalieTuning.LowShot)
        {
            state.Drop(world.Time);
            Catch(world, g, state, pk.Velocity.Length());
        }
    }

    /// <summary>The chance a covered shot is stopped: reflexes, the hand on that side, the shot's speed, and how near the edge of the reach it is.</summary>
    static float CleanChance(Player g, float speed, float cover, int hand)
    {
        float chance = GoalieModel.ByReflexes(g.Data.Traits, GoalieTuning.CleanSlow, GoalieTuning.CleanFast);
        chance += Traits.Scale(hand, -GoalieTuning.HandClean, GoalieTuning.HandClean);
        chance -= GoalieTuning.HardShotPenalty * Math.Clamp((speed - GoalieTuning.HardShot) / (45f - GoalieTuning.HardShot), 0f, 1f);
        if (cover > 0.75f) chance -= (cover - 0.75f) * 0.6f;
        return chance;
    }

    void Save(World world, Player g, GoalieState state, int end, Vector3 at)
    {
        var pk = world.Puck;
        float speed = pk.Velocity.Length();
        var t = g.Data.Traits;
        int control = t.GoalieReboundControl;
        float catchChance = Traits.Scale(control, GoalieTuning.CatchSlow, GoalieTuning.CatchFast);
        if (at.Y < GoalieTuning.LowShot) catchChance *= 0.5f;                       // pads kick out rebounds
        if (speed > GoalieTuning.HardShot) catchChance *= 0.6f;
        if (speed < 12f) catchChance = MathF.Max(catchChance, 0.8f);                // soft shots are easy to hold
        pk.Position = at;
        if (Dice.NextSingle() < catchChance)
        {
            Catch(world, g, state, speed);
            return;
        }

        // rebound: back out, slower, off to one side; poor rebound control leaves it hard and in the
        // slot, good control kills it and steers it wide to the corners
        float keep = Traits.Scale(control, GoalieTuning.ReboundSlow, GoalieTuning.ReboundFast);
        float wide = Traits.Scale(control, 0.1f, 0.75f);
        float angle = (wide + Dice.NextSingle() * (1.2f - wide)) * (Dice.Next(2) == 0 ? -1 : 1);
        var dir = new Vector3(-end * SimMath.Cos(angle), 0, SimMath.Sin(angle));
        pk.Position = at with { X = at.X - end * 0.12f };
        pk.Velocity = dir * speed * keep + new Vector3(0, MathF.Min(pk.Velocity.Y, 0) * 0.2f, 0);
        pk.LastTouch = g.Id;
        state.IgnoreUntil = world.Time + GoalieTuning.IgnoreAfterSave;
        state.SeenAt = float.NaN;
        world.Events.Publish(new ShotSaved(world.Time, g.Side, g.Id, at, speed));
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
        world.Events.Publish(new ShotSaved(world.Time, g.Side, g.Id, pk.Position, speed));
        GoalieDemo.Saved(caught: true);
    }

    /// <summary>
    /// Keeps a held puck in the glove; with no whistle after <see cref="GoalieTuning.HoldSeconds"/>,
    /// plays it out: a good puck handler passes to an open teammate, anyone else rims it to the nearer
    /// corner. Puck handling also sets how hard and how true the puck goes.
    /// </summary>
    void Hold(World world, Player g, GoalieState state, int end)
    {
        var pk = world.Puck;
        pk.Position = g.Position + new Vector3(-end * 0.3f, 0, 0);
        pk.Velocity = Vector3.Zero;
        if (!pk.Live || world.Match.Phase != MatchPhase.Play || world.Time - state.HeldSince < GoalieTuning.HoldSeconds) return;
        float side = g.Position.Z < 0 ? -1 : 1;
        int handling = g.Data.Traits.GoaliePuckHandling;
        var mate = Dice.NextSingle() < Traits.Scale(handling, GoalieTuning.PassOutPoor, GoalieTuning.PassOutGood) ? OpenMate(world, g, end) : null;
        var start = g.Position + new Vector3(0, 0, side * 0.5f);
        var dir = mate != null ? Flat(mate.Position - start) : Vector3.Normalize(new Vector3(end * 0.35f, 0, side));
        pk.Carrier = -1;
        Release(world, g, state, start, dir, handling, pass: mate != null);
        state.PassedOut = mate != null;
    }

    /// <summary>
    /// Leaves the crease for a loose, slowish puck behind the net when the goalie gets there first,
    /// as often as its puck handling says (rolled once per loose puck), then moves it on to an open
    /// teammate, or up the boards with none. Gives up when an opponent closes in or it takes too long.
    /// </summary>
    /// <returns>True while the goalie is out playing the puck this step.</returns>
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
        var dir = mate != null ? Flat(mate.Position - pk.Position) : Vector3.Normalize(new Vector3(-end * 0.6f, 0, pk.Position.Z < 0 ? -1 : 1));
        Release(world, g, state, Flat(pk.Position), dir, g.Data.Traits.GoaliePuckHandling, pass: mate != null);
        state.Playing = false;
        state.PassedOut = mate != null;
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

    /// <summary>The teammate a goalie passes to: the nearest skater in front of the goal line with no opponent near the lane, or null.</summary>
    static Player? OpenMate(World world, Player g, int end)
    {
        var pk = world.Puck;
        Player? best = null;
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

    static Vector3 Flat(Vector3 v)
    {
        return v with { Y = 0 };
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
        float want = d.LengthSquared() < 0.01f ? square : SimMath.Atan2(d.X, d.Z);
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

    /// <summary>Gets or sets a value indicating whether the goalie is out of the crease going for a loose puck.</summary>
    public bool Playing { get; set; }

    /// <summary>Gets or sets when the goalie went for the loose puck.</summary>
    public float PlaySince { get; set; }

    /// <summary>Gets or sets a value indicating whether the goalie has already decided about the current loose puck.</summary>
    public bool Rolled { get; set; }

    /// <summary>Gets or sets a value indicating whether the goalie is away from its spot and skating back.</summary>
    public bool OutOfCrease { get; set; }

    /// <summary>Gets or sets a value indicating whether the last puck the goalie played out went to a teammate.</summary>
    public bool PassedOut { get; set; }

    /// <summary>Gets the glove (+1) or blocker (-1) save pose being shown, or 0.</summary>
    public int HandPose { get; private set; }

    /// <summary>Gets when the hand save pose ends.</summary>
    public float HandPoseUntil { get; private set; }

    /// <summary>Shows a glove or blocker save for a moment.</summary>
    /// <param name="now">The world time.</param>
    /// <param name="side">The z side the puck met the goalie on.</param>
    /// <param name="glove">The z side of the glove.</param>
    public void Pose(float now, int side, int glove)
    {
        HandPose = side == glove ? 1 : -1;
        HandPoseUntil = now + GoalieTuning.HandPoseSeconds;
    }

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
