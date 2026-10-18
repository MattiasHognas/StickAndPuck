using StickAndPuck.App.Core;

namespace StickAndPuck.App.Players.Checks.Dev
{
    /// <summary>The four skaters a checks scene adds.</summary>
    internal sealed record ChecksCast(Player Carrier, Player Hunter, Player Mate1, Player Mate2);
}
