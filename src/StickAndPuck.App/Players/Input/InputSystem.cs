using StickAndPuck.App.Core;

namespace StickAndPuck.App.Players.Input
{
    /// <summary>Reads each human input slot's device and writes a <see cref="PlayerCommand"/> to the
    /// player that slot controls. There are always <see cref="MatchSetup.Slots"/> slots, so a second
    /// human (player vs player) needs no rework. A lone human on keyboard and mouse can also pick up
    /// the first gamepad, as long as no other slot has claimed it.</summary>
    internal sealed class InputSystem : ISystem
    {
        private readonly DeviceReader[] readers =
        [
            .. Enumerable.Range(0, MatchSetup.Slots).Select(_ => new DeviceReader()),
        ];

        public bool RunsWhilePaused => true;

        public void Update(World world, float dt)
        {
            world.App.Pads.Log();
            IReadOnlyList<InputSlot> inputs = world.Setup.Inputs;
            bool padFree = !inputs.Any(s => s.Device == InputDevice.Gamepad1);
            for (int slot = 0; slot < MatchSetup.Slots && slot < inputs.Count; slot++)
            {
                InputDevice device = inputs[slot].Device;
                DeviceState state = device switch
                {
                    InputDevice.KeyboardMouse when padFree => InputDevices.Merge(
                        InputDevices.KeyboardMouse(world.Settings),
                        InputDevices.Gamepad(world.App.Pads.Slot(0), world.Settings)
                    ),
                    InputDevice.KeyboardMouse => InputDevices.KeyboardMouse(world.Settings),
                    InputDevice.Gamepad1 => InputDevices.Gamepad(
                        world.App.Pads.Slot(0),
                        world.Settings
                    ),
                    InputDevice.Gamepad2 => InputDevices.Gamepad(
                        world.App.Pads.Slot(1),
                        world.Settings
                    ),
                    InputDevice.None => default,
                    _ => throw new NotSupportedException(
                        $"Unhandled input device {device} in input system"
                    ),
                };

                // read every step even with no player, so a press is never carried over to the next one
                PlayerCommand command = readers[slot].Next(state);
                if (device != InputDevice.None && world.Controlled(slot).TryGet(out Player player))
                {
                    player.Command = command;
                }
            }
        }
    }
}
