using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season;

/// <summary>How many losses an arcade run survives.</summary>
public enum ArcadeRule
{
    /// <summary>Sudden death: the first loss ends the run.</summary>
    SuddenDeath,

    /// <summary>Three lives: the third loss ends the run.</summary>
    ThreeLives,
}

/// <summary>One played arcade game, seen from the human's club.</summary>
/// <param name="Stage">The stage, from 0: how many clubs the human had beaten before this game.</param>
/// <param name="Opponent">The other club's id.</param>
/// <param name="Home">True when the human's club was the home club.</param>
/// <param name="Result">The final score, home goals first, and how it was decided.</param>
public sealed record ArcadeGame(int Stage, string Opponent, bool Home, FixtureResult Result)
{
    /// <summary>Gets the human's goals.</summary>
    public int GoalsFor => Home ? Result.HomeGoals : Result.AwayGoals;

    /// <summary>Gets the opponent's goals.</summary>
    public int GoalsAgainst => Home ? Result.AwayGoals : Result.HomeGoals;

    /// <summary>Gets a value indicating whether the human won; a tie is not a win, so it costs a life.</summary>
    public bool Won => GoalsFor > GoalsAgainst;
}

/// <summary>
/// An arcade run: the human's club meets every other club once, weakest first. A win moves on to
/// the next club; a loss (in overtime or the shootout too) costs a life and the same club is played
/// again. The run ends when the lives are gone, or as champions when every club is beaten. This is
/// exactly what the arcade save holds.
/// </summary>
public sealed class ArcadeRun
{
    /// <summary>The save format version; bump it when the shape changes.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Gets or sets the save format version.</summary>
    public int Version { get; set; } = CurrentVersion;

    /// <summary>Gets or sets the seed that broke ties in the opponent order.</summary>
    public int Seed { get; set; }

    /// <summary>Gets or sets the id of the club the human plays.</summary>
    public string UserClub { get; set; } = "";

    /// <summary>Gets or sets how many losses the run survives.</summary>
    public ArcadeRule Rule { get; set; } = ArcadeRule.ThreeLives;

    /// <summary>Gets or sets the other clubs' ids in the order they are met.</summary>
    public List<string> Opponents { get; set; } = [];

    /// <summary>Gets or sets every game played so far, oldest first.</summary>
    public List<ArcadeGame> Games { get; set; } = [];

    /// <summary>Gets or sets when the run was started (UTC).</summary>
    public DateTime Started { get; set; }

    /// <summary>Gets or sets when the run was last saved (UTC).</summary>
    public DateTime Saved { get; set; }

    /// <summary>Gets or sets every player's totals over this run (W25).</summary>
    public StatTable Stats { get; set; } = new();

    /// <summary>Gets or sets the player of each game of this run, oldest first (W25).</summary>
    public List<PlayerOfGameAward> Awards { get; set; } = [];

    /// <summary>Gets or sets the best runs so far, best first, carried from save to save (W25).</summary>
    public List<ArcadeRecord> Records { get; set; } = [];

    /// <summary>Gets this run's place on <see cref="Records"/>, from 1, or 0 while it is not on the board.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int Place { get; set; }

    /// <summary>Gets the lives the run starts with: 1 in sudden death, 3 with three lives.</summary>
    public int StartLives => Rule == ArcadeRule.SuddenDeath ? 1 : 3;

    /// <summary>Gets the games lost.</summary>
    public int Losses => Games.Count(g => !g.Won);

    /// <summary>Gets the lives left.</summary>
    public int LivesLeft => Math.Max(0, StartLives - Losses);

    /// <summary>Gets the clubs beaten, which is also the stage being played, from 0.</summary>
    public int Stage => Games.Count(g => g.Won);

    /// <summary>Gets a value indicating whether the run ended on a loss.</summary>
    public bool Lost => LivesLeft == 0;

    /// <summary>Gets a value indicating whether every club was beaten.</summary>
    public bool Champion => !Lost && Opponents.Count > 0 && Stage >= Opponents.Count;

    /// <summary>Gets a value indicating whether the run is over, either way.</summary>
    public bool IsOver => Lost || Champion;

    /// <summary>Gets the next opponent's id, or None when the run is over.</summary>
    public Maybe<string> NextOpponent => IsOver ? Maybe<string>.None : Opponents[Stage];

    /// <summary>Gets a value indicating whether the human's club is at home in the next game: home on even stages.</summary>
    public bool NextAtHome => Stage % 2 == 0;

    /// <summary>Starts a run against every other club, weakest first.</summary>
    /// <param name="clubs">Every club from data/clubs/.</param>
    /// <param name="userClub">The id of the human's club.</param>
    /// <param name="rule">Sudden death or three lives.</param>
    /// <param name="seed">Breaks ties between clubs of about the same strength.</param>
    /// <returns>The new run, nothing played.</returns>
    public static ArcadeRun New(IReadOnlyList<ClubData> clubs, string userClub, ArcadeRule rule, int seed)
    {
        if (!clubs.Any(c => string.Equals(c.Id, userClub, StringComparison.Ordinal))) throw new ArgumentException($"{userClub} is not a club", nameof(userClub));
#pragma warning disable S2245 // seeded order, not security
        var dice = new Random(seed);
#pragma warning restore S2245
        var order = clubs.Where(c => !string.Equals(c.Id, userClub, StringComparison.Ordinal))
            .Select(c => (c.Id, Key: Strength(c) + (dice.NextDouble() * 2)))
            .OrderBy(c => c.Key)
            .Select(c => c.Id)
            .ToList();
        return new ArcadeRun { Seed = seed, UserClub = userClub, Rule = rule, Opponents = order, Started = DateTime.UtcNow };
    }

    /// <summary>Gets a club's strength: the average of every trait across its roster, about 0 to 100.</summary>
    /// <param name="club">The club.</param>
    /// <returns>The strength.</returns>
    public static double Strength(ClubData club)
    {
        var skaters = club.Roster.Where(p => p.Position != PlayingPosition.G).Select(p => p.Traits).ToList();
        double s = skaters.Count == 0 ? 50 : skaters.Average(t => (t.Acceleration + t.TopSpeed + t.ShotAccuracy + t.ShotPower + t.Checking + t.PuckControl + t.HockeyIq) / 7.0);
        double g = club.Roster.Where(p => p.Position == PlayingPosition.G).Select(p => (double)p.Traits.GoalieReflexes).DefaultIfEmpty(50).Max();
        return ((s * 5) + g) / 6;
    }

    /// <summary>Credits a played game's lines and its player of the game to this run's stats.</summary>
    /// <param name="sheet">The match sheet.</param>
    public void Credit(MatchStatSheet sheet)
    {
        Stats.Add(sheet);
        if (sheet.Award($"a{Games.Count}").TryGet(out var award)) Awards.Add(award);
    }

    /// <summary>Records the next game's result.</summary>
    /// <param name="result">The final score, home goals first.</param>
    /// <returns>The game as recorded.</returns>
    public ArcadeGame Record(FixtureResult result)
    {
        if (IsOver) throw new InvalidOperationException("the arcade run is over");
        string opponent = Opponents[Stage];
        var game = new ArcadeGame(Stage, opponent, NextAtHome, result);
        Games.Add(game);
        return game;
    }

    /// <summary>The setup for the next game: the human on their club, the AI on the other, SHL overtime so there is always a winner.</summary>
    /// <param name="clubs">Every club from data/clubs/.</param>
    /// <param name="device">The human's input device.</param>
    /// <returns>The match to play, or <see cref="NextMatch.Over"/> when the run is over.</returns>
    public NextMatch NextSetup(IReadOnlyList<ClubData> clubs, InputDevice device = InputDevice.KeyboardMouse)
    {
        if (IsOver) return NextMatch.Over;
        string opponent = Opponents[Stage];
        ClubData Find(string id) => clubs.FirstOrNone(c => string.Equals(c.Id, id, StringComparison.Ordinal)).TryGet(out var club) ? club : throw new InvalidOperationException($"club {id} from the arcade save has no data file");
        var user = Find(UserClub);
        var other = Find(opponent);
        var side = NextAtHome ? Side.Home : Side.Away;
        return new NextMatch.Play(new MatchSetup
        {
            Home = NextAtHome ? user : other,
            Away = NextAtHome ? other : user,
            Inputs = [new InputSlot(device, side), new InputSlot(InputDevice.None, side == Side.Home ? Side.Away : Side.Home)],
            Overtime = OvertimeRule.Shl,
        });
    }
}
