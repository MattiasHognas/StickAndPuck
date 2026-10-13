using System.Numerics;
using System.Runtime.CompilerServices;
using StickAndPuck.App.Core;
using StickAndPuck.App.Render;

namespace StickAndPuck.App.Referee;

/// <summary>A penalty the referee has signalled and will call at the next stoppage (W14 decides calls).</summary>
/// <param name="Infraction">What is being called: tripping, hooking or slashing.</param>
/// <param name="PlayerId">The offending player.</param>
/// <param name="Time">When the arm went up, in <see cref="World.Time"/>.</param>
public readonly record struct RefereeCall(string Infraction, int PlayerId, float Time);

/// <summary>
/// The one referee on the ice: where it is and what it is doing. <see cref="RefereeSystem"/> moves it;
/// camera, HUD and penalties read it through <see cref="Of"/>. It lives beside the world (not in
/// <see cref="World"/>, which is Core's) and exists only in worlds that run the match flow.
/// </summary>
public sealed class Referee
{
    static readonly ConditionalWeakTable<World, Referee> All = new();

    /// <summary>The referee's outline id in the mask pass: the last id, which players (id + 1) never reach.</summary>
    public const int MaskId = MaskIds.Max;

    /// <summary>Gets or sets where the referee is.</summary>
    public Vector3 Position { get; set; } = RefereeTuning.Start;

    /// <summary>Gets or sets the skating velocity.</summary>
    public Vector3 Velocity { get; set; }

    /// <summary>Gets or sets the facing, in radians about Y like <see cref="Player.Heading"/> (0 faces the camera).</summary>
    public float Heading { get; set; }

    /// <summary>Gets the ground speed.</summary>
    public float Speed => new Vector2(Velocity.X, Velocity.Z).Length();

    /// <summary>Gets or sets how long the referee has stood set at the drop spot.</summary>
    public float SetTime { get; set; }

    /// <summary>Gets or sets how long the current faceoff has waited.</summary>
    public float Waited { get; set; }

    /// <summary>Gets or sets how long the referee holds the puck once set for the coming faceoff, rolled by <see cref="RefereeSystem"/> between <see cref="RefereeTuning.SetSeconds"/> and <see cref="RefereeTuning.MaxSetSeconds"/>; 0 until rolled.</summary>
    public float Hold { get; set; }

    /// <summary>Gets or sets how long the referee stood set before the last drop (the self test checks the spread).</summary>
    public float LastHold { get; set; }

    /// <summary>Gets or sets when the referee last dropped the puck.</summary>
    public float DroppedAt { get; set; } = float.NegativeInfinity;

    /// <summary>Gets or sets where the referee stood for the last drop.</summary>
    public Vector3 DropPoint { get; set; }

    /// <summary>Gets or sets how many faceoffs dropped by the safety net instead of the referee at the dot.</summary>
    public int LateDrops { get; set; }

    /// <summary>
    /// Gets the penalty signalled and not yet called, if any. W14 raises it with <see cref="Signal"/>
    /// when it sees tripping, hooking or slashing; the referee clears it at the next whistle or
    /// faceoff. A2 adds the arm-up and call clips the renderer will play for it.
    /// </summary>
    public RefereeCall? Call { get; private set; }

    /// <summary>Gets the referee of a world.</summary>
    /// <param name="world">The match state.</param>
    /// <returns>The referee, made on first use.</returns>
    public static Referee Of(World world)
    {
        return All.GetValue(world, _ => new Referee());
    }

    /// <summary>W14's hook: the referee raises an arm for a penalty, to be called at the next stoppage.</summary>
    /// <param name="world">The match state.</param>
    /// <param name="infraction">What is being called.</param>
    /// <param name="playerId">The offending player.</param>
    public static void Signal(World world, string infraction, int playerId)
    {
        var r = Of(world);
        r.Call ??= new RefereeCall(infraction, playerId, world.Time);
    }

    /// <summary>Gets the penalty the referee last called at a stoppage; the renderer shows its signal for a moment (W14).</summary>
    public RefereeCall? Announced { get; private set; }

    /// <summary>W14's hook: the referee calls a penalty at the stoppage and shows its signal.</summary>
    /// <param name="world">The match state.</param>
    /// <param name="infraction">What is called: tripping, hooking or slashing.</param>
    /// <param name="playerId">The penalised player.</param>
    public static void Announce(World world, string infraction, int playerId)
    {
        Of(world).Announced = new RefereeCall(infraction, playerId, world.Time);
    }

    /// <summary>Clears the signalled call once play has stopped.</summary>
    internal void ClearCall()
    {
        Call = null;
    }

    /// <summary>Gets the spot the referee drops the puck from: just behind the dot, toward the far boards.</summary>
    /// <param name="dot">The faceoff dot.</param>
    /// <returns>Where to stand.</returns>
    public static Vector3 DropSpot(Vector3 dot)
    {
        return new Vector3(dot.X, 0, dot.Z - RefereeTuning.DropOffset);
    }
}
