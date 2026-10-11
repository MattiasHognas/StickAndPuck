using System.Globalization;
using StickAndPuck.Core;

namespace StickAndPuck.Rules;

/// <summary>
/// SAP_SCENE=overtime: a match that ends tied, to watch what follows. Regulation is short
/// (SAP_PERIOD, default 5 seconds a period) and nobody shoots, so it ends 0-0; then the 3 on 3
/// overtime (SAP_OT seconds, default 15) and the shootout with AI shooters. SAP_HUMAN=1 puts
/// keyboard and mouse on the home club, so you take the home shots yourself. SAP_START=overtime or
/// shootout skips regulation (and overtime) to start right there. SAP_PLAYOFF=1 plays the SHL
/// playoff format instead: 5 on 5 overtime periods until a goal and no shootout. The score line is on.
/// </summary>
public sealed class OvertimeScene : IScene
{
    /// <inheritdoc/>
    public string Name => "overtime";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        float period = Seconds("SAP_PERIOD", 5);
        OvertimeRules.PeriodSeconds = Seconds("SAP_OT", 15);
        bool human = string.Equals(Environment.GetEnvironmentVariable("SAP_HUMAN"), "1", StringComparison.Ordinal);
        bool playoff = string.Equals(Environment.GetEnvironmentVariable("SAP_PLAYOFF"), "1", StringComparison.Ordinal);
        var setup = Scenes.DefaultSetup(clubs) with { PeriodSeconds = period, Overtime = OvertimeRule.Shl };
        if (playoff) setup = setup with { OvertimeFormat = OvertimeFormat.ShlPlayoffs with { PeriodSeconds = OvertimeRules.PeriodSeconds } };
        if (!human) setup = setup with { Inputs = [new InputSlot(InputDevice.None, Side.Home), new InputSlot(InputDevice.None, Side.Away)] };
        var world = new World(setup, settings);
        Scenes.AddStarters(world);
        if (human && world.OnIce(Side.Home).FirstOrDefault(p => p.Data.Position == PlayingPosition.C) is { } centre) centre.Slot = 0;
        MatchFlow.Begin(world);
        string? start = Environment.GetEnvironmentVariable("SAP_START");
        bool shootout = string.Equals(start, "shootout", StringComparison.Ordinal);
        if (shootout || string.Equals(start, "overtime", StringComparison.Ordinal))
        {
            // as if regulation (and the overtime) just ended 0-0
            world.Match.Period = setup.Periods + (shootout ? 1 : 0);
            world.Match.Clock = 0;
            world.Match.Phase = shootout ? MatchPhase.Shootout : MatchPhase.Overtime;
        }

        MatchRules.ShowLine = true;
        return world;
    }

    static float Seconds(string variable, float fallback)
    {
        return float.TryParse(Environment.GetEnvironmentVariable(variable), NumberStyles.Float, CultureInfo.InvariantCulture, out float s) && s > 0 ? s : fallback;
    }
}
