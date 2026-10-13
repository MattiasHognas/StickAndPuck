using StickAndPuck.App.Core;
using StickAndPuck.App.Rules;

namespace StickAndPuck.App.Players;

/// <summary>
/// SAP_SCENE=padtest (W38): the exhibition from SAP_SCENE=match with you on the home centre, and
/// twice a second every Raylib gamepad slot is printed: the pad's name, its axes and the buttons
/// held. Pads plugged in or out while it runs show up as they come and go.
/// </summary>
public sealed class PadTestScene : IScene
{
    /// <inheritdoc/>
    public string Name => "padtest";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        Gamepads.Logging = true;
        var world = MatchScenes.Exhibition(clubs, settings, 20 * 60);
        if (world.OnIce(Side.Home).FirstOrDefault(p => p.Data.Position == PlayingPosition.C) is { } centre) centre.Slot = 0;
        return world;
    }
}
