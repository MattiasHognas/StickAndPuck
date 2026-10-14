using StickAndPuck.App.Core;

namespace StickAndPuck.App.Benches;

/// <summary>The benches' reactions to the match: each step's events (<see cref="BenchCue.For"/>) put
/// reactions on the seated skaters of each bench, every one a moment after the cause and held for a
/// while before he falls back to his base pose; a stronger reaction takes over a weaker one. The coach
/// and the trainer react too, and at a stoppage walk to a player to talk. Render only: it reads the
/// world and the events and never changes them, and its dice are its own, from the benches' seed.</summary>
public sealed class BenchReactions
{
    /// <summary>The longest a figure waits after the cause before he reacts (s).</summary>
    public const float MaxDelay = 0.4f;

    /// <summary>How often a pair on a bench strikes up a chat in play, per second.</summary>
    const float ChatRate = 0.02f;

    /// <summary>How often the coach leans in to a player by himself, per second.</summary>
    const float InstructRate = 0.008f;

    /// <summary>The chance the coach also talks to a player at a stoppage after nothing in particular.</summary>
    const float InstructChance = 0.2f;

    readonly Random dice;
    readonly BenchStaff[] staff = [new(Side.Home, true), new(Side.Home, false), new(Side.Away, true), new(Side.Away, false)];
    readonly bool[] goalAgainst = new bool[2];
    readonly List<BenchFigure> pick = [];

    /// <summary>Initializes a new instance of the <see cref="BenchReactions"/> class.</summary>
    /// <param name="dice">The benches' own dice (<see cref="Seeds.Dice"/> of the match seed's "bench" stream), never a match system's.</param>
    public BenchReactions(Random dice)
    {
        this.dice = dice;
    }

    /// <summary>Gets both clubs' coach and trainer.</summary>
    public IReadOnlyList<BenchStaff> Staff => staff;

    /// <summary>Gets a club's coach or trainer.</summary>
    public BenchStaff StaffOf(Side side, bool coach) => staff[((int)side * 2) + (coach ? 0 : 1)];

    /// <summary>Moves every reaction one step on.</summary>
    /// <param name="world">The world, read only.</param>
    /// <param name="crowd">The bench figures, just updated for this step.</param>
    /// <param name="events">This step's events.</param>
    /// <param name="fightOn">Whether a fight is on: both benches stand to watch it until it ends.</param>
    /// <param name="dt">The step (s).</param>
    public void Update(World world, BenchCrowd crowd, IReadOnlyList<MatchEvent> events, bool fightOn, float dt)
    {
        float now = world.Time;
        foreach (var f in crowd.Shown) Settle(f, now);
        foreach (var e in events)
        {
            foreach (var side in (ReadOnlySpan<Side>)[Side.Home, Side.Away])
            {
                if (e is GoalScored g) goalAgainst[(int)side] = g.Side != side;
                foreach (var cue in BenchCue.For(e, side)) Apply(world, crowd, side, cue);
                if (e is WhistleBlown && (goalAgainst[(int)side] || dice.NextSingle() < InstructChance))
                {
                    goalAgainst[(int)side] = false;
                    Apply(world, crowd, side, new BenchCue(BenchReaction.Instruct, 1, 3f));
                }
            }
        }
        foreach (var side in (ReadOnlySpan<Side>)[Side.Home, Side.Away])
        {
            if (fightOn) Apply(world, crowd, side, new BenchCue(BenchReaction.StandFight, BenchCue.All, float.PositiveInfinity));
            if (dice.NextSingle() < ChatRate * dt) Apply(world, crowd, side, new BenchCue(BenchReaction.Chat, 2, 3f));
            if (dice.NextSingle() < InstructRate * dt) Apply(world, crowd, side, new BenchCue(BenchReaction.Instruct, 1, 3f));
        }
        foreach (var f in crowd.Shown)
        {
            if (!fightOn && f.Reaction == BenchReaction.StandFight && float.IsPositiveInfinity(f.ReactUntil)) f.ReactUntil = now + Delay();
            var showing = f.Showing(now);
            f.Up = BenchCue.Stands(showing);
            if (showing != BenchReaction.None && !float.IsNaN(f.Look)) f.Heading += (f.Look - f.Heading) * MathF.Min(1f, 6f * dt);
        }
        foreach (var s in staff) s.Update(now, dt, world.Puck.Position.X);
    }

    /// <summary>Ends a figure's reaction when it is over or he gets up to walk, and starts taking the helmet off after a long shift.</summary>
    void Settle(BenchFigure f, float now)
    {
        if (f.Reaction != BenchReaction.None && (now >= f.ReactUntil || !Free(f)))
        {
            if (f.Reaction == BenchReaction.HelmetOff && now >= f.ReactUntil) f.Bare = true;
            f.Reaction = BenchReaction.None;
            f.Look = float.NaN;
        }
        if (!f.SatDown || f.Spent >= BenchCue.LongShift || f.Bare) return;
        Start(f, BenchReaction.HelmetOff, now + Delay(), BenchCue.HelmetSeconds);
        StaffOf(f.Player.Side, false).Visit(f.X, 3f, now);   // and the trainer comes to see him
    }

    /// <summary>Gets whether a figure is in his seat and free to react: a seated skater, not walking or up for a change.</summary>
    static bool Free(BenchFigure f) => !f.Player.IsGoalie && !f.Walking && f.Pose == BenchPose.Watch;

    void Apply(World world, BenchCrowd crowd, Side side, BenchCue cue)
    {
        float now = world.Time;
        pick.Clear();
        foreach (var f in crowd.Shown)
        {
            if (f.Player.Side != side || !Free(f) || f.Reaction > cue.Reaction) continue;
            if (f.Reaction == cue.Reaction && (cue.Count != BenchCue.All || float.IsPositiveInfinity(f.ReactUntil))) continue;   // already on it
            if (cue.Reaction == BenchReaction.HelmetOff && f.Bare) continue;
            pick.Add(f);
        }
        if (pick.Count == 0) return;
        switch (cue.Reaction)
        {
            case BenchReaction.Chat:
                Chats(cue, now);
                return;
            case BenchReaction.Trainer or BenchReaction.Instruct:
                Talk(side, cue, now);
                return;
            case BenchReaction.Argue or BenchReaction.Point:
                float door = BenchLayout.DoorX(side);
                pick.Sort((a, b) => MathF.Abs(a.X - door).CompareTo(MathF.Abs(b.X - door)));
                break;
            default:
                Shuffle();
                break;
        }
        for (int i = 0; i < Math.Min(cue.Count, pick.Count); i++)
        {
            // a goal: one in four of the bench (in shuffled order) bangs the boards instead of throwing his arms up
            var r = cue.Reaction == BenchReaction.Cheer && i % 4 == 3 ? BenchReaction.Bang : cue.Reaction;
            Start(pick[i], r, now + Delay(), cue.Seconds);
            if (cue.Reaction == BenchReaction.Point) pick[i].Look = 0f;   // squared up to the ice
        }
        var coach = StaffOf(side, true);
        var trainer = StaffOf(side, false);
        switch (cue.Reaction)
        {
            case BenchReaction.Cheer:
                coach.Do(StaffPose.Clap, cue.Seconds, now);
                trainer.Do(StaffPose.Clap, cue.Seconds, now);
                break;
            case BenchReaction.Argue:
                coach.Do(StaffPose.ArmsUp, cue.Seconds, now);
                break;
            default:
                break;
        }
    }

    /// <summary>Pairs of neighbours turn to each other, one talking and one listening.</summary>
    void Chats(BenchCue cue, float now)
    {
        pick.Sort((a, b) => a.X.CompareTo(b.X));
        int pairs = cue.Count / 2, tries = 8;
        while (pairs > 0 && tries-- > 0 && pick.Count > 1)
        {
            int i = dice.Next(pick.Count - 1);
            var (a, b) = (pick[i], pick[i + 1]);
            if (b.X - a.X > BenchLayout.MaxPitch + 0.05f || a.Reaction != BenchReaction.None || b.Reaction != BenchReaction.None) continue;
            float seconds = 2f + (2f * dice.NextSingle()), from = now + Delay();
            bool aTalks = dice.Next(2) == 0;
            Start(a, aTalks ? BenchReaction.Chat : BenchReaction.Listen, from, seconds);
            Start(b, aTalks ? BenchReaction.Listen : BenchReaction.Chat, from, seconds);
            a.Look = 0.9f;
            b.Look = -0.9f;
            pairs--;
        }
    }

    /// <summary>The trainer walks to the most tired seated skater, or the coach to one of the next unit, to talk to him.</summary>
    void Talk(Side side, BenchCue cue, float now)
    {
        bool coach = cue.Reaction == BenchReaction.Instruct;
        var who = StaffOf(side, coach);
        if (!who.Free) return;
        var f = coach ? pick[dice.Next(pick.Count)] : pick[0];
        if (!coach)
        {
            foreach (var other in pick)
            {
                if (other.Player.Energy < f.Player.Energy) f = other;
            }
        }
        if (!who.Visit(f.X, cue.Seconds, now)) return;
        float arrives = who.Arrives(now);
        Start(f, cue.Reaction, arrives, cue.Seconds);
        f.Look = (who.Home > f.X ? 1f : -1f) * 1.3f;   // turned back to him
    }

    void Start(BenchFigure f, BenchReaction r, float from, float seconds)
    {
        f.Reaction = r;
        f.ReactFrom = from;
        f.ReactUntil = from + seconds;
        f.Look = float.NaN;
    }

    float Delay() => dice.NextSingle() * MaxDelay;

    void Shuffle()
    {
        for (int i = pick.Count - 1; i > 0; i--)
        {
            int j = dice.Next(i + 1);
            (pick[i], pick[j]) = (pick[j], pick[i]);
        }
    }
}
