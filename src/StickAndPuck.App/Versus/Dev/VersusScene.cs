using StickAndPuck.App.Core;
using StickAndPuck.App.Rules;
using StickAndPuck.App.Screens;

namespace StickAndPuck.App.Versus.Dev;

/// <summary>
/// SAP_SCENE=versus: a player vs player match straight away, keyboard and mouse on the home centre
/// and the first gamepad on the away centre (SAP_VERSUS_P2 changes the pad, SAP_HOME and SAP_AWAY
/// the clubs, SAP_PERIOD=seconds the period length, SAP_HUMAN=0 gives both clubs to the AI).
/// </summary>
public sealed class VersusScene : IScene
{
    /// <inheritdoc/>
    public string Name => "versus";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        bool ai = Env.Is("SAP_HUMAN", "0");
        var setup = Scenes.DefaultSetup(clubs) with
        {
            Inputs = VersusMode.Inputs(ai ? InputDevice.None : InputDevice.KeyboardMouse),
            Overtime = OvertimeRule.Shl,
        };
        if (float.TryParse(Env.Get("SAP_PERIOD"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float s) && s > 0)
            setup = setup with { PeriodSeconds = s };
        var world = new World(setup, settings);
        Scenes.AddStarters(world);
        VersusMode.AssignSlots(world);
        MatchFlow.Begin(world);
        Console.WriteLine($"SAP: versus: {setup.Home.Id} ({setup.Inputs[0].Device}) vs {setup.Away.Id} ({setup.Inputs[1].Device})");
        return world;
    }
}

/// <summary>
/// SAP_SCENE=versussetup: the start screen flow opened on player vs player's team select, so the
/// two picks, the match and the results run through W11's screens. SAP_SCREEN_KEYS scripts the
/// picks for headless runs ("enter down enter" picks the first two clubs).
/// </summary>
public sealed class VersusSetupScene : IScene
{
    /// <inheritdoc/>
    public string Name => "versussetup";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var world = ScreenFlow.Begin(clubs, new StartScreen());
        ScreenFlow.Push(new ModeScreen());
        ModeScreen.Choose(new VersusMode());
        Console.WriteLine("SAP: versus: team select for player 1");
        return world;
    }
}
