using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Arcade;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens
{
    /// <summary>Continues the saved arcade run; shown only when there is one still going.</summary>
    internal sealed class ArcadeContinueMode : ArcadeMode
    {
        /// <inheritdoc/>
        public override string Title => Text.Get(K.ArcadeContinue);

        /// <inheritdoc/>
        public override int Order => 19;

        /// <inheritdoc/>
        public override int ClubPicks => 0;

        /// <inheritdoc/>
        public override bool Available(AppState app)
        {
            return ArcadeSession.CanContinue();
        }

        /// <inheritdoc/>
        protected override Maybe<ArcadeRule> Rule => Maybe.None<ArcadeRule>();
    }
}
