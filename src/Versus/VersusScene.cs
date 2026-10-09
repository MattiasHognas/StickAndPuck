using StickAndPuck.Core;

namespace StickAndPuck.Versus;

/// <summary>
/// W17: player vs player. Today SAP_SCENE=versus starts the opening lineup with keyboard and mouse
/// on the home centre and the first gamepad on the away centre; W17 adds the setup screen.
/// </summary>
public sealed class VersusScene : IScene
{
    /// <inheritdoc/>
    public string Name => "versus";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var setup = Scenes.DefaultSetup(clubs) with
        {
            Inputs = [new InputSlot(InputDevice.KeyboardMouse, Side.Home), new InputSlot(InputDevice.Gamepad1, Side.Away)],
        };
        var world = new World(setup, settings);
        Scenes.AddStarters(world);
        for (int slot = 0; slot < MatchSetup.Slots; slot++)
        {
            var side = setup.Inputs[slot].Side;
            if (world.OnIce(side).FirstOrDefault(p => !p.IsGoalie) is { } centre) centre.Slot = slot;
        }
        world.Puck.Position = RinkSpec.CentreDot;
        return world;
    }
}
