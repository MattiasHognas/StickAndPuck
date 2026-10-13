using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Rules;

/// <summary>
/// W7b: delayed offside as the SHL plays it. When the puck wholly crosses a blue line into a zone,
/// sent there by the attacking club, any attacker already in the zone with both skates past the
/// line (other than the puck carrier) makes the offside pending: <see cref="Pending"/> tells the
/// referee to raise an arm. The pending offside is waved off when every attacker tags up at the
/// line or the puck leaves the zone. It is called when an attacker touches the puck in the zone,
/// or at once when the puck is shot at the net. A call publishes Offside (Side = offending club)
/// and whistles a faceoff at the neutral-zone dot outside that zone, on the puck's side.
/// </summary>
internal sealed class OffsideRule : ISystem
{
    /// <summary>Half the painted width of a blue line (30 cm).</summary>
    public const float LineHalfWidth = 0.15f;

    /// <summary>How far past a player's centre the trailing skate reaches; a skate on the line keeps the player onside.</summary>
    public const float SkateReach = 0.2f;

    /// <summary>The x of the neutral-zone dots.</summary>
    public const float NeutralDotX = 6.1f;


    /// <summary>Gets the club with an offside pending (the linesman's arm is up), if any.</summary>
    /// <param name="world">The match state.</param>
    /// <returns>The offending club, or None.</returns>
    public static SideOrNone Pending(World world)
    {
        var s = world.Offside.Get(() => new OffsideState());
        return s.PendingEnd != 0 ? Attacker(world.Match, s.PendingEnd).OrNone() : SideOrNone.None;
    }

    /// <summary>Gets a value indicating whether a player stands in a zone with both skates past its blue line.</summary>
    /// <param name="position">The player's position.</param>
    /// <param name="end">-1 for the zone at -x, +1 for the zone at +x.</param>
    /// <returns>True when the player is wholly inside.</returns>
    public static bool PlayerInZone(Vector3 position, int end)
    {
        return end * position.X > RinkSpec.BlueLineX + LineHalfWidth + SkateReach;
    }

    /// <summary>Gets the club attacking the zone at one end.</summary>
    /// <param name="match">The match state.</param>
    /// <param name="end">-1 or +1.</param>
    /// <returns>The club shooting at that end's net.</returns>
    public static Side Attacker(MatchState match, int end)
    {
        return FaceoffFormation.DefendedEnd(match, Side.Home) == end ? Side.Away : Side.Home;
    }

    /// <summary>Gets the faceoff dot after an offside into the zone at one end: the neutral-zone dot outside it on the puck's side.</summary>
    /// <param name="end">The zone the offside was into.</param>
    /// <param name="puck">Where the puck was.</param>
    /// <returns>The dot.</returns>
    public static Vector3 FaceoffDot(int end, Vector3 puck)
    {
        return MatchFlow.NearestDot(new Vector3(end * NeutralDotX, 0, puck.Z >= 0 ? 1 : -1));
    }

    public void Update(World world, float dt)
    {
        LineCallsDemo.Update(world);
        var s = world.Offside.Get(() => new OffsideState());
        var pk = world.Puck;
        if (!MatchFlow.IsRunning(world) || world.Match.Phase != MatchPhase.Play || !pk.Live || !world.Setup.Rules.Offside)
        {
            // dead puck (or offside switched off, W44): forget any pending call, and read the zone afresh when the puck drops
            s.PendingEnd = 0;
            s.Zone = PuckZone(pk.Position, 0);
            return;
        }

        int was = s.Zone;
        s.Zone = PuckZone(pk.Position, was);
        if (s.PendingEnd != 0 && s.Zone != s.PendingEnd) s.PendingEnd = 0;   // puck left the zone
        if (s.Zone != 0 && was != s.Zone) Entered(world, s, s.Zone);
        if (s.PendingEnd != 0) Watch(world, s);
    }


    /// <summary>The puck wholly crossed into the zone at <paramref name="end"/>.</summary>
    static void Entered(World world, OffsideState s, int end)
    {
        var pk = world.Puck;
        var attacker = Attacker(world.Match, end);
        // a puck the defending club sends into its own zone (or a loose one nobody played) is never offside
        if (!SideOf(world, pk.Carrier >= 0 ? pk.Carrier : pk.LastTouch).Is(attacker)) return;
        if (!Offside(world, attacker, end, pk.Carrier)) return;
        s.PendingEnd = end;
        s.EntryTouch = pk.LastTouch;
        Watch(world, s);
    }

    /// <summary>Waves the pending offside off when everyone tagged up, or calls it when an attacker plays the puck or it is shot at the net.</summary>
    static void Watch(World world, OffsideState s)
    {
        var pk = world.Puck;
        int end = s.PendingEnd;
        var attacker = Attacker(world.Match, end);
        if (!Offside(world, attacker, end, pk.Carrier))
        {
            s.PendingEnd = 0;
            return;
        }

        // carried in with a teammate ahead counts as played at once; a pass or dump waits for an attacking touch
        bool touched = (pk.Carrier >= 0 && SideOf(world, pk.Carrier).Is(attacker))
            || (pk.LastTouch != s.EntryTouch && SideOf(world, pk.LastTouch).Is(attacker));
        if (touched || OnGoal(pk, end)) Call(world, s, end, attacker);
    }

    static void Call(World world, OffsideState s, int end, Side attacker)
    {
        var pk = world.Puck;
        var at = pk.Position;
        s.PendingEnd = 0;
        var dot = FaceoffDot(end, at);
        if (!MatchFlow.Whistle(world, dot)) return;
        world.Events.Publish(new OffsideCalled(world.Time, attacker, at));
        LineCallsDemo.Log("Offside", attacker, at, dot);
    }

    /// <summary>Gets a value indicating whether any attacker but the carrier is wholly inside the zone.</summary>
    static bool Offside(World world, Side attacker, int end, int carrier)
    {
        return world.OnIce(attacker).Any(p => !p.IsGoalie && p.Id != carrier && PlayerInZone(p.Position, end));
    }

    /// <summary>Gets a value indicating whether a loose puck is headed into the net mouth at <paramref name="end"/>.</summary>
    static bool OnGoal(Puck pk, int end)
    {
        if (pk.Carrier >= 0 || end * pk.Velocity.X < 5f) return false;
        float t = (end * RinkSpec.GoalLineX - pk.Position.X) / pk.Velocity.X;
        float z = pk.Position.Z + pk.Velocity.Z * t;
        return t > 0 && MathF.Abs(z) < RinkSpec.NetWidth / 2 + 0.3f;
    }

    /// <summary>
    /// Gets the zone the puck is in: +1 or -1 once it has wholly crossed that blue line, 0 once it is
    /// wholly back in the neutral zone; on a line it stays where it was.
    /// </summary>
    static int PuckZone(Vector3 position, int was)
    {
        float edge = RinkSpec.BlueLineX + LineHalfWidth + RinkSpec.PuckRadius;
        float inner = RinkSpec.BlueLineX - LineHalfWidth - RinkSpec.PuckRadius;
        float x = position.X;
        if (MathF.Abs(x) > edge) return Math.Sign(x);
        if (MathF.Abs(x) < inner) return 0;
        // on a blue line: the zone the puck was in, or the neutral zone if it came from the other end
        return was == Math.Sign(x) ? was : 0;
    }

    static SideOrNone SideOf(World world, int playerId)
    {
        return playerId >= 0 && playerId < world.Players.Count ? world.Players[playerId].Side.OrNone() : SideOrNone.None;
    }
}

/// <summary>Offside's own state for one world.</summary>
internal sealed class OffsideState
{
    /// <summary>Gets or sets the zone the puck is in (-1, 0, +1).</summary>
    public int Zone { get; set; }

    /// <summary>Gets or sets the zone with an offside pending, or 0.</summary>
    public int PendingEnd { get; set; }

    /// <summary>Gets or sets the last touch when the puck entered, so a new attacking touch can be told apart.</summary>
    public int EntryTouch { get; set; } = -1;

}
