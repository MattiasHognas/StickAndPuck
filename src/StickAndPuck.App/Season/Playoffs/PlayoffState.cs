using System.Text.Json.Serialization;
using StickAndPuck.App.Core;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Season;

/// <summary>
/// SHL playoffs, seeded from the final table: places 1 to 6 go straight to the quarter-finals,
/// places 7 to 10 play a best of three play-in (7 against 10, 8 against 9), then best of seven
/// quarter-finals, semi-finals and a final. Every round is reseeded, so the best seed left meets
/// the worst one left. The human's club plays its games; every other game is settled by
/// <see cref="QuickSim"/>, kept level with the human's series so the bracket moves together.
/// Saved inside the <see cref="SeasonState"/> it follows.
/// </summary>
public sealed class PlayoffState
{
    /// <summary>Places that go straight to the quarter-finals.</summary>
    public const int Direct = 6;

    /// <summary>The last place that reaches the play-in.</summary>
    public const int LastPlayIn = 10;

    static readonly (string Name, int BestOf)[] Format =
        [("PLAY-IN", 3), ("QUARTER-FINALS", 7), ("SEMI-FINALS", 7), ("FINAL", 7)];

    /// <summary>Gets or sets the seed the simulated games come from.</summary>
    public int Seed { get; set; }

    /// <summary>Gets or sets the id of the human's club.</summary>
    public string UserClub { get; set; } = "";

    /// <summary>Gets or sets every club in final table order: the seeds, best first.</summary>
    public List<string> Seeds { get; set; } = [];

    /// <summary>Gets or sets the rounds started so far; the last one is being played.</summary>
    public List<PlayoffRound> Rounds { get; set; } = [];

    /// <summary>Gets a round's name in the language shown: play-in, quarter-finals, semi-finals, final.</summary>
    /// <param name="round">The round, from 0.</param>
    /// <returns>The name.</returns>
    public static string RoundTitle(int round) => Text.Get(round switch
    {
        <= 0 => K.RoundPlayIn,
        1 => K.RoundQuarterFinals,
        2 => K.RoundSemiFinals,
        _ => K.RoundFinal,
    });

    /// <summary>Gets the number of rounds in the playoffs.</summary>
    [JsonIgnore]
    public static int RoundCount => Format.Length;

    /// <summary>Gets the name of the round being played, in the language shown (W53; <see cref="PlayoffRound.Name"/> is the save's).</summary>
    [JsonIgnore]
    public string CurrentTitle => RoundTitle(Rounds.Count - 1);

    /// <summary>Gets the round being played.</summary>
    [JsonIgnore]
    public PlayoffRound Current => Rounds[^1];

    /// <summary>Gets the champion's club id, or null until the final is won.</summary>
    [JsonIgnore]
    public string? Champion => Rounds.Count == Format.Length && Current.IsOver ? Current.Series[0].Winner : null;

    /// <summary>Gets a value indicating whether the playoffs have a champion.</summary>
    [JsonIgnore]
    public bool IsOver => Champion != null;

    /// <summary>Gets the human's open series, or null when the human's club is out, waiting for a round, or done.</summary>
    [JsonIgnore]
    public PlayoffSeries? UserSeries => Rounds.Count == 0 ? null : Current.Series.Find(s => !s.IsOver && s.Involves(UserClub));

    /// <summary>Gets a value indicating whether the human's club made the playoffs.</summary>
    [JsonIgnore]
    public bool UserQualified => SeedOf(UserClub) is > 0 and <= LastPlayIn;

    /// <summary>Gets a value indicating whether the human's club can still win the title.</summary>
    [JsonIgnore]
    public bool UserAlive => UserQualified && (IsOver ? Same(Champion!, UserClub) : Alive());

    /// <summary>Seeds the playoffs from a finished season and starts the play-in.</summary>
    /// <param name="season">The season; its table gives the seeds.</param>
    /// <returns>The playoffs, nothing played.</returns>
    public static PlayoffState New(SeasonState season)
    {
        var state = new PlayoffState
        {
            Seed = unchecked((season.Seed * 31) + 2_027),
            UserClub = season.UserClub,
            Seeds = season.Table.Select(r => r.ClubId).ToList(),
        };
        state.Rounds.Add(state.Pair(0, Enumerable.Range(Direct + 1, LastPlayIn - Direct)));
        return state;
    }

    /// <summary>Gets a club's seed: its place in the final table, from 1, or 0 when it is not in the season.</summary>
    /// <param name="clubId">The club id.</param>
    /// <returns>The seed.</returns>
    public int SeedOf(string clubId)
    {
        return Seeds.FindIndex(s => Same(s, clubId)) + 1;
    }

    /// <summary>Adds a game to a series of the round being played.</summary>
    /// <param name="series">The series; the game is its next one, at <see cref="PlayoffSeries.NextHome"/>.</param>
    /// <param name="result">The result; a tie is refused, as playoff games always have a winner.</param>
    /// <returns>True when the game was added.</returns>
    public bool Record(PlayoffSeries series, FixtureResult result)
    {
        if (series.IsOver || result.HomeGoals == result.AwayGoals || !Current.Series.Contains(series)) return false;
        series.Games.Add(new PlayoffGame(series.NextHome, series.NextAway, result));
        return true;
    }

    /// <summary>
    /// Settles every game up to the human's next one: the other series of the round are played to
    /// as many games as the human's series has, and when the human has no open series the round is
    /// finished and the next one drawn, until the human plays again or there is a champion.
    /// </summary>
    /// <param name="clubs">Every club, for their traits.</param>
    /// <returns>The games settled.</returns>
    public int SimulateUntilUser(IReadOnlyList<ClubData> clubs)
    {
        var byId = clubs.ToDictionary(c => c.Id, StringComparer.Ordinal);
        int count = 0;
        while (!IsOver)
        {
            var user = UserSeries;
            int upTo = user?.Games.Count ?? int.MaxValue;
            for (int i = 0; i < Current.Series.Count; i++)
            {
                var s = Current.Series[i];
                while (s != user && !s.IsOver && s.Games.Count < upTo)
                {
                    Record(s, Simulate(byId, s, i));
                    count++;
                }
            }
            if (user != null || !NextRound()) break;
        }
        return count;
    }

    /// <summary>Draws the next round once the current one is over.</summary>
    /// <returns>True when a round was added.</returns>
    public bool NextRound()
    {
        if (!Current.IsOver || Rounds.Count >= Format.Length) return false;
        var winners = Current.Series.Select(s => SeedOf(s.Winner!));
        var seeds = Rounds.Count == 1 ? Enumerable.Range(1, Direct).Concat(winners) : winners;
        Rounds.Add(Pair(Rounds.Count, seeds));
        return true;
    }

    /// <summary>Gets the seed for one simulated game: fixed per playoffs, round, series and game.</summary>
    /// <param name="round">The round, from 0.</param>
    /// <param name="series">The series in the round, from 0.</param>
    /// <param name="game">The game in the series, from 0.</param>
    /// <returns>The seed.</returns>
    public int GameSeed(int round, int series, int game)
    {
        return unchecked((Seed * 1_000_003) + (((round * 64) + (series * 8) + game) * 7_919));
    }

    FixtureResult Simulate(Dictionary<string, ClubData> byId, PlayoffSeries s, int index)
    {
        var home = byId.GetValueOrDefault(s.NextHome) ?? throw new InvalidOperationException($"club {s.NextHome} from the save has no data file");
        var away = byId.GetValueOrDefault(s.NextAway) ?? throw new InvalidOperationException($"club {s.NextAway} from the save has no data file");
        var r = QuickSim.Play(home, away, GameSeed(Rounds.Count - 1, index, s.Games.Count));

        // a simulated game follows the mode's overtime rules like a played one: with no shootout in
        // the format, a tie is settled in sudden death overtime instead
        return r.Kind == ResultKind.Shootout && !PlayoffSession.Rules.Overtime.HasShootout ? r with { Kind = ResultKind.Overtime } : r;
    }

    PlayoffRound Pair(int round, IEnumerable<int> seeds)
    {
        var order = seeds.Order().ToList();
        var (name, bestOf) = Format[round];
        var result = new PlayoffRound { Name = name, BestOf = bestOf };
        for (int i = 0; i < order.Count / 2; i++)
        {
            int high = order[i], low = order[order.Count - 1 - i];
            result.Series.Add(new PlayoffSeries { HighSeed = high, LowSeed = low, High = Seeds[high - 1], Low = Seeds[low - 1], BestOf = bestOf });
        }
        return result;
    }

    bool Alive()
    {
        // the human's club has not lost a series of the current round, or waits out the play-in as a top six seed
        if (Current.Series.Find(s => s.Involves(UserClub)) is { } series) return !series.IsOver || Same(series.Winner!, UserClub);
        return Rounds.Count == 1 && SeedOf(UserClub) <= Direct;
    }

    static bool Same(string a, string b) => string.Equals(a, b, StringComparison.Ordinal);
}
