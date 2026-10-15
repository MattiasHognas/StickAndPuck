using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Rules;

/// <summary>The draw at a faceoff. Each human controls their club's centre. When the puck drops a window of
/// <see cref="MatchTuning.DrawWindow"/> opens and the first centre to press shoot or pass in it wins the draw.
/// A human press before the drop is a false start and hands the draw to the other club. An AI centre presses
/// after a delay set by its hockey IQ, with a little luck. If nobody presses in the window, puck control
/// and hockey IQ decide it on a roll. Until the draw is won the puck lies dead on the dot and the
/// phase stays <see cref="MatchPhase.Faceoff"/>; then play starts and the puck slides back to the winner.</summary>
internal static class FaceoffDraw
{
    /// <summary>Clears the draw for a new faceoff; a button already held as it begins is not a press.</summary>
    public static void Reset(World world, FlowState s)
    {
        var d = s.Draw;
        d.Dropped = false;
        d.Since = 0;
        d.FalseStart = SideOrNone.None;
        for (int slot = 0; slot < d.Down.Length; slot++)
            d.Down[slot] = world.Controlled(slot).Is(p => Pressing(p.Command));
    }

    /// <summary>One step of a faceoff: humans onto their centres, presses read, and the draw won once the window decides it.</summary>
    public static void Step(World world, FlowState s, float dt)
    {
        ControlCentres(world, s.Dot);
        var pressed = Presses(world, s);
        var d = s.Draw;
        if (!d.Dropped)
        {
            if (!d.FalseStart.IsSet() && pressed.Count > 0) d.FalseStart = Pick(world, s, pressed).OrNone();
            return;
        }

        d.Since += dt;
        // a human press this step beats an AI reaching its delay this step: the press came first within it
        if (pressed.Count > 0) { Win(world, s, Pick(world, s, pressed)); return; }
        var react = new[] { Side.Home, Side.Away }.Where(side => d.Delay[(int)side] <= d.Since).ToList();
        if (react.Count > 0) { Win(world, s, react.MinBy(side => d.Delay[(int)side])); return; }
        if (d.Since >= MatchTuning.DrawWindow) Win(world, s, Roll(world, s));
    }

    /// <summary>The puck drops: the window opens and each AI centre rolls its reaction. After a false start the other club wins at once.</summary>
    public static void Drop(World world, FlowState s)
    {
        var d = s.Draw;
        d.Dropped = true;
        d.Since = 0;
        foreach (var side in new[] { Side.Home, Side.Away })
        {
            float luck = s.Dice.NextSingle();
            bool ai = Centre(world, s.Dot, side).Is(p => p.Slot < 0);
            d.Delay[(int)side] = ai ? Reaction(Centre(world, s.Dot, side).Select(p => p.Data.Traits.Iq).Or(0f), luck) : float.PositiveInfinity;
        }
        if (d.FalseStart.TryGetSide(out var early)) Win(world, s, Other(early));
    }

    /// <summary>Gets how long after the drop an AI centre presses: quicker with more hockey IQ (0..1), luck (0..1) adding up to <see cref="MatchTuning.DrawJitter"/>.</summary>
    public static float Reaction(float iq, float luck)
    {
        return MatchTuning.DrawReaction + (MatchTuning.DrawSlowness * (1f - Math.Clamp(iq, 0f, 1f))) + (MatchTuning.DrawJitter * luck);
    }

    /// <summary>Gets the skater of a club nearest the dot: the one taking the draw.</summary>
    public static Maybe<Player> Centre(World world, Vector3 dot, Side side)
    {
        return world.OnIce(side).Where(p => !p.IsGoalie).MinBy(p => Vector3.DistanceSquared(p.Position, dot)) is { } c ? c : Maybe<Player>.None;
    }

    /// <summary>Gets a value indicating whether a human takes this faceoff's draw, so the prompt shows.</summary>
    public static bool HumanCentre(World world, FlowState s)
    {
        return Centre(world, s.Dot, Side.Home).Is(p => p.Slot >= 0) || Centre(world, s.Dot, Side.Away).Is(p => p.Slot >= 0);
    }

    static bool Pressing(PlayerCommand c) => c.Shoot || c.Pass;

    /// <summary>Moves each human slot that controls a skater onto its club's centre.</summary>
    static void ControlCentres(World world, Vector3 dot)
    {
        for (int slot = 0; slot < MatchSetup.Slots; slot++)
        {
            if (!world.Controlled(slot).TryGet(out var me)) continue;
            if (!Centre(world, dot, me.Side).TryGet(out var centre) || centre == me || centre.Slot >= 0) continue;
            var carried = me.Command;
            me.Slot = -1;
            me.Command = default;
            centre.Command = carried;
            centre.Slot = slot;
        }
    }

    /// <summary>Gets the clubs whose human centre pressed shoot or pass this step.</summary>
    static List<Side> Presses(World world, FlowState s)
    {
        var d = s.Draw;
        var sides = new List<Side>();
        for (int slot = 0; slot < d.Down.Length; slot++)
        {
            bool was = d.Down[slot];
            var controlled = world.Controlled(slot);
            bool down = controlled.Is(c => Pressing(c.Command));
            d.Down[slot] = down;
            if (!controlled.TryGet(out var p)) continue;
            if (down && !was && Centre(world, s.Dot, p.Side) == p && !sides.Contains(p.Side)) sides.Add(p.Side);
        }
        return sides;
    }

    /// <summary>Gets the one club of those that pressed in the same step; two at once go to the roll.</summary>
    static Side Pick(World world, FlowState s, List<Side> sides)
    {
        return sides.Count == 1 ? sides[0] : Roll(world, s);
    }

    /// <summary>The roll: the two centres contest it on puck control and hockey IQ with some luck.</summary>
    static Side Roll(World world, FlowState s)
    {
        static float Skill(Maybe<Player> p) => p.Select(c => 0.5f + Traits.Unit(c.Data.Traits.PuckControl) + (0.5f * c.Data.Traits.Iq)).Or(0f);
        float h = Skill(Centre(world, s.Dot, Side.Home)), a = Skill(Centre(world, s.Dot, Side.Away));
        return s.Dice.NextSingle() * (h + a) < h ? Side.Home : Side.Away;
    }

    /// <summary>The draw is won: play starts and the puck is pulled back towards the winner's blade.</summary>
    static void Win(World world, FlowState s, Side winner)
    {
        world.Match.Phase = MatchPhase.Play;
        var pk = world.Puck;
        pk.Position = s.Dot;
        pk.Velocity = Vector3.Zero;
        pk.Live = true;
        s.Draw.Winner = winner.OrNone();
        s.Draw.Dropped = false;
        if (!Centre(world, s.Dot, winner).TryGet(out var centre)) return;
        var pull = centre.Position + ((centre.Position - s.Dot) * 0.5f) - s.Dot;
        pull.Y = 0;
        if (pull.LengthSquared() > 1e-6f) pk.Velocity = Vector3.Normalize(pull) * MatchTuning.DrawSpeed;
    }

    static Side Other(Side side) => side == Side.Home ? Side.Away : Side.Home;
}

/// <summary>The draw of the current faceoff.</summary>
internal sealed class DrawState
{
    /// <summary>Gets or sets a value indicating whether the puck has dropped and the window is open (false again once the draw is won).</summary>
    public bool Dropped { get; set; }

    /// <summary>Gets or sets the seconds since the drop.</summary>
    public float Since { get; set; }

    /// <summary>Gets or sets the club whose centre pressed before the drop, if any.</summary>
    public SideOrNone FalseStart { get; set; }

    /// <summary>Gets or sets the club that won the last draw.</summary>
    public SideOrNone Winner { get; set; }

    /// <summary>Gets each club's AI reaction after the drop, by side; infinite for a human centre.</summary>
    public float[] Delay { get; } = [float.PositiveInfinity, float.PositiveInfinity];

    /// <summary>Gets whether each human slot held shoot or pass last step, so only a fresh press counts.</summary>
    public bool[] Down { get; } = new bool[MatchSetup.Slots];
}
