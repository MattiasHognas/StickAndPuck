using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules;

namespace StickAndPuck.App.Pregame;

/// <summary>
/// W28: the pre-game warm-up other code can steer. A match that someone will play (a world with a
/// human slot, started with <see cref="MatchFlow.Begin"/>) holds its intro while both squads skate
/// around, until start is pressed; then the opening faceoff follows as before. A world without a
/// human, and every self test, runs the plain two-second intro. SAP_WARMUP=0 turns the warm-up off;
/// SAP_WARMUP=seconds starts the game by itself after that long, for scripted runs and captures.
/// </summary>
public static class Pregame
{

    /// <summary>Gets a value indicating whether SAP_WARMUP=0 turned the warm-up off.</summary>
    public static bool Disabled => Env.Is("SAP_WARMUP", "0");

    /// <summary>Gets the seconds after which the warm-up starts the game by itself (SAP_WARMUP=seconds), or infinity.</summary>
    public static float AutoStartSeconds =>
        float.TryParse(Env.Get("SAP_WARMUP"), NumberStyles.Float, CultureInfo.InvariantCulture, out float s) && s > 0
            ? s
            : float.PositiveInfinity;

    /// <summary>Asks for the warm-up in a world even without a human, or refuses it in one with a human (<paramref name="on"/> false).</summary>
    /// <param name="world">The match state.</param>
    /// <param name="on">Whether the intro waits for start.</param>
    public static void Enable(World world, bool on = true)
    {
        State(world).Forced = on;
    }

    /// <summary>Gets a value indicating whether the warm-up is on right now: the intro is being held for the start button.</summary>
    /// <param name="world">The match state.</param>
    /// <returns>True while the squads warm up.</returns>
    public static bool IsWarmingUp(World world)
    {
        return world.Warmup.TryGet(out var s) && s.Running && !s.Started;
    }

    /// <summary>Gets the seconds the warm-up has run.</summary>
    /// <param name="world">The match state.</param>
    /// <returns>The time, 0 before or without a warm-up.</returns>
    public static float Elapsed(World world)
    {
        return world.Warmup.TryGet(out var s) ? s.Elapsed : 0;
    }

    /// <summary>Start pressed: the warm-up ends, the skaters gather and the opening faceoff follows.</summary>
    /// <param name="world">The match state.</param>
    /// <returns>True if a warm-up was running.</returns>
    public static bool Start(World world)
    {
        if (!IsWarmingUp(world)) return false;
        State(world).Started = true;
        Console.WriteLine($"SAP: warm-up over after {State(world).Elapsed:0.0} s, the game starts");
        return true;
    }

    /// <summary>Whether a world wants the warm-up: a match flow with someone to press start, unless told otherwise.</summary>
    internal static bool Wanted(World world)
    {
        if (Disabled || !MatchFlow.IsRunning(world)) return false;
        var s = State(world);
        return s.Forced.TryGet(out bool forced) ? forced : world.Players.Exists(p => p.Slot >= 0);
    }

    internal static WarmupState State(World world)
    {
        return world.Warmup.Get(() => new WarmupState());
    }
}

/// <summary>The warm-up's own state for one world.</summary>
internal sealed class WarmupState
{
    /// <summary>Gets or sets the explicit choice from <see cref="Pregame.Enable"/>, or None to decide from the human slots.</summary>
    public Maybe<bool> Forced { get; set; }

    /// <summary>Gets or sets a value indicating whether the warm-up has begun in this world's intro.</summary>
    public bool Running { get; set; }

    /// <summary>Gets or sets a value indicating whether start was pressed.</summary>
    public bool Started { get; set; }

    /// <summary>Gets or sets the seconds since the warm-up began.</summary>
    public float Elapsed { get; set; }

    /// <summary>Gets or sets a value indicating whether every start button has been seen released, so a press held over from a menu does not count.</summary>
    public bool Armed { get; set; }

    /// <summary>Gets the route of each skater, by player id.</summary>
    public Dictionary<int, WarmupRoute> Routes { get; } = [];
}
