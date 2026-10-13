using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season;

/// <summary>Every player's line for one match, and the player of the game.</summary>
public sealed class MatchStatSheet
{
    /// <summary>Gets or sets the home club id.</summary>
    public string Home { get; set; } = "";

    /// <summary>Gets or sets the away club id.</summary>
    public string Away { get; set; } = "";

    /// <summary>Gets or sets the home goals on the final score.</summary>
    public int HomeGoals { get; set; }

    /// <summary>Gets or sets the away goals on the final score.</summary>
    public int AwayGoals { get; set; }

    /// <summary>Gets or sets every player's line, home club first.</summary>
    public List<MatchStatLine> Lines { get; set; } = [];

    /// <summary>Gets or sets the player of the game, null only when nobody played.</summary>
    public MatchStatLine? PlayerOfGame { get; set; }

    /// <summary>Gets a value indicating whether a side won the match.</summary>
    /// <param name="side">The side.</param>
    /// <returns>True for the winner.</returns>
    public bool Won(Side side)
    {
        return side == Side.Home ? HomeGoals > AwayGoals : AwayGoals > HomeGoals;
    }

    /// <summary>Gets the lines of one side, best first.</summary>
    /// <param name="side">The side.</param>
    /// <returns>The lines.</returns>
    public IEnumerable<MatchStatLine> Of(Side side)
    {
        return Lines.Where(l => l.Side == side).OrderByDescending(l => Merit(l, this));
    }

    /// <summary>Gets the award for the player of the game, for the season's or the run's list.</summary>
    /// <param name="game">The game key.</param>
    /// <returns>The award, or null when nobody played.</returns>
    public PlayerOfGameAward? Award(string game)
    {
        return PlayerOfGame is { } s ? new PlayerOfGameAward(game, Home, Away, HomeGoals, AwayGoals, s.Club, s.Number, s.Name, s.Goals, s.Assists, s.Saves) : null;
    }

    /// <summary>
    /// Picks the player of the game: the line with the most merit, where a goal is worth 3, an
    /// assist 2, a shot or a hit a little, a goalie's saves add up and a shutout counts a lot;
    /// the winners get an edge, so the star is nearly always on the winning side.
    /// </summary>
    public void PickPlayerOfGame()
    {
        PlayerOfGame = Lines.Where(l => l.Played)
            .OrderByDescending(l => Merit(l, this))
            .ThenByDescending(l => l.Points)
            .ThenBy(l => l.Number)
            .FirstOrDefault();
    }

    /// <summary>How much one line earned the award.</summary>
    static float Merit(MatchStatLine l, MatchStatSheet sheet)
    {
        float m;
        if (l.IsGoalie)
        {
            int against = l.Side == Side.Home ? sheet.AwayGoals : sheet.HomeGoals;
            m = (l.Saves * 0.18f) - (against * 1.2f) + (against == 0 && l.Saves > 0 ? 3f : 0f);
        }
        else
        {
            m = (l.Goals * 3f) + (l.Assists * 2f) + (l.Shots * 0.25f) + (l.Hits * 0.3f);
        }
        return m + (sheet.Won(l.Side) ? 1f : 0f);
    }
}

/// <summary>
/// Follows one match step by step: who was on the ice, the shots, hits and saves from the event
/// log, the goals, and the assists worked out from the puck's touches, since the event log carries
/// no assist of its own: the last one or two teammates to touch the puck before the scorer, with
/// no opponent touch in between and within <see cref="AssistWindow"/> seconds, get the assists.
/// Shootout goals go to the score but to nobody's line, as in the SHL.
/// </summary>
public sealed class MatchStatTracker
{
    /// <summary>Seconds of game time within which a teammate's touch before a goal counts as an assist.</summary>
    public const float AssistWindow = 12f;

    /// <summary>Seconds within which the last attacker's touch before an own goal or a deflection counts as the goal.</summary>
    const float DeflectionWindow = 2.5f;

    const int TouchHistory = 12;

    readonly Dictionary<int, MatchStatLine> lines = [];
    readonly List<(int Id, Side Side, float Time)> touches = [];
    World? world;
    int lastTouch = -1;
    bool shootout;

    /// <summary>Gets the sheet so far, with no player of the game until <see cref="Finish"/>.</summary>
    public MatchStatSheet Sheet { get; } = new();

    /// <summary>Gets the world being followed, or null before the first step.</summary>
    public World? World => world;

    /// <summary>Follows one step: call after the systems that publish the step's events have run.</summary>
    /// <param name="w">The world.</param>
    public void Observe(World w)
    {
        if (!ReferenceEquals(w, world)) Begin(w);
        var m = w.Match;
        if (m.Phase == MatchPhase.Shootout) shootout = true;
        foreach (var p in w.Players)
            if (p.OnIce && lines.TryGetValue(p.Id, out var l)) l.Played = true;

        int t = w.Puck.LastTouch;
        if (t != lastTouch && t >= 0 && t < w.Players.Count)
        {
            touches.Add((t, w.Players[t].Side, w.Time));
            if (touches.Count > TouchHistory) touches.RemoveAt(0);
        }
        lastTouch = t;

        foreach (var e in w.Events.Current)
        {
            switch (e.Kind)
            {
                case MatchEventKind.Shot when !shootout:
                    if (Line(e.PlayerId) is { } shooter) shooter.Shots++;
                    break;
                case MatchEventKind.Check:
                    if (Line(e.PlayerId) is { } checker) checker.Hits++;
                    break;
                case MatchEventKind.Save:
                    if (Line(e.PlayerId) is { } goalie) goalie.Saves++;
                    break;
                case MatchEventKind.Goal when !shootout && e.Side is { } side:
                    Goal(w, side, e.PlayerId, e.Time);
                    break;
                case MatchEventKind.Faceoff:
                    touches.Clear();
                    break;
                default:
                    break;
            }
        }
    }

    /// <summary>Closes the sheet with the final score and picks the player of the game.</summary>
    /// <returns>The sheet.</returns>
    public MatchStatSheet Finish()
    {
        if (world != null)
        {
            Sheet.HomeGoals = world.Match.Score[(int)Side.Home];
            Sheet.AwayGoals = world.Match.Score[(int)Side.Away];
        }
        Sheet.PickPlayerOfGame();
        return Sheet;
    }

    void Begin(World w)
    {
        world = w;
        lines.Clear();
        touches.Clear();
        lastTouch = -1;
        shootout = false;
        Sheet.Home = w.Setup.Home.Id;
        Sheet.Away = w.Setup.Away.Id;
        Sheet.Lines.Clear();
        Sheet.PlayerOfGame = null;
        foreach (var p in w.Players)
        {
            var line = MatchStatLine.For(w.Setup.Club(p.Side).Id, p.Side, p.Data);
            lines[p.Id] = line;
            Sheet.Lines.Add(line);
        }
    }

    MatchStatLine? Line(int id)
    {
        return lines.TryGetValue(id, out var l) ? l : null;
    }

    void Goal(World w, Side side, int playerId, float time)
    {
        int scorer = playerId >= 0 && playerId < w.Players.Count && w.Players[playerId].Side == side ? playerId : -1;
        if (scorer < 0)
        {
            // an own goal or a deflection: the last attacker to touch the puck just before gets it
            for (int i = touches.Count - 1; i >= 0; i--)
            {
                if (time - touches[i].Time > DeflectionWindow) break;
                if (touches[i].Side == side) { scorer = touches[i].Id; break; }
            }
        }
        if (scorer < 0 || Line(scorer) is not { } line)
        {
            touches.Clear();
            return;
        }
        line.Goals++;

        var assists = new List<int>();
        int at = touches.Count - 1;
        while (at >= 0 && touches[at].Id == scorer) at--;    // the scorer's own touches at the end
        for (; at >= 0 && assists.Count < 2; at--)
        {
            var (id, s, t) = touches[at];
            if (s != side || id == scorer || time - t > AssistWindow) break;
            if (!assists.Contains(id)) assists.Add(id);
        }
        foreach (int id in assists) Line(id)!.Assists++;
        touches.Clear();
    }
}

/// <summary>The last match's sheet, for the results screen of any mode (exhibition and versus too).</summary>
public static class MatchStats
{
    /// <summary>Gets the sheet of the last match that ended in this run, or null before one has.</summary>
    public static MatchStatSheet? Last { get; internal set; }
}
