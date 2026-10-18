namespace StickAndPuck.App.Core
{
    /// <summary>A penalty being served.</summary>
    public sealed record PenaltyState(
        int PlayerId,
        Side Side,
        string Infraction,
        float SecondsLeft
    );
}
