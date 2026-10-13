using StickAndPuck.App.Core;
using StickAndPuck.App.Rules;
using StickAndPuck.App.Season;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Hud;

/// <summary>A centre-screen banner.</summary>
/// <param name="Panel">"panel", "panel_gold" or "panel_red".</param>
/// <param name="Title">The big first line.</param>
/// <param name="Detail">The second line, or empty.</param>
/// <param name="Crest">The club id whose crest sits left of the text, or empty for none.</param>
/// <param name="Until">World time the banner goes away; infinity keeps it up.</param>
/// <param name="Icon">An icon from the icon sheet drawn left of the title, or empty for none.</param>
/// <param name="Intermission">True for an end-of-period banner, which stays up for the whole intermission.</param>
internal sealed record HudMessage(string Panel, string Title, string Detail, string Crest, float Until, string Icon = "", bool Intermission = false)
{
    /// <summary>Gets the extra gold lines under the detail: game-winning goal, shutout, first goal of the season.</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];

    public IReadOnlyList<string> Lines => Notes;
}

/// <summary>Turns this step's <see cref="MatchEvents"/> into the banner the HUD shows: goal (with hat trick
/// and first goal of the season), end of period, final (with the game-winning goal and a shutout),
/// offside, icing with the club name, and penalties. Keeps a tally of the match's goals so it can
/// tell a hat trick or the game winner from the events alone; the season stats are not needed.</summary>
internal sealed class HudMessages
{
    /// <summary>How long a goal banner stays up, a little under the match's goal pause.</summary>
    public const float GoalSeconds = 3.5f;

    /// <summary>How long an end-of-period banner stays up at least.</summary>
    public const float PeriodSeconds = 4f;

    /// <summary>How long an offside or icing call stays up.</summary>
    public const float CallSeconds = 2f;

    /// <summary>Goals in a match that make a hat trick.</summary>
    public const int HatTrick = 3;

    readonly List<(Side Side, int PlayerId)> goals = [];

    /// <summary>Gets or sets how many goals a club has scored earlier this season, or None outside season mode.
    /// The default reads the season being played; the hud scene swaps in a fake.</summary>

    /// <summary>Gets the banner showing now, or None.</summary>
    public Maybe<HudMessage> Current { get; private set; }

    /// <summary>Gets the goals of this match so far, in order: the scoring club and the scorer (-1 when unknown).</summary>
    public IReadOnlyList<(Side Side, int PlayerId)> Goals => goals;

    /// <summary>Reads this step's events. Call from Update; the HUD runs after rules and physics.</summary>
    public void Update(World world)
    {
        foreach (var e in world.Events.Current)
        {
            var next = For(world, e);
            if (next.HasValue) Current = next;
        }
        if (Current.TryGet(out var shown) && world.Time >= shown.Until && !Held(world, shown)) Current = Maybe<HudMessage>.None;
    }

    /// <summary>Clears the banner and the goal tally, for a new match.</summary>
    public void Clear()
    {
        Current = Maybe<HudMessage>.None;
        goals.Clear();
    }

    /// <summary>Builds the banner for one event, or None for events the HUD does not announce. Goals and the final are tallied.</summary>
    public Maybe<HudMessage> For(World world, MatchEvent e)
    {
        var m = world.Match;
        string Short(Side s) => world.Setup.Club(s).ShortName;
        string Score() => Text.Format(K.ScoreLine, Short(Side.Home), m.Score[0], m.Score[1], Short(Side.Away));
        switch (e)
        {
            case GoalScored goal:
                return Goal(world, goal);
            case PeriodEnded end:
                string title = end.Period > world.Setup.Periods ? Text.Get(K.EndOfOvertime) : Text.Format(K.EndOfPeriod, end.Period);
                return new HudMessage("panel", title, Score(), "", e.Time + PeriodSeconds, Intermission: true);
            case MatchWon won:
                return new HudMessage("panel_red", Text.Get(K.Final), Score(), world.Setup.Club(won.Winner).Id, float.PositiveInfinity) { Notes = FinalNotes(world, won.Winner) };
            case MatchTied:
                return new HudMessage("panel_red", Text.Get(K.FinalTie), Score(), "", float.PositiveInfinity);
            case OffsideCalled:
                return new HudMessage("panel", Text.Get(K.Offside), "", "", e.Time + CallSeconds, "whistle");
            case IcingCalled icing:
                // the club that iced the puck, by name, with its crest
                return new HudMessage("panel", Text.Get(K.Icing), world.Setup.Club(icing.Offender).Name, world.Setup.Club(icing.Offender).Id, e.Time + CallSeconds);
            case PenaltyCalled penalty:
                // The infraction and minutes, then the player
                var called = m.Penalties.FindLast(p => p.PlayerId == penalty.PlayerId);
                var who = world.Players[penalty.PlayerId].Data;
                return new HudMessage("panel_red", Text.Format(K.PenaltyBanner, Infraction.Label(called is { } served ? served.Infraction : ""), MatchFlow.ClockText(penalty.Seconds)), Text.Format(K.PlayerTag, who.Number, who.LastName), world.Setup.Club(penalty.Side).Id, e.Time + CallSeconds + 1);
            default:
                return Maybe<HudMessage>.None;
        }
    }

    /// <summary>Gets the player's goals so far this match.</summary>
    public int GoalsBy(int playerId)
    {
        return playerId < 0 ? 0 : goals.Count(g => g.PlayerId == playerId);
    }

    /// <summary>A goal: GOAL! with the scorer, HAT TRICK! on the third, plus a note for a club's first goal of the season.</summary>
    HudMessage Goal(World world, GoalScored e)
    {
        var scorer = e.Side;
        int clubBefore = goals.Count(g => g.Side == scorer);
        var seasonBefore = world.Switches.SeasonGoals(world, scorer);
        int player = Scorer(world, e).TryGet(out var p) ? p.Id : -1;
        goals.Add((scorer, player));

        string club = world.Setup.Club(scorer).Id;
        string detail = ScorerText(world, e);
        List<string> notes = clubBefore == 0 && seasonBefore == 0 ? [Text.Get(K.FirstGoalOfSeason)] : [];
        string title = Text.Get(GoalsBy(player) == HatTrick ? K.HatTrick : K.Goal);
        return new("panel_gold", title, detail, club, e.Time + GoalSeconds) { Notes = notes };
    }

    /// <summary>The final's extra lines: the game-winning goal and a shutout, when the events say so.</summary>
    List<string> FinalNotes(World world, Side w)
    {
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
            notes.Add(Text.Format(K.GameWinningGoal, p.Number, p.LastName));
        }

        if (m.Score[(int)loser] == 0 && world.OnIce(w).FirstOrDefault(p => p.IsGoalie) is { } goalie)
            notes.Add(Text.Format(K.Shutout, goalie.Data.Number, goalie.Data.LastName));
        return notes;
    }

    /// <summary>Gets the scorer when the event names a player on the scoring club, else None (an own goal).</summary>
    static Maybe<Player> Scorer(World world, GoalScored e)
    {
        var p = world.Players[e.ScorerId];
        return p.Side == e.Side ? p : Maybe<Player>.None;
    }

    /// <summary>Gets "#12 SURNAME" for the scorer, or the club name when nobody on that club touched it last.</summary>
    static string ScorerText(World world, GoalScored e)
    {
        return Scorer(world, e).TryGet(out var p) ? Text.Format(K.PlayerTag, p.Data.Number, p.Data.LastName) : world.Setup.Club(e.Side).Name;
    }

    /// <summary>Goals a club scored in the season's earlier games, or None when this match is not a season game.</summary>
    internal static Maybe<int> SeasonGoalsBefore(World world, Side side)
    {
        if (!SeasonSession.Current.TryGet(out var season) || !SeasonSession.Playing.HasValue) return Maybe<int>.None;
        string club = world.Setup.Club(side).Id;
        return season.PlayedBy(club).Sum(g => g.Result.Select(r => string.Equals(g.Home, club, StringComparison.Ordinal) ? r.HomeGoals : r.AwayGoals).Or(0));
    }

    /// <summary>An end-of-period banner stays up for the whole intermission.</summary>
    static bool Held(World world, HudMessage message)
    {
        return message.Intermission && world.Match.Phase == MatchPhase.Intermission;
    }
}
