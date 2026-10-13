using StickAndPuck.App.Core;
using StickAndPuck.App.Rules;
using StickAndPuck.App.Season;

namespace StickAndPuck.App.Hud;

/// <summary>A centre-screen banner.</summary>
/// <param name="Panel">"panel", "panel_gold" or "panel_red".</param>
/// <param name="Title">The big first line.</param>
/// <param name="Detail">The second line, or empty.</param>
/// <param name="Crest">The club id whose crest sits left of the text, or null.</param>
/// <param name="Until">World time the banner goes away; infinity keeps it up.</param>
/// <param name="Icon">An icon from the icon sheet drawn left of the title, or null.</param>
/// <param name="Notes">Extra gold lines under the detail: game-winning goal, shutout, first goal of the season.</param>
internal sealed record HudMessage(string Panel, string Title, string Detail, string? Crest, float Until, string? Icon = null, IReadOnlyList<string>? Notes = null)
{
    /// <summary>Gets the extra lines, never null.</summary>
    public IReadOnlyList<string> Lines => Notes ?? [];
}

/// <summary>
/// Turns this step's <see cref="MatchEvents"/> into the banner the HUD shows: goal (with hat trick
/// and first goal of the season), end of period, final (with the game-winning goal and a shutout),
/// offside, icing with the club name, and penalties. Keeps a tally of the match's goals so it can
/// tell a hat trick or the game winner from the events alone; W25's stats are not needed.
/// </summary>
internal sealed class HudMessages
{
    /// <summary>How long a goal banner stays up, a little under W7a's goal pause.</summary>
    public const float GoalSeconds = 3.5f;

    /// <summary>How long an end-of-period banner stays up at least.</summary>
    public const float PeriodSeconds = 4f;

    /// <summary>How long an offside or icing call stays up.</summary>
    public const float CallSeconds = 2f;

    /// <summary>Goals in a match that make a hat trick.</summary>
    public const int HatTrick = 3;

    readonly List<(Side Side, int PlayerId)> goals = [];

    /// <summary>
    /// Gets or sets how many goals a club has scored earlier this season, or null outside season mode.
    /// The default reads the season being played; the hud scene swaps in a fake.
    /// </summary>
    public static Func<World, Side, int?> SeasonGoals { get; set; } = SeasonGoalsBefore;

    /// <summary>Gets the banner showing now, or null.</summary>
    public HudMessage? Current { get; private set; }

    /// <summary>Gets the goals of this match so far, in order: the scoring club and the scorer (-1 when unknown).</summary>
    public IReadOnlyList<(Side Side, int PlayerId)> Goals => goals;

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

    /// <summary>Clears the banner and the goal tally, for a new match.</summary>
    public void Clear()
    {
        Current = null;
        goals.Clear();
    }

    /// <summary>Builds the banner for one event, or null for events the HUD does not announce. Goals and the final are tallied.</summary>
    /// <param name="world">The match state.</param>
    /// <param name="e">The event.</param>
    /// <returns>The banner, or null.</returns>
    public HudMessage? For(World world, MatchEvent e)
    {
        var m = world.Match;
        string Short(Side s) => world.Setup.Club(s).ShortName;
        switch (e.Kind)
        {
            case MatchEventKind.Goal when e.Side is { } scorer:
                return Goal(world, e, scorer);
            case MatchEventKind.PeriodEnd:
                int period = e.Strength > 0 ? (int)e.Strength : m.Period;
                string title = period > world.Setup.Periods ? "END OF OVERTIME" : $"END OF PERIOD {period}";
                return new("panel", title, $"{Short(Side.Home)} {m.Score[0]} - {m.Score[1]} {Short(Side.Away)}", null, e.Time + PeriodSeconds);
            case MatchEventKind.MatchEnd:
                string detail = $"{Short(Side.Home)} {m.Score[0]} - {m.Score[1]} {Short(Side.Away)}";
                string? crest = e.Side is { } winner ? world.Setup.Club(winner).Id : null;
                return new("panel_red", e.Side == null ? "FINAL - TIE" : "FINAL", detail, crest, float.PositiveInfinity, Notes: FinalNotes(world, e.Side));
            case MatchEventKind.Offside:
                return new("panel", "OFFSIDE", "", null, e.Time + CallSeconds, "whistle");
            case MatchEventKind.Icing when e.Side is { } iced:
                // the club that iced the puck, by name, with its crest
                return new("panel", "ICING", world.Setup.Club(iced).Name, world.Setup.Club(iced).Id, e.Time + CallSeconds);
            case MatchEventKind.Icing:
                return new("panel", "ICING", "", null, e.Time + CallSeconds, "whistle");
            case MatchEventKind.Penalty when e.Side is { } offender && e.PlayerId >= 0 && e.PlayerId < world.Players.Count:
                // W14: the infraction and minutes, then the player
                var called = m.Penalties.FindLast(p => p.PlayerId == e.PlayerId);
                var who = world.Players[e.PlayerId].Data;
                return new("panel_red", $"{called?.Infraction ?? "penalty"} {MatchFlow.ClockText(e.Strength)}", $"#{who.Number} {who.LastName}", world.Setup.Club(offender).Id, e.Time + CallSeconds + 1);
            default:
                return null;
        }
    }

    /// <summary>Gets the player's goals so far this match.</summary>
    /// <param name="playerId">The player.</param>
    /// <returns>The count.</returns>
    public int GoalsBy(int playerId)
    {
        return playerId < 0 ? 0 : goals.Count(g => g.PlayerId == playerId);
    }

    /// <summary>A goal: GOAL! with the scorer, HAT TRICK! on the third, plus a note for a club's first goal of the season.</summary>
    HudMessage Goal(World world, MatchEvent e, Side scorer)
    {
        int clubBefore = goals.Count(g => g.Side == scorer);
        int? seasonBefore = SeasonGoals(world, scorer);
        int player = Scorer(world, e, scorer)?.Id ?? -1;
        goals.Add((scorer, player));

        string club = world.Setup.Club(scorer).Id;
        string detail = ScorerText(world, e, scorer);
        List<string>? notes = clubBefore == 0 && seasonBefore == 0 ? ["FIRST GOAL OF THE SEASON"] : null;
        string title = GoalsBy(player) == HatTrick ? "HAT TRICK!" : "GOAL!";
        return new("panel_gold", title, detail, club, e.Time + GoalSeconds, Notes: notes);
    }

    /// <summary>The final's extra lines: the game-winning goal and a shutout, when the events say so.</summary>
    List<string>? FinalNotes(World world, Side? winner)
    {
        if (winner is not { } w) return null;
        var m = world.Match;
        var loser = w == Side.Home ? Side.Away : Side.Home;
        var notes = new List<string>();

        // the winner's goal that put them ahead for good: one more than the loser's total.
        // A shootout win adds a goal with no Goal event, so the tally runs short and no winner is named.
        int needed = m.Score[(int)loser] + 1;
        var winners = goals.Where(g => g.Side == w).ToList();
        if (needed <= winners.Count && winners[needed - 1].PlayerId is var gw && gw >= 0 && gw < world.Players.Count)
        {
            var p = world.Players[gw].Data;
            notes.Add($"GAME-WINNING GOAL #{p.Number} {p.LastName}");
        }

        if (m.Score[(int)loser] == 0 && world.OnIce(w).FirstOrDefault(p => p.IsGoalie) is { } goalie)
            notes.Add($"SHUTOUT #{goalie.Data.Number} {goalie.Data.LastName}");
        return notes.Count > 0 ? notes : null;
    }

    /// <summary>Gets the scorer when the event names a player on the scoring club, else null (an own goal).</summary>
    static Player? Scorer(World world, MatchEvent e, Side scorer)
    {
        if (e.PlayerId >= 0 && e.PlayerId < world.Players.Count && world.Players[e.PlayerId] is { } p && p.Side == scorer) return p;
        return null;
    }

    /// <summary>Gets "#12 SURNAME" for the scorer, or the club name when nobody on that club touched it last.</summary>
    static string ScorerText(World world, MatchEvent e, Side scorer)
    {
        return Scorer(world, e, scorer) is { } p ? $"#{p.Data.Number} {p.Data.LastName}" : world.Setup.Club(scorer).Name;
    }

    /// <summary>Goals a club scored in the season's earlier games, or null when this match is not a season game.</summary>
    static int? SeasonGoalsBefore(World world, Side side)
    {
        if (SeasonSession.Current is not { } season || SeasonSession.Playing == null) return null;
        string club = world.Setup.Club(side).Id;
        return season.PlayedBy(club).Sum(g => string.Equals(g.Home, club, StringComparison.Ordinal) ? g.Result!.HomeGoals : g.Result!.AwayGoals);
    }

    /// <summary>An end-of-period banner stays up for the whole intermission.</summary>
    static bool Held(World world, HudMessage message)
    {
        return message.Title.StartsWith("END OF", StringComparison.Ordinal) && world.Match.Phase == MatchPhase.Intermission;
    }
}
