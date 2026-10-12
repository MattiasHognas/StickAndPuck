using StickAndPuck.Core;
using StickAndPuck.Physics;
using StickAndPuck.Render;
using StickAndPuck.Rules;

namespace StickAndPuck.Hud;

/// <summary>
/// SAP_SCENE=hud: both starting lineups with the scorebug at 2-1 in the second period and one
/// banner up. SAP_HUD_MESSAGE picks it: goal (default), period, final, tie, offside, icing, intro,
/// hattrick, firstgoal, shutout, gwg (final with the game-winning goal), pp (a power play, no banner)
/// or none. For screenshots of the HUD without waiting for a match to get there.
/// </summary>
public sealed class HudScene : IScene
{
    /// <inheritdoc/>
    public string Name => "hud";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var world = new World(Scenes.DefaultSetup(clubs), settings);
        Scenes.AddStarters(world);
        var m = world.Match;
        m.Score[0] = 2;
        m.Score[1] = 1;
        m.Period = 2;
        m.Clock = 734.4f;
        HudSystem.ShowAlways = true;
        HudDemo.Message = Environment.GetEnvironmentVariable("SAP_HUD_MESSAGE") is { Length: > 0 } s ? s : "goal";
        if (string.Equals(HudDemo.Message, "intro", StringComparison.Ordinal)) m.Phase = MatchPhase.Intro;
        if (string.Equals(HudDemo.Message, "shutout", StringComparison.Ordinal)) m.Score[1] = 0;
        if (string.Equals(HudDemo.Message, "firstgoal", StringComparison.Ordinal))
        {
            m.Score[0] = 0;
            HudMessages.SeasonGoals = (_, _) => 0;    // as if the club had not scored yet this season
        }
        if (string.Equals(HudDemo.Message, "pp", StringComparison.Ordinal) && world.OnIce(Side.Away).FirstOrDefault(p => !p.IsGoalie) is { } boxed)
            m.Penalties.Add(new PenaltyState(boxed.Id, Side.Away, "tripping", 74));
        return world;
    }
}

/// <summary>Publishes the hud scene's one event on the first step.</summary>
internal static class HudDemo
{
    /// <summary>Gets or sets the banner the hud scene shows, or null outside it.</summary>
    public static string? Message { get; set; }

    /// <summary>Publishes the chosen event on the first step.</summary>
    /// <param name="world">The match state.</param>
    public static void Update(World world)
    {
        if (Message == null || world.Tick != 0) return;
        var scorer = world.OnIce(Side.Home).FirstOrDefault(p => !p.IsGoalie);
        var other = world.OnIce(Side.Away).FirstOrDefault(p => !p.IsGoalie);
        if (string.Equals(Message, "hattrick", StringComparison.Ordinal) || string.Equals(Message, "gwg", StringComparison.Ordinal))
        {
            // the earlier goals of the match, so the tally knows them: two by the scorer, or home, away, home
            bool hat = string.Equals(Message, "hattrick", StringComparison.Ordinal);
            world.Events.Publish(new(MatchEventKind.Goal, world.Time, Side.Home, scorer?.Id ?? -1));
            world.Events.Publish(new(MatchEventKind.Goal, world.Time, hat ? Side.Home : Side.Away, hat ? scorer?.Id ?? -1 : other?.Id ?? -1));
            world.Events.Publish(new(MatchEventKind.Goal, world.Time, Side.Home, scorer?.Id ?? -1));
            if (hat) world.Match.Score[0] = 3;
            if (!hat) world.Events.Publish(new(MatchEventKind.MatchEnd, world.Time, Side.Home));
            return;
        }

        MatchEvent? e = Message switch
        {
            "goal" or "firstgoal" => new(MatchEventKind.Goal, world.Time, Side.Home, scorer?.Id ?? -1),
            "shutout" => new(MatchEventKind.MatchEnd, world.Time, Side.Home),
            "period" => new(MatchEventKind.PeriodEnd, world.Time, Strength: 2),
            "final" => new(MatchEventKind.MatchEnd, world.Time, Side.Home),
            "tie" => new(MatchEventKind.MatchEnd, world.Time),
            "offside" => new(MatchEventKind.Offside, world.Time),
            "icing" => new(MatchEventKind.Icing, world.Time, Side.Away, other?.Id ?? -1),
            _ => null,
        };
        if (e is { } ev) world.Events.Publish(ev);
    }
}

/// <summary>
/// SAP_SCENE=hudtest: checks the HUD's reading of match state and events without a window,
/// prints one line per check and exits with 0 when all pass, 1 otherwise.
/// </summary>
public sealed class HudSelfTest : IScene
{
    int failures;

    /// <inheritdoc/>
    public string Name => "hudtest";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var setup = Scenes.DefaultSetup(clubs) with { PeriodSeconds = 30 };
        World Make()
        {
            var w = new World(setup, settings);
            Scenes.AddStarters(w);
            MatchFlow.Begin(w, log: false);
            return w;
        }

        Texts(Make());
        Stripes(Make());
        Banners(Make());
        Specials(Make());
        Finals(Make());
        PowerPlay(Make());
        WholeMatch(Make());

        Console.WriteLine(failures == 0 ? "HUDTEST: all checks passed" : $"HUDTEST: {failures} check(s) failed");
        Environment.Exit(failures == 0 ? 0 : 1);
        return Make();
    }

    void Check(bool ok, string what)
    {
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {what}");
        if (!ok) failures++;
    }

    void Texts(World w)
    {
        Check(string.Equals(HudSystem.PeriodText(w), "1", StringComparison.Ordinal), "period 1 reads 1");
        w.Match.Period = 4;
        Check(string.Equals(HudSystem.PeriodText(w), "OT", StringComparison.Ordinal), "period 4 reads OT");
        w.Match.Phase = MatchPhase.Shootout;
        Check(string.Equals(HudSystem.PeriodText(w), "SO", StringComparison.Ordinal), "shootout reads SO");
        Check(string.Equals(MatchFlow.ClockText(20 * 60), "20:00", StringComparison.Ordinal) && string.Equals(MatchFlow.ClockText(0.2f), "0:01", StringComparison.Ordinal), "clock text from W7a");
    }

    void Stripes(World w)
    {
        foreach (var side in new[] { Side.Home, Side.Away })
        {
            var c = HudSystem.StripeColour(w, side);
            bool onPalette = Palette.Current.Any(p => (int)MathF.Round(p.X * 255) == c.R && (int)MathF.Round(p.Y * 255) == c.G && (int)MathF.Round(p.Z * 255) == c.B);
            Check(onPalette, $"{side} stripe {c.R},{c.G},{c.B} is a palette colour");
        }
    }

    void Banners(World w)
    {
        var hud = new HudMessages();
        var scorer = w.OnIce(Side.Away).First(p => !p.IsGoalie);
        w.Events.Publish(new MatchEvent(MatchEventKind.Goal, w.Time, Side.Away, scorer.Id));
        hud.Update(w);
        Check(hud.Current is { Title: "GOAL!" } g && string.Equals(g.Detail, $"#{scorer.Data.Number} {scorer.Data.LastName}", StringComparison.Ordinal) && string.Equals(g.Crest, w.Setup.Away.Id, StringComparison.Ordinal),
            "a goal shows GOAL! with the scorer and the scoring club's crest");

        var home = w.OnIce(Side.Home).First(p => !p.IsGoalie);
        w.Events.Clear();
        w.Events.Publish(new MatchEvent(MatchEventKind.Goal, w.Time, Side.Away, home.Id));
        hud.Update(w);
        Check(string.Equals(hud.Current?.Detail, w.Setup.Away.Name, StringComparison.Ordinal), "an own goal names the club, not the defender");

        w.Events.Clear();
        w.Time += HudMessages.GoalSeconds + 0.1f;
        hud.Update(w);
        Check(hud.Current == null, "the goal banner goes away");

        w.Events.Publish(new MatchEvent(MatchEventKind.Icing, w.Time, Side.Home, home.Id));
        hud.Update(w);
        Check(hud.Current is { Title: "ICING" } ic && string.Equals(ic.Detail, w.Setup.Home.Name, StringComparison.Ordinal) && string.Equals(ic.Crest, w.Setup.Home.Id, StringComparison.Ordinal),
            "icing names the club that iced the puck");

        w.Events.Clear();
        w.Events.Publish(new MatchEvent(MatchEventKind.Fight, w.Time));
        hud.Update(w);
        Check(string.Equals(hud.Current?.Title, "ICING", StringComparison.Ordinal), "events the HUD does not announce leave the banner alone");
    }

    void Specials(World w)
    {
        var hud = new HudMessages();
        var scorer = w.OnIce(Side.Home).First(p => !p.IsGoalie);
        var other = w.OnIce(Side.Away).First(p => !p.IsGoalie);
        HudMessages.SeasonGoals = (_, side) => side == Side.Home ? 0 : 4;
        void Goal(Side side, int player)
        {
            w.Events.Clear();
            w.Events.Publish(new MatchEvent(MatchEventKind.Goal, w.Time, side, player));
            w.Match.Score[(int)side]++;
            hud.Update(w);
        }

        Goal(Side.Home, scorer.Id);
        Check(hud.Current is { Title: "GOAL!" } g1 && g1.Lines.Count == 1 && string.Equals(g1.Lines[0], "FIRST GOAL OF THE SEASON", StringComparison.Ordinal),
            "a club's first goal of the season gets a note on the goal banner");
        Goal(Side.Away, other.Id);
        Check(hud.Current is { Title: "GOAL!" } g2 && g2.Lines.Count == 0, "a club that has scored this season gets no note");
        Goal(Side.Home, scorer.Id);
        Check(hud.Current is { Title: "GOAL!" }, "two goals are not a hat trick");
        Goal(Side.Home, scorer.Id);
        Check(hud.Current is { Title: "HAT TRICK!" } h && string.Equals(h.Detail, $"#{scorer.Data.Number} {scorer.Data.LastName}", StringComparison.Ordinal), "the third goal by one player shows HAT TRICK!");
        Goal(Side.Home, scorer.Id);
        Check(hud.Current is { Title: "GOAL!" }, "the fourth is a plain goal again");
        HudMessages.SeasonGoals = (_, _) => null;

        // 4-1 home: the game winner is home's second goal
        Check(Final(w, hud, Side.Home) is { Title: "FINAL" } f && f.Lines.Count == 1 && string.Equals(f.Lines[0], $"GAME-WINNING GOAL #{scorer.Data.Number} {scorer.Data.LastName}", StringComparison.Ordinal),
            "the final names the game-winning goal and no shutout at 4-1");
    }

    void Finals(World w)
    {
        var other = w.OnIce(Side.Away).First(p => !p.IsGoalie);
        var goalie = w.OnIce(Side.Away).First(p => p.IsGoalie);
        var hud = new HudMessages();
        for (int i = 0; i < 2; i++)
        {
            w.Events.Clear();
            w.Events.Publish(new MatchEvent(MatchEventKind.Goal, w.Time, Side.Away, other.Id));
            w.Match.Score[1]++;
            hud.Update(w);
        }

        Check(Final(w, hud, Side.Away) is { Title: "FINAL" } s && s.Lines.Count == 2 && s.Lines[0].StartsWith("GAME-WINNING GOAL", StringComparison.Ordinal)
            && string.Equals(s.Lines[1], $"SHUTOUT #{goalie.Data.Number} {goalie.Data.LastName}", StringComparison.Ordinal),
            "a 2-0 final names the game winner and the goalie's shutout");

        // a shootout win adds a goal with no event: no game winner named
        w.Match.Score[0] = 1;
        w.Match.Score[1] = 0;
        Check(Final(w, new HudMessages(), Side.Home) is { Title: "FINAL" } so && so.Lines.Count == 1 && so.Lines[0].StartsWith("SHUTOUT", StringComparison.Ordinal),
            "a goal the tally never saw names no game winner");
    }

    static HudMessage? Final(World w, HudMessages hud, Side winner)
    {
        w.Events.Clear();
        w.Events.Publish(new MatchEvent(MatchEventKind.MatchEnd, w.Time, winner));
        hud.Update(w);
        return hud.Current;
    }

    void PowerPlay(World w)
    {
        w.Match.Phase = MatchPhase.Play;
        Check(HudSystem.PowerPlayText(w) == null, "even strength shows no PP tag");
        var away = w.OnIce(Side.Away).First(p => !p.IsGoalie);
        w.Match.Penalties.Add(new PenaltyState(away.Id, Side.Away, "tripping", 120));
        Check(string.Equals(HudSystem.PowerPlayText(w), "PP 5V4", StringComparison.Ordinal), "one penalty reads PP 5V4");
        var home = w.OnIce(Side.Home).First(p => !p.IsGoalie);
        w.Match.Penalties.Add(new PenaltyState(home.Id, Side.Home, "hooking", 120));
        Check(HudSystem.PowerPlayText(w) == null, "coincidental minors are even strength");
        var away2 = w.OnIce(Side.Away).Where(p => !p.IsGoalie).Skip(1).First();
        w.Match.Penalties.Add(new PenaltyState(away2.Id, Side.Away, "slashing", 120));
        Check(string.Equals(HudSystem.PowerPlayText(w), "PP 4V3", StringComparison.Ordinal), "two against one reads PP 4V3");
        w.Match.Phase = MatchPhase.Shootout;
        Check(HudSystem.PowerPlayText(w) == null, "no PP tag in the shootout");
    }

    void WholeMatch(World w)
    {
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
            if (hud.Current is { } m) seen.Add(m.Title);
            if (w.Match.Phase == MatchPhase.Intermission && hud.Current?.Title.StartsWith("END OF", StringComparison.Ordinal) != true) periodHeld = false;
            w.Time += Game.Step;
            w.Tick++;
        }
        Check(w.Match.Phase == MatchPhase.Final, "the match reaches the final");
        Check(seen.Contains("GOAL!"), "a real goal from physics shows the goal banner");
        Check(seen.Contains("END OF PERIOD 1") && seen.Contains("END OF PERIOD 2"), "period ends show");
        Check(periodHeld, "the period banner stays up through the intermission");
        Check(hud.Current is { Title: "FINAL", Panel: "panel_red" } f && f.Detail.EndsWith(w.Setup.Away.ShortName, StringComparison.Ordinal) && string.Equals(f.Crest, w.Setup.Home.Id, StringComparison.Ordinal),
            "the final banner shows the score and the winner's crest and stays up");
    }
}
