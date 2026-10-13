using StickAndPuck.App.Rules;

namespace StickAndPuck.App.Core;

/// <summary>Home or away. Home defends the -x end in the first period.</summary>
public enum Side
{
    /// <summary>The home club, in home colours.</summary>
    Home,

    /// <summary>The visiting club, in away colours.</summary>
    Away,
}

/// <summary>The device a human input slot reads.</summary>
public enum InputDevice
{
    /// <summary>Slot not used: AI plays.</summary>
    None,

    /// <summary>Keyboard and mouse.</summary>
    KeyboardMouse,

    /// <summary>The first gamepad.</summary>
    Gamepad1,

    /// <summary>The second gamepad.</summary>
    Gamepad2,
}

/// <summary>How a tied match is settled.</summary>
public enum OvertimeRule
{
    /// <summary>A tie stands (exhibition and tests).</summary>
    None,

    /// <summary>SHL: 3 on 3 sudden death, then a shootout.</summary>
    Shl,

    /// <summary>Straight to a shootout.</summary>
    ShootoutOnly,
}

/// <summary>One human input slot: which device, and which club it plays for.</summary>
public sealed record InputSlot(InputDevice Device, Side Side);

/// <summary>Everything needed to start a match, so season, arcade and player vs player modes all start one
/// the same way: the two clubs, who is human, period length, the overtime rule and the rules.</summary>
public sealed record MatchSetup
{
    /// <summary>The number of human input slots. Two from the start, so player vs player needs no rework.</summary>
    public const int Slots = 2;

    /// <summary>Gets the home club.</summary>
    public required ClubData Home { get; init; }

    /// <summary>Gets the visiting club.</summary>
    public required ClubData Away { get; init; }

    /// <summary>Gets the two human input slots; a slot with <see cref="InputDevice.None"/> is unused.</summary>
    public IReadOnlyList<InputSlot> Inputs { get; init; } =
        [new InputSlot(InputDevice.KeyboardMouse, Side.Home), new InputSlot(InputDevice.None, Side.Away)];

    /// <summary>Gets the length of one period in game seconds.</summary>
    public float PeriodSeconds { get; init; } = 20 * 60;

    /// <summary>Gets the number of regulation periods.</summary>
    public int Periods { get; init; } = 3;

    /// <summary>Gets how a tie after regulation is settled.</summary>
    public OvertimeRule Overtime { get; init; } = OvertimeRule.Shl;

    /// <summary>Gets the mode's own overtime rules, or None to play the format <see cref="Overtime"/> names.</summary>
    public Maybe<OvertimeFormat> OvertimeFormat { get; init; }

    /// <summary>Gets the rules the match is played by, resolved when it starts; systems read their switches here.</summary>
    public RuleSet Rules { get; init; } = RuleSet.Shl;

    /// <summary>This setup played by a rule set: the set goes on <see cref="Rules"/>, and its overtime
    /// switch picks the format. A setup whose mode pinned its own <see cref="OvertimeFormat"/> is a
    /// playoff game and takes <see cref="RuleSet.PlayoffOvertime"/>, keeping the mode's own
    /// format for sudden death; every other game takes <see cref="RuleSet.Overtime"/>. A setup
    /// whose tie stands (<see cref="OvertimeRule.None"/>, the test scenes) keeps it.</summary>
    public MatchSetup WithRules(RuleSet rules)
    {
        var setup = this with { Rules = rules };
        if (Overtime == OvertimeRule.None && !OvertimeFormat.HasValue) return setup;
        if (OvertimeFormat.TryGet(out var own))
        {
            return setup with
            {
                OvertimeFormat = rules.PlayoffOvertime == OvertimeStyle.SuddenDeath && own.UntilGoal ? own : Format(rules.PlayoffOvertime),
            };
        }
        return rules.Overtime switch
        {
            OvertimeStyle.ThreeOnThree => setup with { Overtime = OvertimeRule.Shl },
            OvertimeStyle.ShootoutOnly => setup with { Overtime = OvertimeRule.ShootoutOnly },
            OvertimeStyle.Tie => setup with { Overtime = OvertimeRule.None },
            _ => setup with { OvertimeFormat = global::StickAndPuck.App.Rules.OvertimeFormat.ShlPlayoffs },
        };
    }

    /// <summary>Gets the club playing on the given side.</summary>
    public ClubData Club(Side side)
    {
        return side == Side.Home ? Home : Away;
    }

    static OvertimeFormat Format(OvertimeStyle style) => style switch
    {
        OvertimeStyle.ThreeOnThree => global::StickAndPuck.App.Rules.OvertimeFormat.Shl,
        OvertimeStyle.ShootoutOnly => global::StickAndPuck.App.Rules.OvertimeFormat.ShootoutOnly,
        OvertimeStyle.Tie => global::StickAndPuck.App.Rules.OvertimeFormat.None,
        _ => global::StickAndPuck.App.Rules.OvertimeFormat.ShlPlayoffs,
    };
}
