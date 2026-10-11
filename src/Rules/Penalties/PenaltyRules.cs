using StickAndPuck.Core;
using StickAndPuck.Hud;

namespace StickAndPuck.Rules;

/// <summary>
/// W14: tripping, hooking and slashing calls, the penalty box, power play and penalty kill. Runs
/// after physics, the match rules, offside and icing, so it sees this step's checks, goals and
/// whistles. See <see cref="Penalties"/> for the rules it keeps. Line changes stay W13's: a skater
/// in the box is never sent on, so a club changes lines a skater short until he comes back.
/// Draws the penalty box under the scorebug: each penalty's number and time left, a delayed call,
/// and a club's extra attacker while its goalie is pulled (W23).
/// W23: at every faceoff the skaters on the ice are balanced to <see cref="Penalties.TargetSkaters"/>,
/// which gives 4 on 3 after a penalty in 3 on 3 overtime and sends an extra attacker off once the
/// goalie is back; a skater let out of the box in overtime plays on until that next whistle.
/// </summary>
internal sealed class PenaltyRules : ISystem, IDisposable
{
    static readonly Side[] BothSides = [Side.Home, Side.Away];

    HudArt? art;

    public void Start(World world)
    {
        if (MatchFlow.IsRunning(world)) art = HudArt.Load();
    }

    public void Update(World world, float dt)
    {
        if (!MatchFlow.IsRunning(world)) return;
        var book = Penalties.Book(world);
        var m = world.Match;
        if (m.Phase is MatchPhase.Overtime or MatchPhase.Shootout or MatchPhase.Final)
        {
            EndOfRegulation(world, book);
            book.LastPhase = m.Phase;
            return;
        }

        if (float.IsNaN(book.FirstDrop) && m.Phase == MatchPhase.Play) book.FirstDrop = world.Time;     // the referee drops the puck after this system runs
        if (m.Phase == MatchPhase.Faceoff && book.LastPhase != MatchPhase.Faceoff) AtFaceoff(world, book);
        book.LastPhase = m.Phase;
        RunClocks(world, book);
        if (m.Phase == MatchPhase.Goal) Goals(world, book);

        if (m.Phase == MatchPhase.Play)
        {
            Script(world, book);
            if (world.Settings.Penalties) Watch(world, book, dt);
            if (book.Delayed.Count > 0 && Touched(world, book) is { } offender)
            {
                MatchFlow.Whistle(world, Penalties.FaceoffDot(world, offender));
                Penalties.CallDelayed(world, book);
            }
        }
        else if (book.Delayed.Count > 0)
        {
            // play stopped by itself (offside, icing, a frozen puck, the period's end): the call is made now
            if (m.Phase == MatchPhase.Stopped) MatchFlow.State(world).Dot = Penalties.FaceoffDot(world, book.Delayed[0].Side);
            Penalties.CallDelayed(world, book);
        }
    }

    /// <summary>
    /// The faceoff is set up: the first one of a period with an empty box reads the full strength
    /// off the ice (5 in regulation, 3 or 5 in overtime); then each club's skaters are brought to
    /// <see cref="Penalties.TargetSkaters"/>, one on from the bench or one off to it, and placed again.
    /// </summary>
    static void AtFaceoff(World world, PenaltyBook book)
    {
        var m = world.Match;
        if (book.BasePeriod != m.Period && m.Penalties.Count == 0 && !Lines.GoaliePull.IsPulled(world, Side.Home) && !Lines.GoaliePull.IsPulled(world, Side.Away))
        {
            int most = BothSides.Max(side => world.OnIce(side).Count(p => !p.IsGoalie));
            book.BaseSkaters = Math.Clamp(most, PenaltyTuning.MinSkaters, PenaltyTuning.MaxSkaters);
            book.BasePeriod = m.Period;
        }

        bool changed = false;
        foreach (var side in BothSides)
        {
            int target = Penalties.TargetSkaters(world, side);
            int on = world.OnIce(side).Count(p => !p.IsGoalie);
            for (; on < target; on++)
            {
                if (Lines.Bench.SendOn(world, side, Lines.Group.Forwards, onTheFly: false) is not { } added) break;
                changed = true;
                book.Log(world, $"{Penalties.Name(world, added)} comes on, {target} skaters");
            }
            for (; on > target; on--)
            {
                if (Lines.Bench.Surplus(world, side, target) is not { } off) break;
                Lines.Bench.SendOff(world, off);
                changed = true;
                book.Log(world, $"{Penalties.Name(world, off)} goes off, {target} skaters");
            }
        }
        if (changed) FaceoffFormation.Place(world, MatchFlow.FaceoffDot(world));
    }

    /// <summary>Runs each penalty's clock by exactly what the game clock ran this step, and lets out whoever has served his time.</summary>
    static void RunClocks(World world, PenaltyBook book)
    {
        var m = world.Match;
        float ran = book.LastPeriod == m.Period && !float.IsNaN(book.LastClock) ? book.LastClock - m.Clock : 0;
        book.LastClock = m.Clock;
        book.LastPeriod = m.Period;
        if (ran <= 0) return;
        foreach (var p in m.Penalties.ToList())
        {
            float left = p.SecondsLeft - ran;
            if (left <= 0.0001f) Penalties.Release(world, book, p, "served");
            else m.Penalties[m.Penalties.IndexOf(p)] = p with { SecondsLeft = left };
        }
    }

    /// <summary>A goal against the offending club waves its delayed calls off; a power play goal ends the shortest penalty of the club scored on.</summary>
    static void Goals(World world, PenaltyBook book)
    {
        foreach (var goal in world.Events.Of(MatchEventKind.Goal))
        {
            if (goal.Side is not { } scorer) continue;
            var other = scorer == Side.Home ? Side.Away : Side.Home;
            if (book.Delayed.RemoveAll(d => d.Side == other) > 0) book.Log(world, "delayed call waved off by the goal");
            if (Penalties.Serving(world, scorer) < Penalties.Serving(world, other)
                && world.Match.Penalties.Where(p => p.Side == other).MinBy(p => p.SecondsLeft) is { } shortest)
                Penalties.Release(world, book, shortest, "power play goal");
            return;     // MatchRules counts one goal per step
        }
    }

    /// <summary>Judges every check this step and every close chase of the carrier.</summary>
    static void Watch(World world, PenaltyBook book, float dt)
    {
        foreach (var check in world.Events.Of(MatchEventKind.Check).ToList())
        {
            if (PenaltyModel.Judge(world, check) is { } foul) Roll(world, book, foul);
        }
        foreach (var foul in PenaltyModel.Chases(world, dt).ToList()) Roll(world, book, foul);
    }

    static void Roll(World world, PenaltyBook book, Foul foul)
    {
        if (book.Rng.NextSingle() < foul.Chance * Penalties.Strictness) Penalties.Signal(world, foul.PlayerId, foul.Infraction);
    }

    /// <summary>Gets the offending club once it touches the puck: it carries it, or last played it.</summary>
    static Side? Touched(World world, PenaltyBook book)
    {
        var pk = world.Puck;
        int touch = pk.Carrier >= 0 ? pk.Carrier : pk.LastTouch;
        if (touch < 0 || touch >= world.Players.Count) return null;
        var side = world.Players[touch].Side;
        return book.Delayed.Exists(d => d.Side == side) ? side : null;
    }

    /// <summary>The penalty scene's scripted call: a few seconds after the opening drop, once away has the puck, the home skater nearest it trips someone.</summary>
    static void Script(World world, PenaltyBook book)
    {
        if (float.IsNaN(book.ScriptAfterDrop) || float.IsNaN(book.FirstDrop) || world.Time - book.FirstDrop < book.ScriptAfterDrop) return;
        // wait for away to have the puck, so the call is delayed and the referee's arm shows
        var pk = world.Puck;
        if (pk.Carrier < 0 || world.Players[pk.Carrier].Side != Side.Away) return;
        book.ScriptAfterDrop = float.NaN;
        var offender = world.OnIce(Side.Home)
            .Where(p => !p.IsGoalie && p.Id != world.Puck.Carrier)
            .MinBy(p => System.Numerics.Vector3.DistanceSquared(p.Position, world.Puck.Position));
        if (offender != null) Penalties.Signal(world, offender.Id, Infraction.Tripping);
    }

    /// <summary>Penalties end with regulation (the overtime break) and before the shootout: skaters in the box go to the bench and delayed calls are dropped.</summary>
    static void EndOfRegulation(World world, PenaltyBook book)
    {
        book.Delayed.Clear();
        var m = world.Match;
        if (m.Penalties.Count == 0) return;
        foreach (var p in m.Penalties)
        {
            var player = world.Players[p.PlayerId];
            if (player.OnIce) continue;
            var door = Lines.Squad.Door(player.Side);
            player.Position = new System.Numerics.Vector3(door.X, 0, door.Y - 1.5f);
        }
        m.Penalties.Clear();
        book.Log(world, "penalties end with regulation");
    }

    public void DrawHud(World world, RenderContext render)
    {
        if (art == null || !MatchFlow.IsRunning(world)) return;
        var m = world.Match;
        if (m.Phase is MatchPhase.Intro or MatchPhase.Shootout or MatchPhase.Final) return;
        const int h = 20, pad = 3, crest = 16, gap = 4;
        int y = HudSystem.PenaltyTop;
        int x = art.BugPosition.X;
        foreach (var side in BothSides)
        {
            foreach (var p in m.Penalties.Where(p => p.Side == side).OrderBy(p => p.SecondsLeft))
            {
                string text = $"#{world.Players[p.PlayerId].Data.Number} {MatchFlow.ClockText(p.SecondsLeft)}";
                Row(art, world.Setup.Club(side).Id, text, "panel_red", x, y, h, pad, crest, gap);
                y += h + 1;
            }
        }
        foreach (var d in Penalties.Delayed(world))
        {
            Row(art, world.Setup.Club(d.Side).Id, "DELAYED", "panel", x, y, h, pad, crest, gap);
            y += h + 1;
        }
        foreach (var side in BothSides.Where(side => Lines.GoaliePull.IsPulled(world, side)))
        {
            Row(art, world.Setup.Club(side).Id, "EXTRA ATTACKER", "panel_gold", x, y, h, pad, crest, gap);
            y += h + 1;
        }
    }

    static void Row(HudArt art, string clubId, string text, string panel, int x, int y, int h, int pad, int crest, int gap)
    {
        int w = pad + crest + gap + art.Measure(text) + pad + 1;
        art.DrawPanel(panel, x, y, w, h);
        art.DrawCrest(clubId, x + pad, y + ((h - crest) / 2));
        // text y is 2 px above the 7 px capitals
        art.DrawText(text, x + pad + crest + gap, y + ((h - 7) / 2) - 2, art.Text);
    }

    public void Dispose()
    {
        art?.Unload();
        art = null;
    }
}
