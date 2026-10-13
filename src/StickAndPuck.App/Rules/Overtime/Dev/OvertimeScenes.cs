using System.Globalization;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Rules.Dev;

/// <summary>SAP_SCENE=overtime: a match that ends tied, to watch what follows. Regulation is short
/// (SAP_PERIOD, default 5 seconds a period) and nobody shoots, so it ends 0-0; then the 3 on 3
/// overtime (SAP_OT seconds, default 15) and the shootout with AI shooters. SAP_HUMAN=1 puts
/// keyboard and mouse on the home club, so you take the home shots yourself. SAP_START=overtime or
/// shootout skips regulation (and overtime) to start right there. SAP_PLAYOFF=1 plays the SHL
/// playoff format instead: 5 on 5 overtime periods until a goal and no shootout. SAP_RULES=shl|arcade|nhl
/// plays that preset's shootout (SHL away first, NHL home chooses). The score line is on.</summary>
public sealed class OvertimeScene : IScene
{
    /// <inheritdoc/>
    public string Name => "overtime";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        float period = Seconds("SAP_PERIOD", 5);
        float overtime = Seconds("SAP_OT", 15);
        bool human = Env.Is("SAP_HUMAN", "1");
        bool playoff = Env.Is("SAP_PLAYOFF", "1");
        var setup = Scenes.DefaultSetup(clubs) with { PeriodSeconds = period, Overtime = OvertimeRule.Shl };
        if (Enum.TryParse<RulePreset>(Env.Get("SAP_RULES"), ignoreCase: true, out var preset)) setup = setup with { Rules = RuleSet.Of(preset) };
        if (playoff) setup = setup with { OvertimeFormat = OvertimeFormat.ShlPlayoffs with { PeriodSeconds = overtime } };
        if (!human) setup = setup with { Inputs = [new InputSlot(InputDevice.None, Side.Home), new InputSlot(InputDevice.None, Side.Away)] };
        var world = new World(setup, settings);
        world.Switches.OvertimeSeconds = overtime;
        Scenes.AddStarters(world);
        if (human && world.OnIce(Side.Home).FirstOrDefault(p => p.Data.Position == PlayingPosition.C) is { } centre) centre.Slot = 0;
        MatchFlow.Begin(world);
        string start = Env.Get("SAP_START");
        bool shootout = string.Equals(start, "shootout", StringComparison.Ordinal);
        if (shootout || string.Equals(start, "overtime", StringComparison.Ordinal))
        {
            // as if regulation (and the overtime) just ended 0-0
            world.Match.Period = setup.Periods + (shootout ? 1 : 0);
            world.Match.Clock = 0;
            world.Match.Phase = shootout ? MatchPhase.Shootout : MatchPhase.Overtime;
        }

        world.Switches.MatchLine = true;
        return world;
    }

    static float Seconds(string variable, float fallback)
    {
        return float.TryParse(Env.Get(variable), NumberStyles.Float, CultureInfo.InvariantCulture, out float s) && s > 0 ? s : fallback;
    }
}
