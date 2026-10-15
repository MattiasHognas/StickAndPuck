using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.AI;

/// <summary>The AI layers that write a skater's command, in the order they run each step.</summary>
public enum AiLayer
{
    /// <summary>Nobody has written this skater's command yet.</summary>
    None,

    /// <summary>Positioning: the base move every step.</summary>
    Positioning,

    /// <summary>Defence: contain, stay home, mark.</summary>
    Defence,

    /// <summary>Special teams: power play and kill shapes, power play shots, kill clears.</summary>
    SpecialTeams,

    /// <summary>Club tactics: forecheck, trap, dump and chase.</summary>
    Tactics,

    /// <summary>Decisions: shots, passes, checks and the skating that goes with them.</summary>
    Decisions,
}

/// <summary>Who last wrote an AI skater's command, why, and on which step: for debug traces and tests.</summary>
public readonly record struct AiIntent(AiLayer Owner, string Reason, long Tick);

/// <summary>The one way the AI writes a skater's <see cref="PlayerCommand"/>, so who owns which field
/// and which write wins is written down here rather than spread over five systems.
/// <para>Who owns what, per field:</para>
/// <list type="bullet">
/// <item>Every field of a human's command (<see cref="Player.Slot"/> 0 or more) is the input
/// system's. Every write here leaves a human's command alone, so the AI never overwrites one.</item>
/// <item>Move and Sprint: <see cref="AiLayer.Positioning"/> writes them first every step
/// (<see cref="Base"/>, which also clears every button left from the step before), then
/// <see cref="AiLayer.Defence"/>, <see cref="AiLayer.SpecialTeams"/>, <see cref="AiLayer.Tactics"/>
/// and <see cref="AiLayer.Decisions"/> may replace them in that order (<see cref="Steer"/> and
/// <see cref="Take"/>): the last layer to run wins.</item>
/// <item>Shoot and the shot's aim in Move: a shot is committed with <see cref="Shoot"/>, by
/// decisions or, for a power play shot, a kill clear or a dump, by special teams and tactics. From
/// then on decisions hold it (HoldShot) until the puck leaves the stick, and no layer starts
/// another action for that skater (<see cref="Committed"/>).</item>
/// <item>Pass, Check and Deke: decisions only (<see cref="Act"/>). Decisions also hold Shoot for a
/// receiver one-timing a pass, before the puck reaches him.</item>
/// <item>SwitchPlayer, ChangeLine, Punch, Block, AcceptFight, Pause: never the AI's.</item>
/// </list>
/// <para>Merge rules, in one place:</para>
/// <list type="number">
/// <item>Taking a skater over (<see cref="Take"/>) sets positioning's job to
/// <see cref="Job.Requested"/> with the layer's target, so later layers and decisions see he is
/// spoken for; decisions only hit with a skater still on positioning's <see cref="Job.Pressure"/>,
/// so a defender the defence has containing never finishes a check, one it lets engage may.</item>
/// <item>A committed shot beats everything: decisions hold it first, before any pass or look.</item>
/// <item>A delayed (fumbled) pass goes before the carrier's next look, unless special teams or
/// tactics claim the carrier's next action first (<see cref="Hold"/>), which drops it.</item>
/// <item>A kill clear or a dump claims the carrier with <see cref="Hold"/> while it waits for its
/// moment, then commits its shot; decisions take no shot or pass of their own meanwhile, and do
/// not hold him up at the blue line either (<see cref="Held"/>), so a dump taken wide stays wide.</item>
/// <item>A pass meant for an AI skater has him skate onto its line; reading a one-timer, decisions
/// also hold shoot until the puck arrives (puck handling strikes it) or the one-timer window is all
/// but over, so a late pass is taken rather than swung at.</item>
/// </list>
/// SAP_AI_TRACE=1 prints each change of who has an AI skater at the end of a step, one line per change.</summary>
public static class AiCommand
{



    /// <summary>Gets who last wrote a skater's command, and why.</summary>
    public static AiIntent IntentOf(World world, Player p)
    {
        return world.AiIntents.Get(() => []).GetValueOrDefault(p.Id, new AiIntent(AiLayer.None, "", long.MinValue));
    }

    /// <summary>Positioning's write at the start of a step: the whole command, every button cleared.</summary>
    internal static void Base(World world, Player p, PlayerCommand move, string reason)
    {
        if (p.Slot >= 0) return;
        if (world.Switches.AiTrace) Print(world, p);
        Owns(world, p, AiLayer.Positioning, reason);
        p.Command = move;
    }

    /// <summary>A later layer replaces the move: Move and Sprint only.</summary>
    internal static void Steer(World world, Player p, AiLayer layer, PlayerCommand move, string reason)
    {
        if (!Owns(world, p, layer, reason)) return;
        p.Command = p.Command with { Move = move.Move, Sprint = move.Sprint };
    }

    /// <summary>A layer takes a skater over from positioning: job requested, its target, and the move there.</summary>
    internal static void Take(World world, Player p, AiLayer layer, Vector2 target, PlayerCommand move, string reason)
    {
        if (p.Slot >= 0) return;
        var plan = Positioning.Board(world).For(p.Id);
        plan.Job = Job.Requested;
        plan.Target = target;
        Steer(world, p, layer, move, reason);
    }

    /// <summary>Commits a shot: decisions hold shoot for the given steps (0 is a wrist shot let go at once), the stick on the aim.</summary>
    internal static void Shoot(World world, Player p, AiLayer layer, Vector2 aim, int steps, string reason)
    {
        if (!Owns(world, p, layer, reason)) return;
        var brain = Decisions.Board(world).For(p.Id);
        brain.ShootAim = aim;
        brain.ShootSteps = steps;
        p.Command = p.Command with { Move = aim, Shoot = true, Sprint = false, Pass = false };
    }

    /// <summary>Decisions write buttons (pass, check, a held shot) on top of the move.</summary>
    internal static void Act(World world, Player p, PlayerCommand command, string reason)
    {
        if (!Owns(world, p, AiLayer.Decisions, reason)) return;
        p.Command = command;
    }

    /// <summary>Claims a carrier's next action for another layer: decisions take no look for a moment and drop a delayed pass.</summary>
    internal static void Hold(World world, Player p)
    {
        var brain = Decisions.Board(world).For(p.Id);
        brain.NextThink = Math.Max(brain.NextThink, world.Time + 0.2f);
        brain.LatePass = Maybe<PassOption>.None;
        brain.HeldTick = world.Tick;
    }

    /// <summary>Gets whether special teams or tactics claimed this carrier's next action this step (<see cref="Hold"/>).</summary>
    internal static bool Held(World world, Player p)
    {
        return Decisions.Board(world).For(p.Id).HeldTick == world.Tick;
    }

    /// <summary>Gets whether a skater is in the middle of something no layer may start over: a held shot or an action under way.</summary>
    internal static bool Committed(World world, Player p)
    {
        return Decisions.Board(world).For(p.Id).ShootSteps > 0 || p.Action != PlayerAction.None;
    }

    /// <summary>Records the writer; false for a human, whose command the AI never writes.</summary>
    static bool Owns(World world, Player p, AiLayer layer, string reason)
    {
        if (p.Slot >= 0) return false;
        var intent = new AiIntent(layer, reason, world.Tick);
        world.AiIntents.Get(() => [])[p.Id] = intent;
        world.Switches.AiWatch(p, intent);
        return true;
    }

    /// <summary>Prints who had a skater at the end of the last step he was written, when that changed.</summary>
    static void Print(World world, Player p)
    {
        if (!world.AiIntents.Get(() => []).TryGetValue(p.Id, out var now)) return;
        var printed = world.AiIntentsPrinted.Get(() => []);
        if (printed.TryGetValue(p.Id, out var was) && was.Owner == now.Owner && string.Equals(was.Reason, now.Reason, StringComparison.Ordinal)) return;
        printed[p.Id] = now;
        Console.WriteLine($"AI {now.Tick} #{p.Id} {p.Side} {now.Owner}: {now.Reason}");
    }
}
