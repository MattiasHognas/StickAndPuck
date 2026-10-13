using StickAndPuck.App.Core;

namespace StickAndPuck.App.Hud;

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
