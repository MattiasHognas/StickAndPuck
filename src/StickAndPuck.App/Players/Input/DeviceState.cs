using System.Numerics;

namespace StickAndPuck.App.Players.Input
{
    /// <summary>The buttons a device reports in one step, before presses are found.</summary>
    internal struct DeviceState
    {
        public Vector2 Move;
        public bool Sprint,
            Shoot,
            Pass,
            Check,
            Deke,
            Switch,
            Line,
            Punch,
            Block,
            Accept,
            Pause;
    }
}
