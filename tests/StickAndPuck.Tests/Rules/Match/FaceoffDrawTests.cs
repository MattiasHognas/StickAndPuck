using StickAndPuck.App.Core;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Rules;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests;

/// <summary>The timed faceoff draw: the first centre to press after the drop wins, a press before it is a false start,
/// the AI presses later with less hockey IQ, the roll decides when nobody presses, and AI against AI stays even.</summary>
public sealed class FaceoffDrawTests : CheckedTest
{
    /// <summary>Runs physics and rules step by step, calling <paramref name="each"/> before each step; returns true when <paramref name="until"/> held.</summary>
    static bool Run(World world, Func<World, bool> until, Action<World> each, float limit = 5)
    {
        var systems = new ISystem[] { new PuckPhysics(), new MatchRules() };
        for (int i = 0; i < limit / Game.Step; i++)
        {
            if (until(world)) return true;
            world.Events.Clear();
            each(world);
            foreach (var s in systems) s.Update(world, Game.Step);
            world.Time += Game.Step; world.Tick++;
        }
        return until(world);
    }

    static bool Run(World world, Func<World, bool> until, float limit = 5) => Run(world, until, _ => { }, limit);

    static DrawState Draw(World world) => MatchFlow.State(world).Draw;

    static void Press(World world, int slot, bool down)
    {
        if (!world.Controlled(slot).TryGet(out var p)) return;
        var c = p.Command;
        c.Shoot = down;
        p.Command = c;
    }

    /// <summary>Plays one faceoff at centre from its set-up to the won draw and gives the winner.</summary>
    static SideOrNone OneDraw(World world)
    {
        MatchFlow.SetUpFaceoff(world, RinkSpec.CentreDot);
        Run(world, x => x.Match.Phase == MatchPhase.Play);
        return Draw(world).Winner;
    }

    static ClubData WithIq(ClubData club, int iq)
    {
        return club with { Roster = [.. club.Roster.Select(p => p with { Traits = p.Traits with { HockeyIq = iq } })] };
    }

    /// <summary>The human, on the left wing, is moved onto the centre and wins by pressing shoot just after the drop.</summary>
    [Test]
    public void TimedWin()
    {
        var w = new WorldBuilder().Human(PlayingPosition.LW).Build();
        Check(Run(w, x => x.Match.Phase == MatchPhase.Faceoff), "the opening faceoff");
        Run(w, _ => false, 0.1f);
        Check(w.Controlled(0).Is(p => p.Data.Position == PlayingPosition.C) && MatchFlow.FaceoffPrompt(w), "the human takes the draw with the centre and the prompt shows");
        Check(Run(w, x => Draw(x).Dropped), "the puck drops");
        Check(w.Match.Phase == MatchPhase.Faceoff && !w.Puck.Live, "the puck lies dead while the window is open");
        Check(Run(w, x => x.Match.Phase == MatchPhase.Play, x => Press(x, 0, true), 1), "a press decides the draw");
        Check(Draw(w).Winner == SideOrNone.Home && w.Puck.Live && !MatchFlow.FaceoffPrompt(w), "the human wins it and play starts");
        var centre = w.Controlled(0).Or(w.Players[0]);
        Check(System.Numerics.Vector3.Dot(w.Puck.Velocity, centre.Position - RinkSpec.CentreDot) > 0, "the puck slides back towards the human's centre");
    }

    /// <summary>Pressing before the drop is a false start: the other club wins the moment the puck drops.</summary>
    [Test]
    public void FalseStart()
    {
        var w = new WorldBuilder().Human().Build();
        Check(Run(w, x => x.Match.Phase == MatchPhase.Faceoff), "the opening faceoff");
        Run(w, _ => false, x => Press(x, 0, x.Match.Phase == MatchPhase.Faceoff && x.Time < 2.3f), 0.2f);
        Check(Draw(w).FalseStart == SideOrNone.Home && !Draw(w).Dropped, "an early press is a false start");
        Check(Run(w, x => Draw(x).Dropped, x => Press(x, 0, false)), "the puck drops");
        Check(w.Match.Phase == MatchPhase.Play && Draw(w).Winner == SideOrNone.Away, "the other club wins at the drop");
    }

    /// <summary>A held button carried into the faceoff is not a press, so it is no false start.</summary>
    [Test]
    public void HeldIsNoPress()
    {
        var w = new WorldBuilder().Human().Build();
        Press(w, 0, true);
        Check(Run(w, x => Draw(x).Dropped, x => Press(x, 0, true), 6), "the puck drops");
        Check(!Draw(w).FalseStart.IsSet(), "holding shoot from before the faceoff is no false start");
    }

    /// <summary>The AI presses sooner with more hockey IQ, and a sharper AI centre wins most draws.</summary>
    [Test]
    public void AiReactionByIq()
    {
        Check(FaceoffDraw.Reaction(0.9f, 0.5f) < FaceoffDraw.Reaction(0.5f, 0.5f) && FaceoffDraw.Reaction(0.5f, 0.5f) < FaceoffDraw.Reaction(0.2f, 0.5f), "higher hockey IQ reacts faster");
        Check(FaceoffDraw.Reaction(1f, 0f) >= MatchTuning.DrawReaction && FaceoffDraw.Reaction(0.7f, 0f) < MatchTuning.DrawWindow, "a fair AI presses inside the window");
        var w = new WorldBuilder().Setup(s => s with { Home = WithIq(s.Home, 90), Away = WithIq(s.Away, 30) }).Build();
        int home = 0;
        const int draws = 100;
        for (int i = 0; i < draws; i++) if (OneDraw(w) == SideOrNone.Home) home++;
        Check(home > draws * 0.75, $"the sharper centre wins most draws ({home} of {draws})");
    }

    /// <summary>When nobody presses (two humans who wait) the window closes and the roll decides.</summary>
    [Test]
    public void TieBreak()
    {
        var w = new WorldBuilder().Human().Setup(s => s with { Inputs = [new InputSlot(InputDevice.KeyboardMouse, Side.Home), new InputSlot(InputDevice.Gamepad1, Side.Away)] }).Build();
        w.OnIce(Side.Away).First(p => p.Data.Position == PlayingPosition.C).Slot = 1;
        Check(Run(w, x => Draw(x).Dropped), "the puck drops");
        float dropped = w.Time;
        Check(Run(w, x => x.Match.Phase == MatchPhase.Play, 1), "the draw is decided");
        Check(w.Time - dropped >= MatchTuning.DrawWindow - Game.Step, "only once the window closes");
        Check(Draw(w).Winner.IsSet(), "the roll picks a winner");
    }

    /// <summary>AI against AI the draws stay about even, with the clubs played both ways round.</summary>
    [Test]
    public void AllAiEven()
    {
        int home = 0, total = 0;
        foreach (bool swap in new[] { false, true })
        {
            var w = new WorldBuilder().Setup(s => swap ? s with { Home = s.Away, Away = s.Home } : s).Build();
            for (int i = 0; i < 200; i++, total++) if (OneDraw(w) == SideOrNone.Home) home++;
        }
        float share = home / (float)total;
        Check(share is > 0.4f and < 0.6f, $"the home share is about half ({share:0.00})");
    }
}
