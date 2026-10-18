namespace StickAndPuck.App.AI.Tactics
{
    /// <summary>A dump decided on: the carrier and his run with the puck, and the side of the ice he takes it wide on.</summary>
    internal sealed record DumpPlan(int Player, float GotAt, float Wide);
}
