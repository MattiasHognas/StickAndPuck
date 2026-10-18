using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Arcade;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens
{
    /// <summary>A new arcade run where the first loss ends it.</summary>
    internal sealed class ArcadeSuddenDeathMode : ArcadeMode
    {
        /// <inheritdoc/>
        public override string Title => Text.Get(K.ArcadeSuddenDeath);

        /// <inheritdoc/>
        public override int Order => 21;

        /// <inheritdoc/>
        protected override Maybe<ArcadeRule> Rule => ArcadeRule.SuddenDeath;
    }
}
