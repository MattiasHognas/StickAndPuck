using StickAndPuck.Core;
using StickAndPuck.Physics;
using StickAndPuck.Render;
using StickAndPuck.Rules;

namespace StickAndPuck.Hud;

/// <summary>
/// SAP_SCENE=hud: both starting lineups with the scorebug at 2-1 in the second period and one
/// banner up. SAP_HUD_MESSAGE picks it: goal (default), period, final, tie, offside, icing, intro
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
        MatchEvent? e = Message switch
        {
            "goal" => new(MatchEventKind.Goal, world.Time, Side.Home, scorer?.Id ?? -1),
            "period" => new(MatchEventKind.PeriodEnd, world.Time, Strength: 2),
            "final" => new(MatchEventKind.MatchEnd, world.Time, Side.Home),
            "tie" => new(MatchEventKind.MatchEnd, world.Time),
            "offside" => new(MatchEventKind.Offside, world.Time),
            "icing" => new(MatchEventKind.Icing, world.Time),
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

        w.Events.Publish(new MatchEvent(MatchEventKind.Icing, w.Time));
        hud.Update(w);
        Check(hud.Current is { Title: "ICING", Icon: "whistle" }, "icing shows a call");

        w.Events.Clear();
        w.Events.Publish(new MatchEvent(MatchEventKind.Fight, w.Time));
        hud.Update(w);
        Check(string.Equals(hud.Current?.Title, "ICING", StringComparison.Ordinal), "events the HUD does not announce leave the banner alone");
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
