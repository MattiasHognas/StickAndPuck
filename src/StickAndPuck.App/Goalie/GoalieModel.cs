using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Physics;

namespace StickAndPuck.App.Goalie;

/// <summary>Where a shot meets a goalie: the offset across the crease from the goalie's centre, the height, and when.</summary>
/// <param name="Offset">Distance along z from the goalie's centre to the puck.</param>
/// <param name="Height">Height of the puck above the ice.</param>
/// <param name="Time">Seconds until the puck gets there.</param>
public readonly record struct ShotLine(float Offset, float Height, float Time);

/// <summary>
/// The goalie's maths, with no Raylib and no randomness, so tests and the AI can use it: where
/// to stand, how fast a goalie reacts and moves from goalie reflexes, where a shot will cross,
/// and whether a stance covers a point.
/// </summary>
public static class GoalieModel
{
    /// <summary>The end of the rink a goalie defends: -1 for the net at -x, +1 for the net at +x.</summary>
    /// <param name="world">The match state.</param>
    /// <param name="goalie">The goalie.</param>
    /// <returns>-1 or +1.</returns>
    public static int End(World world, Player goalie)
    {
        return goalie.Side == world.Match.DefendsLeft ? -1 : 1;
    }

    /// <summary>Blends a value from a goalie with reflexes 0 to one with reflexes 100.</summary>
    /// <param name="traits">The goalie's traits.</param>
    /// <param name="slow">The value at reflexes 0.</param>
    /// <param name="fast">The value at reflexes 100.</param>
    /// <returns>The value for this goalie.</returns>
    public static float ByReflexes(Traits traits, float slow, float fast)
    {
        return slow + (fast - slow) * Traits.Unit(traits.GoalieReflexes);
    }

    /// <summary>Seconds from a shot being released to the goalie moving toward it.</summary>
    /// <param name="traits">The goalie's traits.</param>
    /// <returns>The reaction time.</returns>
    public static float ReactionTime(Traits traits)
    {
        return ByReflexes(traits, GoalieTuning.ReactSlow, GoalieTuning.ReactFast);
    }

    /// <summary>Speed of the goalie's push toward a shot.</summary>
    /// <param name="traits">The goalie's traits.</param>
    /// <returns>Metres per second.</returns>
    public static float SaveSpeed(Traits traits)
    {
        return ByReflexes(traits, GoalieTuning.SaveSpeedSlow, GoalieTuning.SaveSpeedFast);
    }

    /// <summary>
    /// Where a goalie stands for a puck: on the line from the middle of the net to the puck, further
    /// out the further away the puck is, and tight to the near post once the puck is behind the line.
    /// </summary>
    /// <param name="end">The defended end, -1 or +1.</param>
    /// <param name="puck">The puck position.</param>
    /// <returns>The spot on the ice.</returns>
    public static Vector3 Spot(int end, Vector3 puck)
    {
        return Spot(end, puck, 50);
    }

    /// <summary>
    /// Where a goalie with a given positioning stands for a puck (see <see cref="Spot(int, Vector3)"/>).
    /// Positioning is the goalie's <see cref="Traits.HockeyIq"/>, its read of the play: a poor one
    /// stays deeper in the net and squares up only part of the way to the puck, leaving the far side
    /// open; a good one comes further out to cut the angle. 50 plays exactly as the plain spot.
    /// </summary>
    /// <param name="end">The defended end, -1 or +1.</param>
    /// <param name="puck">The puck position.</param>
    /// <param name="positioning">The goalie's positioning, 0 to 100.</param>
    /// <returns>The spot on the ice.</returns>
    public static Vector3 Spot(int end, Vector3 puck, int positioning)
    {
        var net = RinkSpec.NetMouth(end);
        float outX = (net.X - puck.X) * end;                       // metres in front of the goal line
        if (outX < 0.3f)
        {
            float side = MathF.Abs(puck.Z) < 0.01f ? 0 : MathF.Sign(puck.Z);
            return new Vector3(net.X - end * 0.3f, 0, side * (RinkSpec.NetWidth / 2 - GoalieTuning.PostHug));
        }
        float square = MathF.Min(1f, Traits.Scale(positioning, GoalieTuning.SquareWorst, GoalieTuning.SquareBest));
        var toward = new Vector2(-end * outX, (puck.Z - net.Z) * square);
        float distance = toward.Length();
        float far = GoalieTuning.DepthFar * Traits.Scale(positioning, GoalieTuning.DepthWorst, GoalieTuning.DepthBest);
        float depth = GoalieTuning.DepthNear + (far - GoalieTuning.DepthNear) * Math.Clamp((distance - 2f) / 7f, 0f, 1f);
        var step = Vector2.Normalize(toward) * depth;
        return new Vector3(net.X + step.X, 0, net.Z + step.Y);
    }

    /// <summary>Whether a loose puck is headed at the net, near enough to the posts to need a save.</summary>
    /// <param name="end">The defended end, -1 or +1.</param>
    /// <param name="position">The puck position.</param>
    /// <param name="velocity">The puck velocity.</param>
    /// <returns>True for a shot on this net.</returns>
    public static bool IsShot(int end, Vector3 position, Vector3 velocity)
    {
        if (velocity.X * end < GoalieTuning.ShotSpeed) return false;
        if (position.X * end >= RinkSpec.GoalLineX) return false;
        var at = Cross(position, velocity, end * RinkSpec.GoalLineX);
        return MathF.Abs(at.Offset) < RinkSpec.NetWidth / 2 + GoalieTuning.ShotMargin && at.Height < RinkSpec.NetHeight + GoalieTuning.ShotMargin;
    }

    /// <summary>Where a puck in flight crosses the plane x = <paramref name="planeX"/>, with gravity on a puck off the ice.</summary>
    /// <param name="position">The puck position.</param>
    /// <param name="velocity">The puck velocity; its x must head toward the plane.</param>
    /// <param name="planeX">The plane's x.</param>
    /// <returns>The crossing, with Offset as the puck's z there.</returns>
    public static ShotLine Cross(Vector3 position, Vector3 velocity, float planeX)
    {
        float t = MathF.Abs(velocity.X) < 1e-4f ? float.PositiveInfinity : (planeX - position.X) / velocity.X;
        if (t < 0 || float.IsInfinity(t)) return new ShotLine(float.PositiveInfinity, 0, float.PositiveInfinity);
        float y = position.Y > PuckMotion.HalfThickness || velocity.Y > 0
            ? MathF.Max(0, position.Y + velocity.Y * t - 0.5f * PuckTuning.Gravity * t * t)
            : 0;
        return new ShotLine(position.Z + velocity.Z * t, y, t);
    }

    /// <summary>
    /// How much more of the net a goalie fills against a shot from close in: a skater walking in
    /// gives the goalie time to square up and fill the angle, so up close the reach grows, from 1
    /// at <see cref="GoalieTuning.CloseRange"/> up to 1 + <see cref="GoalieTuning.CloseBonus"/> on the doorstep.
    /// </summary>
    /// <param name="shotFrom">Metres from the middle of the goal line where the shot was first seen, or NaN.</param>
    /// <returns>The factor the reach is multiplied by.</returns>
    public static float CloseRead(float shotFrom)
    {
        return CloseRead(shotFrom, 50);
    }

    /// <summary>
    /// <see cref="CloseRead(float)"/> for a goalie with a given positioning: a good positional goalie
    /// fills more of the net on a close shot, a poor one less (half to one and a half the bonus).
    /// </summary>
    /// <param name="shotFrom">Metres from the middle of the goal line where the shot was first seen, or NaN.</param>
    /// <param name="positioning">The goalie's positioning, 0 to 100.</param>
    /// <returns>The factor the reach is multiplied by.</returns>
    public static float CloseRead(float shotFrom, int positioning)
    {
        if (float.IsNaN(shotFrom)) return 1f;
        return 1f + GoalieTuning.CloseBonus * Traits.Scale(positioning, 0.5f, 1.5f) * Math.Clamp((GoalieTuning.CloseRange - shotFrom) / (GoalieTuning.CloseRange - 2.5f), 0f, 1f);
    }

    /// <summary>
    /// How well a stance covers a point on the goalie's plane: 0 means dead centre, 1 the very edge
    /// of the body, pads or hands, and anything above 1 is open net past the goalie.
    /// </summary>
    /// <param name="offset">Distance along z from the goalie's centre, signed.</param>
    /// <param name="height">Height of the puck.</param>
    /// <param name="butterfly">Whether the goalie is down in the butterfly.</param>
    /// <returns>The share of the reach the puck is out.</returns>
    public static float Coverage(float offset, float height, bool butterfly)
    {
        return Coverage(offset, height, butterfly, 1f);
    }

    /// <summary>
    /// <see cref="Coverage(float, float, bool)"/> with the glove or blocker on that side reaching
    /// <paramref name="hand"/> times as far as an average one (see <see cref="HandScale"/>).
    /// </summary>
    /// <param name="offset">Distance along z from the goalie's centre, signed.</param>
    /// <param name="height">Height of the puck.</param>
    /// <param name="butterfly">Whether the goalie is down in the butterfly.</param>
    /// <param name="hand">How far the hand on the puck's side reaches, 1 for average.</param>
    /// <returns>The share of the reach the puck is out.</returns>
    public static float Coverage(float offset, float height, bool butterfly, float hand)
    {
        float reach;
        if (height > GoalieTuning.ShoulderHeight + (butterfly ? -0.45f : 0.2f)) reach = GoalieTuning.HandReach * 0.6f * hand;
        else if (height < GoalieTuning.LowShot) reach = butterfly ? GoalieTuning.ButterflyHalfWidth : GoalieTuning.BodyHalfWidth + 0.1f;
        else reach = GoalieTuning.BodyHalfWidth + GoalieTuning.HandReach * hand;
        return MathF.Abs(offset) / reach;
    }

    /// <summary>Whether a shot at this height is played with the glove or blocker rather than the pads.</summary>
    /// <param name="height">Height of the puck.</param>
    /// <returns>True for a shot the hands stop.</returns>
    public static bool HandHeight(float height)
    {
        return height >= GoalieTuning.LowShot;
    }

    /// <summary>
    /// The z side of the goalie's glove: a goalie in the data's <see cref="PlayerData.Shoots"/> is its
    /// catching hand, and catching left (the usual "regular" goalie) puts the glove on the goalie's
    /// left, which is +z for the net at +x and -z for the net at -x.
    /// </summary>
    /// <param name="data">The goalie.</param>
    /// <param name="end">The defended end, -1 or +1.</param>
    /// <returns>+1 or -1.</returns>
    public static int GloveSide(PlayerData data, int end)
    {
        return (data.Shoots == Hand.Left ? 1 : -1) * end;
    }

    /// <summary>The goalie's hand trait on one side of its body.</summary>
    /// <param name="data">The goalie.</param>
    /// <param name="end">The defended end, -1 or +1.</param>
    /// <param name="side">The z side, +1 or -1.</param>
    /// <returns>Glove or blocker, 0 to 100.</returns>
    public static int HandTrait(PlayerData data, int end, int side)
    {
        return side == GloveSide(data, end) ? data.Traits.GoalieGlove : data.Traits.GoalieBlocker;
    }

    /// <summary>How far a hand trait reaches compared with an average one: 0.5 at 0, 1 at 50, 1.5 at 100.</summary>
    /// <param name="hand">Glove or blocker, 0 to 100.</param>
    /// <returns>The reach factor.</returns>
    public static float HandScale(int hand)
    {
        return Traits.Scale(hand, GoalieTuning.HandWorst, GoalieTuning.HandBest);
    }

    /// <summary>
    /// The goalie's weak side for a shooter to pick: the z side of the weaker of glove and blocker,
    /// or 0 when they are within <see cref="GoalieTuning.WeakSideGap"/> of each other. AI shooters
    /// aim here, as often as their hockey IQ reads it.
    /// </summary>
    /// <param name="goalie">The goalie.</param>
    /// <param name="end">The defended end, -1 or +1.</param>
    /// <returns>+1, -1 or 0.</returns>
    public static int WeakSide(Player goalie, int end)
    {
        var t = goalie.Data.Traits;
        int gap = t.GoalieGlove - t.GoalieBlocker;
        if (Math.Abs(gap) < GoalieTuning.WeakSideGap) return 0;
        int glove = GloveSide(goalie.Data, end);
        return gap < 0 ? glove : -glove;
    }
}
