using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players;

namespace StickAndPuck.App.AI;

/// <summary>One pass a carrier could make: to whom, where the puck is aimed, and what it is worth.</summary>
/// <param name="Receiver">The teammate.</param>
/// <param name="Target">Where the pass is aimed (x, z), leading the receiver.</param>
/// <param name="Success">The passer's estimate that the pass arrives, 0 to 1.</param>
/// <param name="Value">What the pass is worth: <see cref="Success"/> times the value of the receiver's spot.</param>
public readonly record struct PassOption(Player Receiver, Vector2 Target, float Success, float Value);

/// <summary>
/// W6b: the maths behind the AI's choices, with no state and no Raylib, so tests can ask what a
/// skater would think of a shot or a pass. Values share one scale, roughly "how likely this
/// possession ends in a good chance": a shot from the slot is worth about 0.6, the puck in the
/// neutral zone about 0.1.
/// <list type="bullet">
/// <item>Smart passes sets how well a passer sees: how far up the ice they look, how well they
/// judge whether a defender can get to the lane in time, and how much their judgement wobbles.
/// A poor passer misreads where defenders are and assumes they react slowly, so passes into
/// covered lanes; a smart one finds the open teammate further up the ice.</item>
/// <item>Shot accuracy sets how far out a skater is willing to shoot from.</item>
/// </list>
/// </summary>
public static class Choices
{
    /// <summary>What keeping the puck in the attacking-zone cycle is worth on top of its spot (W22a).</summary>
    public const float CycleKeep = 0.08f;

    /// <summary>What losing the puck on a pass costs in the cycle, where a turnover gives up the zone (W22a).</summary>
    public const float CycleTurnover = 0.2f;

    /// <summary>Passes shorter than this are not worth making.</summary>
    public const float ShortestPass = 4f;

    const float Reaction = 0.2f;     // seconds before a defender reacts to a pass
    const float StickReach = 0.8f;   // how far from the blade a defender can knock a pass down
    const int LaneSamples = 8;
    const float CloseStick = 1.4f;   // a stick this near the puck as it leaves covers every lane on its side       // points along a lane checked against each defender

    /// <summary>Gets how good a shot from a spot is: 1 on the doorstep, about 0.4 in the slot, near 0 from the blue line or a bad angle.</summary>
    /// <param name="end">The attacked end, +1 or -1.</param>
    /// <param name="at">The spot (x, z).</param>
    /// <returns>0 to 1.</returns>
    public static float ShotQuality(int end, Vector2 at)
    {
        float dx = RinkSpec.GoalLineX - end * at.X;
        if (dx < 0.8f) return 0f;
        float d = new Vector2(dx, at.Y).Length();
        float angle = SimMath.Atan2(MathF.Abs(at.Y), dx);
        float range = Math.Clamp(1f - (d - 3f) / 19f, 0f, 1f);
        float open = Math.Clamp(1f - angle / 1.3f, 0f, 1f);
        return range * range * open;
    }

    /// <summary>Gets what holding the puck on a spot is worth to the attacking side, before pressure.</summary>
    /// <param name="end">The attacked end.</param>
    /// <param name="at">The spot (x, z).</param>
    /// <returns>About 0.03 deep in the own end up to about 0.7 on the doorstep.</returns>
    public static float SpotValue(int end, Vector2 at)
    {
        float progress = Math.Clamp((end * at.X + RinkSpec.GoalLineX) / (2 * RinkSpec.GoalLineX), 0f, 1f);
        return 0.03f + 0.1f * progress + 0.6f * ShotQuality(end, at);
    }

    /// <summary>Gets how far a passer looks for teammates, in metres.</summary>
    /// <param name="hockeyIq">The hockey IQ trait.</param>
    /// <returns>Metres.</returns>
    public static float Vision(int hockeyIq)
    {
        return 14f + 26f * Traits.Unit(hockeyIq);
    }

    /// <summary>Gets how long a carrier takes between looks at the play, in seconds.</summary>
    /// <param name="hockeyIq">The hockey IQ trait.</param>
    /// <returns>Seconds.</returns>
    public static float ThinkEvery(int hockeyIq)
    {
        return 0.1f + 0.35f * (1f - Traits.Unit(hockeyIq));
    }

    /// <summary>Gets how many metres off a passer reads where each defender is.</summary>
    /// <param name="hockeyIq">The hockey IQ trait.</param>
    /// <returns>Metres.</returns>
    public static float Misread(int hockeyIq)
    {
        return 2.5f * (1f - Traits.Unit(hockeyIq));
    }

    /// <summary>Gets how much a carrier's opinion of each option wobbles.</summary>
    /// <param name="hockeyIq">The hockey IQ trait.</param>
    /// <returns>Spread of the noise added to values.</returns>
    public static float Wobble(int hockeyIq)
    {
        return 0.1f * (1f - Traits.Unit(hockeyIq));
    }

    /// <summary>
    /// Gets the chance a pass along a lane arrives, as a passer sees it: for points along the lane,
    /// whether any defender gets a stick there before the puck. A poor passer thinks defenders
    /// react later than they do.
    /// </summary>
    /// <param name="from">The puck (x, z).</param>
    /// <param name="to">The aim point (x, z).</param>
    /// <param name="speed">The pass speed.</param>
    /// <param name="defenders">Where the passer thinks each defender's stick is, with their top speed.</param>
    /// <param name="hockeyIq">The passer's hockey IQ.</param>
    /// <returns>0 to 1.</returns>
    public static float LaneSuccess(Vector2 from, Vector2 to, float speed, IReadOnlyList<(Vector2 At, float Speed)> defenders, int hockeyIq)
    {
        float length = Vector2.Distance(from, to);
        float react = Reaction + 0.5f * (1f - Traits.Unit(hockeyIq));
        Span<float> times = stackalloc float[LaneSamples + 1];
        SampleTimes(length, speed, times);
        float success = 1f;
        foreach (var (at, top) in defenders)
        {
            float worst = 0f;
            for (int i = 0; i <= LaneSamples; i++)
            {
                float f = i / (float)LaneSamples;
                var point = Vector2.Lerp(from, to, f);
                float puckTime = times[i];
                float reach = Math.Max(0f, Vector2.Distance(at, point) - StickReach);
                // a stick already in the lane needs no reaction: the puck runs into it
                float margin = react * Math.Min(1f, reach / 0.6f) + reach / Math.Max(top, 1f) - puckTime;
                worst = Math.Max(worst, 1f / (1f + SimMath.Exp(margin / 0.08f)));
            }

            // W22a: a stick right at the release, on the side the pass goes, gets a touch on it nearly every time
            var near = at - from;
            float gap = near.Length();
            if (gap < CloseStick && length > 1e-3f && Vector2.Dot(near / Math.Max(gap, 1e-3f), (to - from) / length) > 0.2f) worst = Math.Max(worst, 0.85f);
            success *= 1f - worst;
        }
        return success;
    }

    /// <summary>
    /// Fills in when a sliding puck reaches each of <see cref="LaneSamples"/> + 1 evenly spaced points
    /// along a lane, in one pass of the same friction maths as <see cref="PuckHandling.SlideTime"/>,
    /// so lane reads stay cheap enough for every off-puck skater to make many of them (W22a).
    /// </summary>
    /// <param name="length">The lane's length in metres.</param>
    /// <param name="speed">The pass speed.</param>
    /// <param name="times">Seconds to each point; 5 s for points the puck never reaches.</param>
    static void SampleTimes(float length, float speed, Span<float> times)
    {
        const float step = 1 / 60f;
        float covered = 0, t = 0;
        int next = 0;
        while (next < times.Length && speed > 0.1f && t < 5f)
        {
            while (next < times.Length && covered >= length * next / (times.Length - 1)) times[next++] = t;
            covered += speed * step;
            speed -= (Physics.PuckTuning.IceFriction + Physics.PuckTuning.Drag * speed) * step;
            t += step;
        }
        for (; next < times.Length; next++) times[next] = t;
    }

    /// <summary>Gets how much room a spot has: 0 with a defender on it, 1 with none within 4 m.</summary>
    /// <param name="at">The spot (x, z).</param>
    /// <param name="defenders">The defenders.</param>
    /// <returns>0 to 1.</returns>
    public static float Room(Vector2 at, IReadOnlyList<(Vector2 At, float Speed)> defenders)
    {
        float nearest = defenders.Count == 0 ? 99f : defenders.Min(d => Vector2.Distance(d.At, at));
        return Math.Clamp(nearest / 4f, 0f, 1f);
    }

    /// <summary>Gets how clear the line from a spot to the net is: 1 clear, down to 0.3 with a body in it.</summary>
    /// <param name="end">The attacked end.</param>
    /// <param name="at">The shooter (x, z).</param>
    /// <param name="blockers">Defending skaters (not the goalie).</param>
    /// <returns>0.3 to 1.</returns>
    public static float ShotLane(int end, Vector2 at, IEnumerable<Vector2> blockers)
    {
        var net = new Vector2(end * RinkSpec.GoalLineX, 0);
        float nearest = blockers.Select(b => SegmentDistance(at, net, b)).DefaultIfEmpty(99f).Min();
        return Math.Clamp(nearest / 1.2f, 0.3f, 1f);
    }

    /// <summary>
    /// Gets whether a pass would put a teammate offside: the puck still outside the attacking
    /// zone while the receiver is already inside it.
    /// </summary>
    /// <param name="end">The attacked end.</param>
    /// <param name="puck">The puck (x, z).</param>
    /// <param name="receiver">The receiver (x, z).</param>
    /// <returns>True when offside.</returns>
    public static bool Offside(int end, Vector2 puck, Vector2 receiver)
    {
        return end * puck.X < RinkSpec.BlueLineX && end * receiver.X > RinkSpec.BlueLineX + 0.3f;
    }

    /// <summary>Gets the side of the net to shoot at: away from where the goalie leans, else the far side.</summary>
    /// <param name="at">The shooter (x, z).</param>
    /// <param name="goalieZ">The goalie's z, or None with no goalie.</param>
    /// <returns>+1 for +z, -1 for -z.</returns>
    public static int ShotSide(Vector2 at, Maybe<float> goalieZ)
    {
        if (goalieZ.TryGet(out float g) && MathF.Abs(g) > 0.15f) return g > 0 ? -1 : 1;
        return at.Y > 0 ? -1 : 1;
    }

    /// <summary>Gets the distance from a point to a segment.</summary>
    /// <param name="a">The segment's start.</param>
    /// <param name="b">The segment's end.</param>
    /// <param name="p">The point.</param>
    /// <returns>Metres.</returns>
    public static float SegmentDistance(Vector2 a, Vector2 b, Vector2 p)
    {
        var ab = b - a;
        float t = Math.Clamp(Vector2.Dot(p - a, ab) / Math.Max(ab.LengthSquared(), 1e-6f), 0f, 1f);
        return Vector2.Distance(a + ab * t, p);
    }
}
