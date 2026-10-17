namespace StickAndPuck.App.Season;

/// <summary>A played match's final score, shootout goal included, and how it was decided.</summary>
public sealed record FixtureResult(int HomeGoals, int AwayGoals, ResultKind Kind)
{
    /// <summary>Gets the SHL points for the home club.</summary>
    public int HomePoints => Points(HomeGoals, AwayGoals);

    /// <summary>Gets the SHL points for the away club.</summary>
    public int AwayPoints => Points(AwayGoals, HomeGoals);

    int Points(int own, int other)
    {
        if (own == other)
            return 1;

        bool extra = Kind != ResultKind.Regulation;

        if (own > other)
            return extra ? 2 : 3;

        return extra ? 1 : 0;
    }
}
