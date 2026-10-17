using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Hud;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Render;
using StickAndPuck.App.Rules;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests;

/// <summary>
/// The HUD's reading of match state and events: period and clock texts, stripes, banners, the
/// special notes, the finals, the power play tag and a whole match. Formerly SAP_SCENE=hudtest.
/// </summary>
public sealed class HudTests : CheckedTest
{
    /// <summary>A match of 30-second periods, the starters on and the match flow begun.</summary>
    static World Make() => new WorldBuilder().Setup(s => s with { PeriodSeconds = 30 }).Build();

    [Test]
    public void Texts()
    {
        var w = Make();
        Check(string.Equals(HudSystem.PeriodText(w), "1", StringComparison.Ordinal), "period 1 reads 1");
        w.Match.Period = 4;
        Check(string.Equals(HudSystem.PeriodText(w), "OT", StringComparison.Ordinal), "period 4 reads OT");
        w.Match.Phase = MatchPhase.Shootout;
        Check(string.Equals(HudSystem.PeriodText(w), "SO", StringComparison.Ordinal), "shootout reads SO");
        Check(string.Equals(MatchFlow.ClockText(20 * 60), "20:00", StringComparison.Ordinal) && string.Equals(MatchFlow.ClockText(0.2f), "0:01", StringComparison.Ordinal), "clock text from W7a");
    }

    [Test]
    public void Stripes()
    {
        var w = Make();
        foreach (var side in new[] { Side.Home, Side.Away })
        {
            var c = HudSystem.StripeColour(w, side);
            bool onPalette = Palette.Current.Any(p => (int)MathF.Round(p.X * 255) == c.R && (int)MathF.Round(p.Y * 255) == c.G && (int)MathF.Round(p.Z * 255) == c.B);
            Check(onPalette, $"{side} stripe {c.R},{c.G},{c.B} is a palette colour");
        }
    }

    [Test]
    public void Banners()
    {
        var w = Make();
        var hud = new HudMessages();
        var scorer = w.OnIce(Side.Away).First(p => !p.IsGoalie);
        w.Events.Publish(new GoalScored(w.Time, Side.Away, scorer.Id, default, 0));
        hud.Update(w);
        Check(Shown(hud).TryGet(out var g) && g is { Title: "GOAL!" } && string.Equals(g.Detail, string.Create(CultureInfo.InvariantCulture, $"#{scorer.Data.Number} {scorer.Data.LastName}"), StringComparison.Ordinal) && string.Equals(g.Crest, w.Setup.Away.Id, StringComparison.Ordinal),
            "a goal shows GOAL! with the scorer and the scoring club's crest");

        var home = w.OnIce(Side.Home).First(p => !p.IsGoalie);
        w.Events.Clear();
        w.Events.Publish(new GoalScored(w.Time, Side.Away, home.Id, default, 0));
        hud.Update(w);
        Check(Shown(hud).Is(b => string.Equals(b.Detail, w.Setup.Away.Name, StringComparison.Ordinal)), "an own goal names the club, not the defender");

        w.Events.Clear();
        w.Time += HudMessages.GoalSeconds + 0.1f;
        hud.Update(w);
        Check(!Shown(hud).HasValue, "the goal banner goes away");

        w.Events.Publish(new IcingCalled(w.Time, Side.Home, home.Id, default));
        hud.Update(w);
        Check(Shown(hud).TryGet(out var ic) && ic is { Title: "ICING" } && string.Equals(ic.Detail, w.Setup.Home.Name, StringComparison.Ordinal) && string.Equals(ic.Crest, w.Setup.Home.Id, StringComparison.Ordinal),
            "icing names the club that iced the puck");

        w.Events.Clear();
        w.Events.Publish(new FightStarted(w.Time, Side.Home, home.Id, default));
        hud.Update(w);
        Check(Shown(hud).Is(b => string.Equals(b.Title, "ICING", StringComparison.Ordinal)), "events the HUD does not announce leave the banner alone");
    }

    [Test]
    public void Specials()
    {
        var w = Make();
        var hud = new HudMessages();
        var scorer = w.OnIce(Side.Home).First(p => !p.IsGoalie);
        var other = w.OnIce(Side.Away).First(p => !p.IsGoalie);
        w.Switches.SeasonGoals = (_, side) => side == Side.Home ? 0 : 4;
        void Goal(Side side, int player)
        {
            w.Events.Clear();
            w.Events.Publish(new GoalScored(w.Time, side, player, default, 0));
            w.Match.Score[(int)side]++;
            hud.Update(w);
        }

        Goal(Side.Home, scorer.Id);
        Check(Shown(hud).TryGet(out var g1) && g1 is { Title: "GOAL!" } && g1.Lines.Count == 1 && string.Equals(g1.Lines[0], "FIRST GOAL OF THE SEASON", StringComparison.Ordinal),
            "a club's first goal of the season gets a note on the goal banner");
        Goal(Side.Away, other.Id);
        Check(Shown(hud).TryGet(out var g2) && g2 is { Title: "GOAL!" } && g2.Lines.Count == 0, "a club that has scored this season gets no note");
        Goal(Side.Home, scorer.Id);
        Check(Shown(hud).Is(b => b is { Title: "GOAL!" }), "two goals are not a hat trick");
        Goal(Side.Home, scorer.Id);
        Check(Shown(hud).TryGet(out var h) && h is { Title: "HAT TRICK!" } && string.Equals(h.Detail, string.Create(CultureInfo.InvariantCulture, $"#{scorer.Data.Number} {scorer.Data.LastName}"), StringComparison.Ordinal), "the third goal by one player shows HAT TRICK!");
        Goal(Side.Home, scorer.Id);
        Check(Shown(hud).Is(b => b is { Title: "GOAL!" }), "the fourth is a plain goal again");
        w.Switches.SeasonGoals = (_, _) => Maybe<int>.None;

        // 4-1 home: the game winner is home's second goal
        Check(Final(w, hud, Side.Home).TryGet(out var f) && f is { Title: "FINAL" } && f.Lines.Count == 1 && string.Equals(f.Lines[0], $"GAME-WINNING GOAL #{scorer.Data.Number} {scorer.Data.LastName}", StringComparison.Ordinal),
            "the final names the game-winning goal and no shutout at 4-1");
    }

    [Test]
    public void Finals()
    {
        var w = Make();
        var other = w.OnIce(Side.Away).First(p => !p.IsGoalie);
        var goalie = w.OnIce(Side.Away).First(p => p.IsGoalie);
        var hud = new HudMessages();
        for (int i = 0; i < 2; i++)
        {
            w.Events.Clear();
            w.Events.Publish(new GoalScored(w.Time, Side.Away, other.Id, default, 0));
            w.Match.Score[1]++;
            hud.Update(w);
        }

        Check(Final(w, hud, Side.Away).TryGet(out var s) && s is { Title: "FINAL" } && s.Lines.Count == 2 && s.Lines[0].StartsWith("GAME-WINNING GOAL", StringComparison.Ordinal)
            && string.Equals(s.Lines[1], $"SHUTOUT #{goalie.Data.Number} {goalie.Data.LastName}", StringComparison.Ordinal),
            "a 2-0 final names the game winner and the goalie's shutout");

        // a shootout win adds a goal with no event: no game winner named
        w.Match.Score[0] = 1;
        w.Match.Score[1] = 0;
        Check(Final(w, new HudMessages(), Side.Home).TryGet(out var so) && so is { Title: "FINAL" } && so.Lines.Count == 1 && so.Lines[0].StartsWith("SHUTOUT", StringComparison.Ordinal),
            "a goal the tally never saw names no game winner");
    }

    /// <summary>Gets the banner showing now, if any.</summary>
    static Maybe<HudMessage> Shown(HudMessages hud) => hud.Current;

    static Maybe<HudMessage> Final(World w, HudMessages hud, Side winner)
    {
        w.Events.Clear();
        w.Events.Publish(new MatchWon(w.Time, winner));
        hud.Update(w);
        return Shown(hud);
    }

    [Test]
    public void PowerPlay()
    {
        var w = Make();
        w.Match.Phase = MatchPhase.Play;
        Check(HudSystem.PowerPlayText(w).Length == 0, "even strength shows no PP tag");
        var away = w.OnIce(Side.Away).First(p => !p.IsGoalie);
        w.Match.Penalties.Add(new PenaltyState(away.Id, Side.Away, "tripping", 120));
        Check(string.Equals(HudSystem.PowerPlayText(w), "PP 5V4", StringComparison.Ordinal), "one penalty reads PP 5V4");
        var home = w.OnIce(Side.Home).First(p => !p.IsGoalie);
        w.Match.Penalties.Add(new PenaltyState(home.Id, Side.Home, "hooking", 120));
        Check(HudSystem.PowerPlayText(w).Length == 0, "coincidental minors are even strength");
        var away2 = w.OnIce(Side.Away).Where(p => !p.IsGoalie).Skip(1).First();
        w.Match.Penalties.Add(new PenaltyState(away2.Id, Side.Away, "slashing", 120));
        Check(string.Equals(HudSystem.PowerPlayText(w), "PP 4V3", StringComparison.Ordinal), "two against one reads PP 4V3");
        w.Match.Phase = MatchPhase.Shootout;
        Check(HudSystem.PowerPlayText(w).Length == 0, "no PP tag in the shootout");
    }

    [Test]
    public void WholeMatch()
    {
        var w = Make();
        // physics and rules drive the match; the HUD only reads. Shoot into the away net now and then.
        var systems = new ISystem[] { new PuckPhysics(), new MatchRules() };
        var hud = new HudMessages();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        bool periodHeld = true;
        for (int i = 0; i < 400 / Game.Step && w.Match.Phase != MatchPhase.Final; i++)
        {
            w.Events.Clear();
            if (w.Match.Phase == MatchPhase.Play && w.Match.Score[0] == 0 && w.Puck.Position.X < RinkSpec.GoalLineX - 5)
            {
                int end = -FaceoffFormation.DefendedEnd(w.Match, Side.Home);
                w.Puck.Carrier = -1;
                w.Puck.Position = new(end * (RinkSpec.GoalLineX - 4), 0, 0.3f);
                w.Puck.Velocity = new(end * 25, 0, 0);
                w.Puck.LastTouch = w.OnIce(Side.Home).First(p => !p.IsGoalie).Id;
            }
            foreach (var s in systems) s.Update(w, Game.Step);
            hud.Update(w);
            if (Shown(hud).TryGet(out var m)) seen.Add(m.Title);
            if (w.Match.Phase == MatchPhase.Intermission && !Shown(hud).Is(b => b.Title.StartsWith("END OF", StringComparison.Ordinal))) periodHeld = false;
            w.Time += Game.Step;
            w.Tick++;
        }
        Check(w.Match.Phase == MatchPhase.Final, "the match reaches the final");
        Check(seen.Contains("GOAL!"), "a real goal from physics shows the goal banner");
        Check(seen.Contains("END OF PERIOD 1") && seen.Contains("END OF PERIOD 2"), "period ends show");
        Check(periodHeld, "the period banner stays up through the intermission");
        Check(Shown(hud).TryGet(out var f) && f is { Title: "FINAL", Panel: "panel_red" } && f.Detail.EndsWith(w.Setup.Away.ShortName, StringComparison.Ordinal) && string.Equals(f.Crest, w.Setup.Home.Id, StringComparison.Ordinal),
            "the final banner shows the score and the winner's crest and stays up");
    }
}
