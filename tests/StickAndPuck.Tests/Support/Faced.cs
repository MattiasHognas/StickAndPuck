namespace StickAndPuck.Tests.Support
{
    /// <summary>One goalie's shots faced in one match.</summary>
    public sealed record Faced(string Goalie, int Reflexes, int Saves, int Goals);
}
