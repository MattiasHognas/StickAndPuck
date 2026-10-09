using StickAndPuck.Core;

namespace StickAndPuck.Players;

/// <summary>
/// W4a: reads each human input slot's device and writes a <see cref="PlayerCommand"/> to the
/// player that slot controls. There are always <see cref="MatchSetup.Slots"/> slots, so a second
/// human (player vs player) needs no rework. A lone human on keyboard and mouse can also pick up
/// the first gamepad, as long as no other slot has claimed it.
/// </summary>
internal sealed class InputSystem : ISystem
{
    readonly DeviceReader[] readers = [.. Enumerable.Range(0, MatchSetup.Slots).Select(_ => new DeviceReader())];

    public bool RunsWhilePaused => true;

    public void Update(World world, float dt)
    {
        var inputs = world.Setup.Inputs;
        bool padFree = !inputs.Any(s => s.Device == InputDevice.Gamepad1);
        for (int slot = 0; slot < MatchSetup.Slots && slot < inputs.Count; slot++)
        {
            var device = inputs[slot].Device;
            var state = device switch
            {
                InputDevice.KeyboardMouse when padFree => InputDevices.Merge(InputDevices.KeyboardMouse(), InputDevices.Gamepad(0)),
                InputDevice.KeyboardMouse => InputDevices.KeyboardMouse(),
                InputDevice.Gamepad1 => InputDevices.Gamepad(0),
                InputDevice.Gamepad2 => InputDevices.Gamepad(1),
                _ => default,
            };

            // read every step even with no player, so a press is never carried over to the next one
            var command = readers[slot].Next(state);
            if (device != InputDevice.None && world.Controlled(slot) is { } player) player.Command = command;
        }
    }
}
