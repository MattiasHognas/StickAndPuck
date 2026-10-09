using StickAndPuck.Core;

namespace StickAndPuck.Hud;

/// <summary>A centre-screen banner.</summary>
/// <param name="Panel">"panel", "panel_gold" or "panel_red".</param>
/// <param name="Title">The big first line.</param>
/// <param name="Detail">The second line, or empty.</param>
/// <param name="Crest">The club id whose crest sits left of the text, or null.</param>
/// <param name="Until">World time the banner goes away; infinity keeps it up.</param>
/// <param name="Icon">An icon from the icon sheet drawn left of the title, or null.</param>
internal sealed record HudMessage(string Panel, string Title, string Detail, string? Crest, float Until, string? Icon = null);

/// <summary>
/// Turns this step's <see cref="MatchEvents"/> into the banner the HUD shows: goal, end of period,
/// final, offside and icing. Keeps no match state of its own beyond which banner is up.
/// </summary>
internal sealed class HudMessages
{
    /// <summary>How long a goal banner stays up, a little under W7a's goal pause.</summary>
    public const float GoalSeconds = 3.5f;

    /// <summary>How long an end-of-period banner stays up at least.</summary>
    public const float PeriodSeconds = 4f;

    /// <summary>How long an offside or icing call stays up.</summary>
    public const float CallSeconds = 2f;

    /// <summary>Gets the banner showing now, or null.</summary>
    public HudMessage? Current { get; private set; }

    /// <summary>Reads this step's events. Call from Update; the HUD runs after rules and physics.</summary>
    /// <param name="world">The match state.</param>
    public void Update(World world)
    {
        foreach (var e in world.Events.Current)
        {
            var next = For(world, e);
            if (next != null) Current = next;
        }
        if (Current != null && world.Time >= Current.Until && !Held(world, Current)) Current = null;
    }

    /// <summary>Clears the banner, for a new match.</summary>
    public void Clear()
    {
        Current = null;
    }

    /// <summary>Builds the banner for one event, or null for events the HUD does not announce.</summary>
    /// <param name="world">The match state.</param>
    /// <param name="e">The event.</param>
    /// <returns>The banner, or null.</returns>
    public static HudMessage? For(World world, MatchEvent e)
    {
        var m = world.Match;
        string Short(Side s) => world.Setup.Club(s).ShortName;
        switch (e.Kind)
        {
            case MatchEventKind.Goal when e.Side is { } scorer:
                return new("panel_gold", "GOAL!", Scorer(world, e, scorer), world.Setup.Club(scorer).Id, e.Time + GoalSeconds);
            case MatchEventKind.PeriodEnd:
                int period = e.Strength > 0 ? (int)e.Strength : m.Period;
                string title = period > world.Setup.Periods ? "END OF OVERTIME" : $"END OF PERIOD {period}";
                return new("panel", title, $"{Short(Side.Home)} {m.Score[0]} - {m.Score[1]} {Short(Side.Away)}", null, e.Time + PeriodSeconds);
            case MatchEventKind.MatchEnd:
                string detail = $"{Short(Side.Home)} {m.Score[0]} - {m.Score[1]} {Short(Side.Away)}";
                string? crest = e.Side is { } winner ? world.Setup.Club(winner).Id : null;
                return new("panel_red", e.Side == null ? "FINAL - TIE" : "FINAL", detail, crest, float.PositiveInfinity);
            case MatchEventKind.Offside:
                return new("panel", "OFFSIDE", "", null, e.Time + CallSeconds, "whistle");
            case MatchEventKind.Icing:
                return new("panel", "ICING", "", null, e.Time + CallSeconds, "whistle");
            default:
                return null;
        }
    }

    /// <summary>Gets "#12 SURNAME" for the scorer, or the club name when nobody on that club touched it last.</summary>
    static string Scorer(World world, MatchEvent e, Side scorer)
    {
        if (e.PlayerId >= 0 && e.PlayerId < world.Players.Count && world.Players[e.PlayerId] is { } p && p.Side == scorer)
            return $"#{p.Data.Number} {p.Data.LastName}";
        return world.Setup.Club(scorer).Name;
    }

    /// <summary>An end-of-period banner stays up for the whole intermission.</summary>
    static bool Held(World world, HudMessage message)
    {
        return message.Title.StartsWith("END OF", StringComparison.Ordinal) && world.Match.Phase == MatchPhase.Intermission;
    }
}
