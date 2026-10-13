using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players;

namespace StickAndPuck.App.AI;

/// <summary>
/// W22a: where an off-puck attacker goes to be open. Around the spot the shape or the cycle gives
/// him, he tries a ring of spots and picks the one where a pass from the carrier would get through
/// and the puck would be worth most: a clear lane (<see cref="Choices.LaneSuccess"/>), a good shot
/// from there (<see cref="Choices.SpotValue"/>), room around it, and not too far off his spot.
/// He reads the defenders like he reads a pass, by hockey IQ, and every read is rolled with
/// <see cref="Jitter"/>, so a smart skater mostly finds the hole and now and then drifts into a
/// covered lane, and a poor one sometimes finds it anyway.
/// </summary>
public static class GetOpen
{
    const int Angles = 8;

    /// <summary>Gets the best spot to be open for a pass, as this skater reads the play.</summary>
    /// <param name="world">The match.</param>
    /// <param name="p">The off-puck attacker.</param>
    /// <param name="carrier">His teammate with the puck.</param>
    /// <param name="spot">The spot the shape or the cycle gives him (x, z).</param>
    /// <param name="radius">How far from that spot he may go, in metres.</param>
    /// <param name="minA">The least distance toward the attacked net (x times end) he may go to.</param>
    /// <param name="maxA">The most distance toward the attacked net he may go to, so forwards stay onside.</param>
    /// <param name="hockeyIq">The hockey IQ this read is played with, after the jitter roll.</param>
    /// <param name="rng">Randomness for misreads and judgement.</param>
    /// <param name="last">Where he was going after his last read, kept a little longer to stop him dithering.</param>
    /// <returns>The spot (x, z).</returns>
    public static Vector2 Best(World world, Player p, Player carrier, Vector2 spot, float radius, float minA, float maxA, int hockeyIq, Random rng, Vector2? last)
    {
        int end = PuckHandling.AttackEnd(world, p.Side);
        var from = new Vector2(world.Puck.Position.X, world.Puck.Position.Z);
        float misread = Choices.Misread(hockeyIq), wobble = Choices.Wobble(hockeyIq);
        var seen = new List<(Vector2 At, float Speed)>();
        foreach (var o in world.Players)
        {
            if (!o.OnIce || o.Side == p.Side || o.Action == PlayerAction.Fallen) continue;
            var off = new Vector2(PuckHandling.Gauss(rng), PuckHandling.Gauss(rng)) * misread * 0.6f;
            var at = o.IsGoalie ? o.Position : PuckHandling.Blade(o);
            seen.Add((new Vector2(at.X, at.Z) + off, o.IsGoalie ? 3f : SkatingModel.TopSpeed(o.Data.Traits, o.Energy)));
            if (!o.IsGoalie) seen.Add((new Vector2(o.Position.X, o.Position.Z) + off, SkatingModel.TopSpeed(o.Data.Traits, o.Energy)));
        }

        var best = Clamp(spot, end, minA, maxA);
        float bestValue = float.MinValue;
        void Try(Vector2 c, float bonus)
        {
            c = Clamp(c, end, minA, maxA);
            float value = Value(end, from, c, spot, seen, hockeyIq) + bonus + wobble * PuckHandling.Gauss(rng);
            if (value > bestValue) { bestValue = value; best = c; }
        }

        Try(spot, 0f);
        if (last is { } l && Vector2.Distance(l, spot) < radius * 1.2f) Try(l, 0.015f);
        float turn = rng.NextSingle() * MathF.Tau / Angles;
        foreach (float r in new[] { radius * 0.5f, radius })
        {
            for (int i = 0; i < Angles; i++)
            {
                float angle = turn + i * MathF.Tau / Angles;
                Try(spot + new Vector2(SimMath.Cos(angle), SimMath.Sin(angle)) * r, 0f);
            }
        }

        return best;
    }

    /// <summary>
    /// Gets what being on a spot is worth to an off-puck attacker: the chance a pass from the puck
    /// gets there, times what the puck is worth there and the room around it, less a little for
    /// straying from his spot.
    /// </summary>
    /// <param name="end">The attacked end.</param>
    /// <param name="from">The puck (x, z).</param>
    /// <param name="at">The spot tried (x, z).</param>
    /// <param name="home">The spot the shape or the cycle gives him (x, z).</param>
    /// <param name="defenders">The opponents as he reads them.</param>
    /// <param name="hockeyIq">The hockey IQ the read is played with.</param>
    /// <returns>The value; higher is better.</returns>
    public static float Value(int end, Vector2 from, Vector2 at, Vector2 home, IReadOnlyList<(Vector2 At, float Speed)> defenders, int hockeyIq)
    {
        float d = Vector2.Distance(from, at);
        float lane = Choices.LaneSuccess(from, at, PuckHandling.PassSpeed(d), defenders, hockeyIq);
        if (d < Choices.ShortestPass) lane *= 0.3f;
        if (Cycle.ThroughNet(from, at)) lane *= 0.1f;
        return lane * (0.15f + Choices.SpotValue(end, at)) * (0.6f + 0.4f * Choices.Room(at, defenders)) - 0.008f * Vector2.Distance(at, home);
    }

    static Vector2 Clamp(Vector2 c, int end, float minA, float maxA)
    {
        float a = Math.Clamp(end * c.X, minA, maxA);
        return TeamShape.Inside(new Vector2(end * a, c.Y));
    }
}
