using StickAndPuck.App.Core;

namespace StickAndPuck.App.AI.SpecialTeams
{
    /// <summary>A clear on its way: who fired it, whether it was flipped, when it was decided and when it left the stick (below 0 until then).</summary>
    internal sealed record ClearWatch(
        Side Side,
        int Player,
        bool Flip,
        float At,
        float Released = -1f
    );
}
