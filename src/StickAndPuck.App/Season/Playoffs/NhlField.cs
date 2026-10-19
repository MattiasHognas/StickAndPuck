namespace StickAndPuck.App.Season.Playoffs
{
    /// <summary>The NHL playoff field: the first-round pairs in bracket order and every qualifier's tag.</summary>
    /// <param name="Pairs">The first-round series, in bracket order; neighbours meet in the next round.</param>
    /// <param name="Tags">Each qualifier's tag by club id (A1, M3, WC2).</param>
    /// <param name="Conferences">The conferences in bracket order.</param>
    public sealed record NhlField(
        IReadOnlyList<(string First, string Second)> Pairs,
        IReadOnlyDictionary<string, string> Tags,
        IReadOnlyList<string> Conferences
    );
}
