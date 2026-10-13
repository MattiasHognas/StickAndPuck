namespace StickAndPuck.App.Core;

/// <summary>
/// W56: the switches dev scenes and tests turn on a world, which were statics. A new world starts
/// with the defaults; the next world the screens start keeps the last one's, as the statics did.
/// </summary>
public sealed class WorldSwitches
{
    /// <summary>Gets or sets the factor on every penalty call chance; tests and SAP_PENALTY_STRICT raise or lower it.</summary>
    public float PenaltyStrictness { get; set; } = 1f;

    /// <summary>Gets or sets the length of an overtime period when the format sets none; the overtime scene (SAP_OT) shortens it.</summary>
    public float OvertimeSeconds { get; set; } = Rules.MatchTuning.OvertimeSeconds;

    /// <summary>Gets or sets how much faster than normal skaters tire and rest; demo scenes and SAP_FATIGUE raise it.</summary>
    public float FatiguePace { get; set; } = Env.Float("SAP_FATIGUE").TryGet(out float pace) && pace > 0 ? pace : 1f;

    /// <summary>Gets or sets where the next zamboni run starts along its route, in seconds; the intermission scene's SAP_ZAMBONI_AT sets it, and the run resets it to 0.</summary>
    public float ZamboniStartAt { get; set; }

    /// <summary>Gets or sets a value indicating whether goals start a celebration.</summary>
    public bool Celebrations { get; set; } = true;

    /// <summary>Gets or sets where the HUD's goal banner reads a club's season goals before this match; tests and the HUD scene replace it.</summary>
    public Func<World, Side, Maybe<int>> SeasonGoals { get; set; } = Hud.HudMessages.SeasonGoalsBefore;

    /// <summary>Gets or sets a value indicating whether AI owner changes are printed (SAP_AI_TRACE=1).</summary>
    public bool AiTrace { get; set; } = Env.Is("SAP_AI_TRACE", "1");

    /// <summary>Gets or sets a hook that sees every AI write as it happens, for the W68 fixtures (a no-op by default).</summary>
    internal Action<Player, AI.AiIntent> AiWatch { get; set; } = static (_, _) => { };    // nobody is watching: a fixture sets its own hook

    /// <summary>Gets or sets a value indicating whether the tactics debug line shows (SAP_TACTICS_LINE=1).</summary>
    public bool TacticsLine { get; set; } = Env.Is("SAP_TACTICS_LINE", "1");

    /// <summary>Gets or sets a value indicating whether the special teams spots are drawn (SAP_ST_SPOTS=1).</summary>
    public bool SpecialTeamsSpots { get; set; } = Env.Is("SAP_ST_SPOTS", "1");

    /// <summary>Gets or sets a value indicating whether the plain score and clock debug line shows (SAP_MATCH_DEBUG=1, the setting, or a rules scene).</summary>
    public bool MatchLine { get; set; } = Env.Is("SAP_MATCH_DEBUG", "1");

    /// <summary>Gets or sets a value indicating whether the energy panel shows (SAP_LINES_DEBUG=1, the setting, or a lines scene).</summary>
    public bool LinesPanel { get; set; } = Env.Is("SAP_LINES_DEBUG", "1");

    /// <summary>Gets or sets a value indicating whether the decision scenes' debug line shows.</summary>
    public bool DecisionLine { get; set; }

    /// <summary>Gets or sets a value indicating whether the HUD shows outside a match too (the HUD scene).</summary>
    public bool HudAlways { get; set; }

    /// <summary>Gets or sets the message the HUD scene shows.</summary>
    public string HudMessage { get; set; } = "";

    /// <summary>Gets or sets a value indicating whether the season table shows over the rink (the season scenes).</summary>
    public bool ScheduleTable { get; set; }

    /// <summary>Gets or sets a value indicating whether the playoff bracket shows over the rink (the playoff scenes).</summary>
    public bool PlayoffBracket { get; set; }

    /// <summary>Gets or sets a value indicating whether the arcade run's summary shows over the rink (the arcade scenes).</summary>
    public bool ArcadeSummary { get; set; }

    /// <summary>Gets a copy, for the next world.</summary>
    /// <returns>The copy.</returns>
    public WorldSwitches Copy() => (WorldSwitches)MemberwiseClone();
}
