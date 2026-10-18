using StickAndPuck.App.Core;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens
{
    /// <summary>A single match between two picked clubs, the player on the home club, SHL overtime.</summary>
    public sealed class ExhibitionMode : IGameMode
    {
        /// <inheritdoc/>
        public string Title => Text.Get(K.Exhibition);

        /// <inheritdoc/>
        public int Order => 0;

        /// <inheritdoc/>
        public Maybe<MatchSetup> Setup(GameModeStart start)
        {
            ClubData home = start.Picks.Count > 0 ? start.Picks[0] : start.Clubs[0];
            ClubData away =
                start.Picks.Count > 1
                    ? start.Picks[1]
                    : start.Clubs.FirstOrDefault(c => c != home) ?? home;
            return new MatchSetup
            {
                Home = home,
                Away = away,
                Inputs =
                [
                    new InputSlot(start.Device, Side.Home),
                    new InputSlot(InputDevice.None, Side.Away),
                ],
                Overtime = OvertimeRule.Shl,
            };
        }
    }
}
