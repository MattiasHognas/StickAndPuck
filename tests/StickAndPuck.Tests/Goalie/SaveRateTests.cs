using StickAndPuck.Tests.Support;
using TUnit.Core.Enums;

namespace StickAndPuck.Tests;

/// <summary>
/// How often shots go in: the full all-AI matches between the clubs (<see cref="AllAiMatches"/>, which
/// <see cref="MovesTests"/> judges too), counting shots on goal, goals and every starting goalie's saves. The
/// "done when" of the save tuning is about one shot in ten going in; that the best club goalies save
/// clearly more than the weakest is checked shot by shot in <see cref="HumanShotTests.BetterGoalieSavesMore"/>.
/// </summary>
public sealed class SaveRateTests : CheckedTest
{
    /// <summary>Gets the matches' results, shared by the class's tests.</summary>
    [ClassDataSource<Played<AllAiMatch>>(Shared = SharedType.PerClass)]
    public required Played<AllAiMatch> MatchesPlayed { get; init; }

    /// <summary>Gets the matches.</summary>
    public static IEnumerable<int> MatchNumbers() => AllAiMatches.Numbers();

    /// <summary>One full match; <see cref="ShootingPercentage"/> judges them all.</summary>
    [Test]
    [ExecutionPriority(Priority.Critical)]
    [MethodDataSource(nameof(MatchNumbers))]
    public void PlayMatch(int m) => MatchesPlayed.Add(m, AllAiMatches.Get(m));

    /// <summary>About one shot on goal in ten goes in.</summary>
    [Test]
    [DependsOn(nameof(PlayMatch), [typeof(int)])]
    public void ShootingPercentage()
    {
        var played = MatchesPlayed.All(AllAiMatches.Count);
        int shots = played.Sum(p => p.Shots), goals = played.Sum(p => p.Goals);
        float shooting = (float)goals / Math.Max(1, shots);
        var lines = played.SelectMany(p => p.Goalies).GroupBy(f => f.Goalie)
            .Select(g => (Goalie: g.Key, g.First().Reflexes, Saves: g.Sum(f => f.Saves), Goals: g.Sum(f => f.Goals)))
            .Where(g => g.Saves + g.Goals >= 20)
            .OrderByDescending(g => g.Reflexes).ToList();
        foreach (var g in lines) Console.WriteLine($"      {g.Goalie} reflexes {g.Reflexes}: {g.Saves} saves, {g.Goals} goals, {Pct(g.Saves, g.Goals):P1}");
        Console.WriteLine($"      {played.Length} matches: {goals} goals on {shots} shots on goal, {shooting:P1}, {(float)goals / played.Length:F1} goals a match");
        Console.WriteLine($"      {played.Sum(p => p.Caught)} of {played.Sum(p => p.Saves)} saves held ({(float)played.Sum(p => p.Caught) / Math.Max(1, played.Sum(p => p.Saves)):P0})");
        Check(shooting is > 0.07f and < 0.13f, $"about one shot on goal in ten goes in ({shooting:P1})");
    }

    static float Pct(int saves, int goals) => (float)saves / Math.Max(1, saves + goals);
}
