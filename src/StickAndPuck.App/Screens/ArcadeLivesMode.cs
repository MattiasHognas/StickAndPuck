using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Arcade;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens
{
    /// <summary>A new arcade run with three lives.</summary>
    internal sealed class ArcadeLivesMode : ArcadeMode
    {
        /// <inheritdoc/>
        public override string Title => Text.Get(K.ArcadeThreeLives);

        /// <inheritdoc/>
        protected override Maybe<ArcadeRule> Rule => ArcadeRule.ThreeLives;
    }
}
