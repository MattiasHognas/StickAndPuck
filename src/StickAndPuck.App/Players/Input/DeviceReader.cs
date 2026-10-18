using StickAndPuck.App.Core;

namespace StickAndPuck.App.Players.Input
{
    /// <summary>Turns one input slot's device into a <see cref="PlayerCommand"/>. Presses are found by
    /// comparing held buttons with the previous fixed step rather than with Raylib's per-frame
    /// pressed flags, so a press is seen exactly once whether a frame runs no step or several.</summary>
    internal sealed class DeviceReader
    {
        private DeviceState last;

        /// <summary>Reads the device and builds this step's command.</summary>
        /// <returns>The command, with Pass, Check, Switch, Line, Punch, Accept and Pause true only on the step they went down.</returns>
        public PlayerCommand Next(DeviceState now)
        {
            PlayerCommand command = new()
            {
                Move = now.Move,
                Sprint = now.Sprint,
                Shoot = now.Shoot,
                Block = now.Block,
                Pass = now.Pass && !last.Pass,
                Check = now.Check && !last.Check,
                Deke = now.Deke,
                SwitchPlayer = now.Switch && !last.Switch,
                ChangeLine = now.Line && !last.Line,
                Punch = now.Punch && !last.Punch,
                AcceptFight = now.Accept && !last.Accept,
                Pause = now.Pause && !last.Pause,
            };
            last = now;
            return command;
        }
    }
}
