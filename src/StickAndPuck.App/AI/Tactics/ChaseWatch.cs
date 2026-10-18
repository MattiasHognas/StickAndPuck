using StickAndPuck.App.Core;

namespace StickAndPuck.App.AI.Tactics
{
    /// <summary>A dump on its way: the side and skater that dumped it, until when it is chased, and whether it has left his stick.</summary>
    internal sealed record ChaseWatch(Side Side, int Dumper, float Until, bool Released);
}
