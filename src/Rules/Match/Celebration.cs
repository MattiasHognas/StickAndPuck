using System.Numerics;
using StickAndPuck.Camera;
using StickAndPuck.Core;
using StickAndPuck.Lines;

namespace StickAndPuck.Rules;

/// <summary>
/// W40: the end-of-game celebration. At the final horn of a decided game (regulation, overtime or
/// the shootout) the winners swarm their goalie and celebrate, the losers drop to the ice where
/// they stand or trudge off to their bench and bury their heads, both goalies included, and the
/// camera holds on the pile. The match stays in <see cref="MatchPhase.Stopped"/> for
/// <see cref="MatchTuning.CelebrationSeconds"/> and only then becomes <see cref="MatchPhase.Final"/>,
/// so the result summary follows the celebration as it followed the horn before. The players are
/// posed from here after the movement systems ran, so input and the AI cannot move them. A tie has
/// no celebration. The skater renderer asks <see cref="Pose"/> for the clip to show.
/// </summary>
public static class Celebration
{
    const float PileRadius = 1.3f;
    const float SkateSpeed = 7f;
    const float TrudgeSpeed = 3.2f;
    const float DropSpeed = 5f;
    const float BrakeSeconds = 0.5f;
    const float PileX = 10f;

    /// <summary>Gets or sets a value indicating whether the celebration plays at all (tests turn it off).</summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>Gets a value indicating whether a celebration is on or has played in this match, so the final poses hold until the summary.</summary>
    /// <param name="world">The match state.</param>
    /// <returns>True from the horn of a decided game.</returns>
    public static bool Playing(World world)
    {
        return MatchFlow.State(world).Celebration != null;
    }

    /// <summary>Gets the winners of the celebration under way.</summary>
    /// <param name="world">The match state.</param>
    /// <returns>The winning club, or null with no celebration.</returns>
    public static Side? Winner(World world)
    {
        return MatchFlow.State(world).Celebration?.Won;
    }

    /// <summary>
    /// Gets the clip a skater shows in the celebration: "celebrate" for a winner at the pile, "fall"
    /// for a loser who dropped to the ice, "block" (head in the hands) for one who reached the bench,
    /// with the seconds since it started and whether it loops. Null while the skater is still skating
    /// or standing as usual, and for a skater outside any celebration.
    /// </summary>
    /// <param name="world">The match state.</param>
    /// <param name="p">The skater.</param>
    /// <returns>The pose.</returns>
    public static (string Clip, float Seconds, bool Loop)? Pose(World world, Player p)
    {
        if (MatchFlow.State(world).Celebration is not { } c || !c.Parts.TryGetValue(p.Id, out var part) || float.IsNaN(part.ClipSince)) return null;
        float since = Math.Max(0, c.Elapsed - part.ClipSince);
        return part.Mood switch
        {
            Mood.Winner => ("celebrate", since, true),
            Mood.Drop => ("fall", since, false),
            Mood.SkateOff => ("block", since, false),
            _ => null,
        };
    }

    /// <summary>Gets a value indicating whether a goalie has slumped in the celebration: the losing goalie drops into the butterfly and stays down.</summary>
    /// <param name="world">The match state.</param>
    /// <param name="goalie">The goalie.</param>
    /// <returns>True for the losing goalie once the celebration is on.</returns>
    public static bool Slumps(World world, Player goalie)
    {
        return MatchFlow.State(world).Celebration is { } c && goalie.IsGoalie && goalie.Side != c.Won && c.Elapsed >= BrakeSeconds;
    }

    /// <summary>Starts the celebration at the horn if the game was decided and both clubs have players on the ice.</summary>
    /// <returns>True when the celebration runs, so the caller holds the final.</returns>
    internal static bool Begin(World world, FlowState s, Side? winner)
    {
        if (!Enabled || winner is not { } win || s.Celebration != null) return false;
        Pour(world, Side.Home);
        Pour(world, Side.Away);
        var skaters = world.Players.Where(p => p.OnIce && !p.IsGoalie).ToList();
        if (!skaters.Any(p => p.Side == win) || !skaters.Any(p => p.Side != win)) return false;

        var c = new CelebrationState(win, MatchTuning.CelebrationSeconds);
        var m = world.Match;
        int end = FaceoffFormation.DefendedEnd(m, win);
        // the pile sits inside the winners' own blue line, so the camera holds the pile, the losers and their bench in one view
        c.Pile = new Vector3(end * PileX, 0, 0.4f);
        c.Anchor = new Vector3(end * PileX * 0.45f, 0, 0);

        // the winners ring the pile, their goalie on the net side of it
        var winners = world.OnIce(win).Where(p => !p.IsGoalie).OrderBy(p => Vector3.Distance(p.Position, c.Pile)).ToList();
        int slots = winners.Count + 1;
        for (int i = 0; i < winners.Count; i++)
        {
            float a = MathF.Atan2(0, end) + (i + 1) * MathF.PI * 2 / slots;
            c.Parts[winners[i].Id] = new Part(Mood.Winner, c.Pile + new Vector3(MathF.Cos(a), 0, MathF.Sin(a)) * PileRadius);
        }
        foreach (var g in world.OnIce(win).Where(p => p.IsGoalie))
            c.Parts[g.Id] = new Part(Mood.Goalie, c.Pile + new Vector3(end * PileRadius, 0, 0));

        // the losers nearest the pile drop where they stand, the rest trudge to the bench; their goalie slumps in the crease
        var losers = world.Players.Where(p => p.OnIce && !p.IsGoalie && p.Side != win).OrderBy(p => Vector3.Distance(p.Position, c.Pile)).ToList();
        for (int i = 0; i < losers.Count; i++)
        {
            var p = losers[i];
            if (i < 2)
            {
                // a few metres from the pile, between it and centre ice: down on the knees, then flat on the ice
                c.Parts[p.Id] = new Part(Mood.Drop, new Vector3(c.Anchor.X - end * 1.5f, 0, i == 0 ? 3.2f : -2.6f));
            }
            else
            {
                var door = Squad.Door(p.Side);
                float spread = (i - losers.Count / 2f) * 1.1f;
                c.Parts[p.Id] = new Part(Mood.SkateOff, new Vector3(door.X + spread, 0, door.Y + 1.2f));
            }
        }
        foreach (var g in world.Players.Where(p => p.OnIce && p.IsGoalie && p.Side != win))
            c.Parts[g.Id] = new Part(Mood.Goalie, g.Position);

        foreach (var p in world.Players)
        {
            p.Command = default;
            if (p.OnIce && !p.IsGoalie) p.Begin(p.Side == win ? PlayerAction.Celebrate : PlayerAction.None);
        }
        s.Celebration = c;
        s.Dot = RinkSpec.CentreDot;     // the referee heads to centre ice and stays out of the way
        return true;
    }

    /// <summary>After a shootout the skaters are on the bench (the last shooter aside): the starters pour back over the boards at their door.</summary>
    static void Pour(World world, Side side)
    {
        if (world.Match.Phase != MatchPhase.Shootout) return;
        var door = Squad.Door(side);
        int on = world.OnIce(side).Count(p => !p.IsGoalie);
        var bench = world.Players.Where(p => p.Side == side && !p.IsGoalie && !p.OnIce).Take(Math.Max(0, 5 - on)).ToList();
        for (int i = 0; i < bench.Count; i++)
        {
            var p = bench[i];
            p.OnIce = true;
            p.Slot = -1;
            p.Position = new Vector3(door.X + (i - 2) * 1.1f, 0, door.Y + 0.6f);
            p.Velocity = Vector3.Zero;
            p.Heading = 0;
        }
    }

    /// <summary>One step of the celebration, after the movement systems ran: poses every player and ends in <see cref="MatchPhase.Final"/>.</summary>
    /// <returns>True while a celebration owns the players, so the match flow does nothing else.</returns>
    internal static bool Update(World world, FlowState s, float dt)
    {
        if (s.Celebration is not { } c) return false;
        var m = world.Match;
        // W9's goal replay (a sudden-death winner) plays out first; the live state comes back when it ends
        if (Replay.Playing) return true;

        var pk = world.Puck;
        pk.Live = false;
        pk.Carrier = -1;
        pk.Position = c.Anchor;
        pk.Velocity = Vector3.Zero;

        c.Elapsed += dt;
        foreach (var p in world.Players)
        {
            if (!p.OnIce || !c.Parts.TryGetValue(p.Id, out var part)) continue;
            p.Command = default;
            if (part.Held is { } held) p.Position = held;
            Step(c, p, part, dt);
            part.Held = p.Position;
        }

        if (!c.Done && c.Elapsed >= c.Seconds)
        {
            c.Done = true;
            m.Phase = MatchPhase.Final;
        }
        return true;
    }

    static void Step(CelebrationState c, Player p, Part part, float dt)
    {
        switch (part.Mood)
        {
            case Mood.Winner:
                if (Move(p, part.Target, SkateSpeed, dt))
                {
                    Face(p, c.Pile);
                    part.StartClip(c);
                }
                else if (Vector3.Distance(p.Position, c.Pile) < 5f)
                {
                    part.StartClip(c);     // the last metres are a glide with the stick up
                }
                break;
            case Mood.Goalie:
                if (p.Side == c.Won)
                {
                    if (Move(p, part.Target, GoalieSpeed, dt)) Face(p, c.Pile);
                }
                else
                {
                    Brake(p, dt);
                }
                break;
            case Mood.Drop:
                if (float.IsNaN(part.ClipSince) && !Move(p, part.Target, DropSpeed, dt)) break;
                Brake(p, dt);
                p.Heading = MathF.Atan2(c.Pile.X - p.Position.X, 0);   // the back to the winners, slumped toward the far end
                p.Heading += MathF.PI;
                part.StartClip(c);
                break;
            case Mood.SkateOff:
                if (Move(p, part.Target, TrudgeSpeed, dt))
                {
                    p.Heading = MathF.PI;     // back to the ice, face in the bench
                    part.StartClip(c);
                }
                break;
        }
    }

    const float GoalieSpeed = 5f;

    /// <summary>Skates a player one step toward a spot, slowing on arrival.</summary>
    /// <returns>True once on the spot.</returns>
    static bool Move(Player p, Vector3 target, float speed, float dt)
    {
        var to = target - p.Position;
        to.Y = 0;
        float d = to.Length();
        if (d < 0.08f)
        {
            p.Position = target;
            p.Velocity = Vector3.Zero;
            return true;
        }
        var dir = to / d;
        float v = Math.Min(speed, 0.6f + d * 2.5f);
        float step = Math.Min(v * dt, d);
        p.Position += dir * step;
        p.Velocity = dir * v;
        p.Heading = MathF.Atan2(dir.X, dir.Z);
        return false;
    }

    static void Brake(Player p, float dt)
    {
        var v = p.Velocity;
        v.Y = 0;
        float s = v.Length();
        if (s < 0.05f)
        {
            p.Velocity = Vector3.Zero;
            return;
        }
        float ns = Math.Max(0, s - 8f * dt);
        p.Position += v / s * (s + ns) / 2 * dt;
        p.Velocity = v / s * ns;
    }

    static void Face(Player p, Vector3 at)
    {
        p.Heading = MathF.Atan2(at.X - p.Position.X, at.Z - p.Position.Z);
    }

    internal enum Mood
    {
        Winner,
        Goalie,
        Drop,
        SkateOff,
    }

    internal sealed class Part(Mood mood, Vector3 target)
    {
        public Mood Mood { get; } = mood;

        public Vector3 Target { get; } = target;

        /// <summary>Gets or sets where the celebration left the player last step; the movement and goalie systems run first and would drag him away.</summary>
        public Vector3? Held { get; set; }

        /// <summary>Gets the celebration time the pose clip started, NaN until it has.</summary>
        public float ClipSince { get; private set; } = float.NaN;

        public void StartClip(CelebrationState c)
        {
            if (float.IsNaN(ClipSince)) ClipSince = c.Elapsed;
        }
    }

    internal sealed class CelebrationState(Side winner, float seconds)
    {
        public Side Won { get; } = winner;

        public float Seconds { get; } = seconds;

        public float Elapsed { get; set; }

        public bool Done { get; set; }

        public Vector3 Pile { get; set; }

        public Vector3 Anchor { get; set; }

        internal Dictionary<int, Part> Parts { get; } = [];
    }
}
