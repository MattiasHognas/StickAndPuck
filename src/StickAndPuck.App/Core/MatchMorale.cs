namespace StickAndPuck.App.Core
{
    /// <summary>Both clubs' team morale, 0 to 100 (50 is neutral), as a season game starts; the momentum meter
    /// starts from it when <see cref="RuleSet.Morale"/> is on.</summary>
    /// <param name="Home">The home club's morale.</param>
    /// <param name="Away">The away club's morale.</param>
    public sealed record MatchMorale(int Home, int Away)
    {
        /// <summary>Gets one club's morale.</summary>
        public int Of(Side side)
        {
            return side == Side.Home ? Home : Away;
        }
    }
}
