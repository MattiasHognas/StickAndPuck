using System.Text.Json.Serialization;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season;

/// <summary>One finished (or abandoned) arcade run on the best-runs board, kept in arcade.json across runs.
/// Ranked champions first, then most clubs beaten, fewest losses, best goal difference, earliest.</summary>
/// <param name="Club">The human's club id.</param>
/// <param name="Rule">Sudden death or three lives.</param>
/// <param name="Beaten">Clubs beaten.</param>
/// <param name="Champion">True when every club was beaten.</param>
/// <param name="Losses">Games lost.</param>
/// <param name="GoalsFor">Goals scored over the run.</param>
/// <param name="GoalsAgainst">Goals conceded over the run.</param>
/// <param name="Ended">When the run ended (UTC).</param>
/// <param name="TopScorer">The run's top scorer, "#NR NAME", or empty.</param>
/// <param name="TopPoints">That player's points.</param>
public sealed record ArcadeRecord(string Club, ArcadeRule Rule, int Beaten, bool Champion, int Losses, int GoalsFor, int GoalsAgainst, DateTime Ended, string TopScorer, int TopPoints)
{
    /// <summary>How many runs the board keeps.</summary>
    public const int Kept = 10;

    /// <summary>Gets the goal difference.</summary>
    [JsonIgnore]
    public int GoalDifference => GoalsFor - GoalsAgainst;

    /// <summary>Gets a value indicating whether this run ranks above another.</summary>
    public bool Beats(ArcadeRecord other)
    {
        if (Champion != other.Champion) return Champion;
        if (Beaten != other.Beaten) return Beaten > other.Beaten;
        if (Losses != other.Losses) return Losses < other.Losses;
        if (GoalDifference != other.GoalDifference) return GoalDifference > other.GoalDifference;
        return Ended < other.Ended;
    }

    /// <summary>The record of a run as it stands.</summary>
    public static ArcadeRecord Of(ArcadeRun run)
    {
        var top = run.Stats.Leaders(1, run.UserClub).FirstOrNone();
        return new ArcadeRecord(run.UserClub, run.Rule, run.Stage, run.Champion, run.Losses,
            run.Games.Sum(g => g.GoalsFor), run.Games.Sum(g => g.GoalsAgainst), DateTime.UtcNow, top.Select(t => t.Label).Or(""), top.Select(t => t.Points).Or(0));
    }

    /// <summary>Adds a run to a board and keeps the best <see cref="Kept"/>, best first.</summary>
    public static List<ArcadeRecord> Add(IEnumerable<ArcadeRecord> board, ArcadeRecord record)
    {
        var all = board.Append(record).ToList();
        all.Sort((a, b) => a == b ? 0 : a.Beats(b) ? -1 : 1);
        return all.Take(Kept).ToList();
    }

    /// <summary>Gets a run's place on a board, from 1, or 0 when it is not on it.</summary>
    public static int Place(IReadOnlyList<ArcadeRecord> board, ArcadeRecord record)
    {
        for (int i = 0; i < board.Count; i++) if (board[i] == record) return i + 1;
        return 0;
    }
}
