using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Rules;

public static partial class Penalties
{
    /// <summary>Gets whether a skater is skating to the box: called while play is stopped, on the ice and not in it yet.</summary>
    public static bool OnTheWay(World world, int playerId)
    {
        return Book(world).Trips.Exists(t => t.PlayerId == playerId);
    }

    /// <summary>Sends a penalised skater to the box. While play is stopped a skater on the ice skates
    /// there, to a spot in front of his club's box door and in through it, and the faceoff waits
    /// for him (<see cref="Skate"/>); otherwise, or already off the ice, he is put straight in it.</summary>
    static void SendToBox(World world, PenaltyBook book, Player p)
    {
        if (world.Match.Phase != MatchPhase.Stopped || !p.OnIce || book.Trips.Exists(t => t.PlayerId == p.Id))
        {
            ToBox(world, p);
            return;
        }
        HandOff(world, p);
        if (world.Puck.Carrier == p.Id) world.Puck.Carrier = -1;
        p.Command = default;
        p.Begin(PlayerAction.None);
        book.Trips.Add(new BoxTrip(p.Id, p.Position with { Y = 0 }, p.Velocity with { Y = 0 }, world.Time));
        HoldFaceoff(world);
    }

    /// <summary>Moves every skater on his way to the box one step and puts him in it at the door. Play
    /// starting again under him (a period's end, the match's), a skater taken off the ice by
    /// someone else, or a skate that takes too long ends the trip: he is put in the box at once.
    /// The faceoff waits while anyone is still on his way.</summary>
    internal static void Skate(World world, PenaltyBook book, float dt)
    {
        if (book.Trips.Count == 0) return;
        bool stopped = world.Match.Phase == MatchPhase.Stopped;
        foreach (var trip in book.Trips.ToList())
        {
            var p = world.Players[trip.PlayerId];
            if (stopped && p.OnIce && world.Time - trip.Started < PenaltyTuning.TripTimeout && !Step(world, p, trip, dt)) continue;
            book.Trips.Remove(trip);
            if (!book.Ejected.Contains(p.Id)) ToBox(world, p);
        }
        if (stopped && book.Trips.Count > 0) HoldFaceoff(world);
    }

    /// <summary>Keeps the stoppage running until a moment after the last skater is in the box.</summary>
    static void HoldFaceoff(World world)
    {
        var s = MatchFlow.State(world);
        s.Timer = Math.Max(s.Timer, PenaltyTuning.TripAfter);
    }

    /// <summary>One step of a skate to the box, from where the trip left him last step (whatever the AI and
    /// skating did to him since is undone): he lines up in front of the door, then skates in, easing
    /// off for it. Gets whether he reached the door.</summary>
    static bool Step(World world, Player p, BoxTrip trip, float dt)
    {
        var door = BoxDoor(p.Side);
        var lineUp = door with { Z = RinkSpec.HalfWidth - PenaltyTuning.TripLineUp };
        var inside = door with { Z = RinkSpec.HalfWidth - PenaltyTuning.TripInside };
        var pos = trip.Position;
        if (!trip.LinedUp && (Vector3.Distance(pos, lineUp) < 1f || (pos.Z > lineUp.Z - 0.5f && MathF.Abs(pos.X - door.X) < 1f))) trip.LinedUp = true;
        if (trip.LinedUp && (Vector3.Distance(pos, inside) < PenaltyTuning.TripReach || (pos.Z >= inside.Z - 0.01f && MathF.Abs(pos.X - door.X) < 0.5f))) return true;

        var to = trip.LinedUp ? inside : lineUp;
        var way = to - pos;
        float d = way.Length();
        float left = trip.LinedUp ? d : d + Vector3.Distance(lineUp, inside);
        var want = d > 1e-4f ? way / d : Vector3.UnitZ;
        float speed = trip.Velocity.Length();
        var dir = speed > 0.3f ? trip.Velocity / speed : want;
        dir += (want - dir) * MathF.Min(1f, PenaltyTuning.TripTurn * dt);
        dir = dir.LengthSquared() > 1e-4f ? Vector3.Normalize(dir) : want;
        float cap = MathF.Min(PenaltyTuning.TripSpeed, MathF.Sqrt(2 * PenaltyTuning.TripBrake * left) + PenaltyTuning.TripDoorSpeed);
        speed = speed < cap ? MathF.Min(cap, speed + (PenaltyTuning.TripAccel * dt)) : MathF.Max(cap, speed - (2 * PenaltyTuning.TripBrake * dt));
        trip.Velocity = dir * speed;
        var next = pos + (trip.Velocity * dt);
        trip.Position = new Vector3(
            Math.Clamp(next.X, -RinkSpec.HalfLength + 1f, RinkSpec.HalfLength - 1f), 0,
            Math.Clamp(next.Z, -RinkSpec.HalfWidth + 1f, inside.Z));

        HandOff(world, p);
        if (p.Action != PlayerAction.None) p.Begin(PlayerAction.None);
        p.Position = trip.Position;
        p.Velocity = trip.Velocity;
        p.Heading = SimMath.Atan2(dir.X, dir.Z);
        p.Command = default;
        return false;
    }
}
