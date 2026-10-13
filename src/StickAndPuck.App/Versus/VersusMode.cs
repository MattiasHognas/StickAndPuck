using StickAndPuck.App.Core;
using StickAndPuck.App.Screens;

namespace StickAndPuck.App.Versus;

/// <summary>
/// W17: player vs player, the "new game" entry for two people in the same room. Player one plays
/// with the device from the settings (keyboard and mouse by default) and picks the home club,
/// player two plays with the first gamepad (or the keyboard when player one holds that pad) and
/// picks the visiting club. Exhibition rules: periods from the settings, SHL overtime and a
/// shootout. SAP_VERSUS_P2=keyboard|pad1|pad2 overrides player two's device.
/// </summary>
public sealed class VersusMode : IGameMode
{
    /// <inheritdoc/>
    public string Title => "PLAYER VS PLAYER";

    /// <inheritdoc/>
    public int Order => 10;

    /// <inheritdoc/>
    public int ClubPicks => 2;

    /// <inheritdoc/>
    public string PickHeading(int index)
    {
        // W38: each player sees the device they play with, the pad by name
        var device = index == 0 ? Screens.ScreenFlow.Prefs.Controls : SecondDevice(Screens.ScreenFlow.Prefs.Controls);
        return $"PLAYER {index + 1} ({Players.Gamepads.Label(device)}): PICK YOUR CLUB";
    }

    /// <inheritdoc/>
    public MatchSetup? Setup(GameModeStart start)
    {
        var home = start.Picks.Count > 0 ? start.Picks[0] : start.Clubs[0];
        var away = start.Picks.Count > 1 ? start.Picks[1] : start.Clubs.FirstOrDefault(c => c != home) ?? home;
        return new MatchSetup
        {
            Home = home,
            Away = away,
            Inputs = Inputs(start.Device),
            Overtime = OvertimeRule.Shl,
        };
    }

    /// <summary>
    /// The two human slots: player one on <paramref name="playerOne"/> and the home club, player
    /// two on <see cref="SecondDevice"/> and the visitors. <see cref="InputDevice.None"/> for
    /// player one (SAP_HUMAN=0) lets the AI play both clubs, so headless runs go through.
    /// </summary>
    /// <param name="playerOne">Player one's device.</param>
    /// <returns>The slots, home first.</returns>
    public static IReadOnlyList<InputSlot> Inputs(InputDevice playerOne)
    {
        var playerTwo = playerOne == InputDevice.None ? InputDevice.None : SecondDevice(playerOne);
        return [new InputSlot(playerOne, Side.Home), new InputSlot(playerTwo, Side.Away)];
    }

    /// <summary>Player two's device: the settings screen's pick (default the first gamepad) unless player one holds it, then the keyboard; SAP_VERSUS_P2 overrides, and OFF hands the visitors to the AI.</summary>
    /// <param name="playerOne">Player one's device.</param>
    /// <returns>A device different from player one's.</returns>
    public static InputDevice SecondDevice(InputDevice playerOne)
    {
        var wanted = Environment.GetEnvironmentVariable("SAP_VERSUS_P2")?.ToLowerInvariant() switch
        {
            "keyboard" or "kb" or "keyboardmouse" => InputDevice.KeyboardMouse,
            "pad1" or "gamepad1" or "gamepad" => InputDevice.Gamepad1,
            "pad2" or "gamepad2" => InputDevice.Gamepad2,
            _ => (InputDevice?)null,
        };
        wanted ??= Screens.ScreenFlow.Prefs.Player2;    // W20: the settings screen's PLAYER 2 CONTROLS
        if (wanted == InputDevice.None) return InputDevice.None;    // player two turned off: the AI plays the visitors
        if (wanted is { } w && w != playerOne) return w;
        return playerOne == InputDevice.Gamepad1 ? InputDevice.KeyboardMouse : InputDevice.Gamepad1;
    }

    /// <summary>Gives each human slot a skater on its club: the centre, or the first skater free.</summary>
    /// <param name="world">A world with the starters on the ice.</param>
    public static void AssignSlots(World world)
    {
        var inputs = world.Setup.Inputs;
        for (int slot = 0; slot < inputs.Count && slot < MatchSetup.Slots; slot++)
        {
            if (inputs[slot].Device == InputDevice.None) continue;
            var skaters = world.OnIce(inputs[slot].Side).Where(p => !p.IsGoalie && p.Slot < 0).ToList();
            if ((skaters.Find(p => p.Data.Position == PlayingPosition.C) ?? skaters.FirstOrDefault()) is { } centre) centre.Slot = slot;
        }
    }
}
