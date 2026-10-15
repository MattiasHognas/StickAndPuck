using StickAndPuck.App.Core;

namespace StickAndPuck.App.AI;

/// <summary>Easy, normal and hard. The difficulty scales the traits of the club no human plays for the
/// whole match: hockey IQ (every AI read rolls against it), shooting, puck control, skating,
/// checking and the goalie. Everything that reads traits then plays weaker or sharper on its own.
/// Normal keeps the data files' skater traits; the AI goalie is scaled on every difficulty, normal
/// included, so a human beats it about as often as a human expects to score. Versus and all-AI matches
/// are never scaled, because there is no single AI club facing a human. The World constructor runs every match's setup through
/// <see cref="Apply(MatchSetup, Difficulty)"/>.</summary>
public static class DifficultyScale
{
    /// <summary>Gets the AI club a difficulty applies to: the one side no human plays, or None when both or neither have a human.</summary>
    public static SideOrNone AiSide(MatchSetup setup)
    {
        bool home = setup.Inputs.Any(i => i.Device != InputDevice.None && i.Side == Side.Home);
        bool away = setup.Inputs.Any(i => i.Device != InputDevice.None && i.Side == Side.Away);
        if (home == away) return SideOrNone.None;
        return home ? SideOrNone.Away : SideOrNone.Home;
    }

    /// <summary>Scales the AI club of a match by the difficulty.</summary>
    /// <returns>The setup with the AI club's traits scaled, or the same setup on normal or with no single AI club.</returns>
    public static MatchSetup Apply(MatchSetup setup, Difficulty level)
    {
        return AiSide(setup).TryGetSide(out var side) ? Apply(setup, side, level) : setup;
    }

    /// <summary>Scales one club of a match by a difficulty; the difficulty test plays hard against easy this way.</summary>
    public static MatchSetup Apply(MatchSetup setup, Side side, Difficulty level)
    {
        return side == Side.Home ? setup with { Home = Scale(setup.Home, level) } : setup with { Away = Scale(setup.Away, level) };
    }

    /// <summary>Scales every player of a club: skaters by <see cref="Scale(Traits, Difficulty)"/>, goalies by <see cref="Goalie"/>.</summary>
    public static ClubData Scale(ClubData club, Difficulty level)
    {
        return club with { Roster = [.. club.Roster.Select(p => p with { Traits = p.Position == PlayingPosition.G ? Goalie(p.Traits, level) : Scale(p.Traits, level) })] };
    }

    /// <summary>Scales a goalie's traits: every goalie trait comes down by the difficulty's goalie step, and
    /// positioning (hockey IQ) comes down on easy only. Club goalies keep their order.</summary>
    public static Traits Goalie(Traits t, Difficulty level)
    {
        int step = level switch
        {
            Difficulty.Easy => DifficultyTuning.GoalieEasy,
            Difficulty.Hard => DifficultyTuning.GoalieHard,
            _ => DifficultyTuning.GoalieNormal,
        };
        int Down(int value, int by) => Math.Clamp(value - by, 0, 100);
        return t with
        {
            HockeyIq = Down(t.HockeyIq, level == Difficulty.Easy ? DifficultyTuning.HockeyIq : 0),
            GoalieReflexes = Down(t.GoalieReflexes, step),
            GoalieGlove = Down(t.GoalieGlove, step),
            GoalieBlocker = Down(t.GoalieBlocker, step),
            GoalieReboundControl = Down(t.GoalieReboundControl, step),
            GoaliePuckHandling = Down(t.GoaliePuckHandling, step),
        };
    }

    /// <summary>Scales a skater's traits: each moves by its own step, down on easy and up on hard, kept within 0 to 100.</summary>
    public static Traits Scale(Traits t, Difficulty level)
    {
        float k = level switch
        {
            Difficulty.Easy => -1f,
            Difficulty.Hard => DifficultyTuning.HardShare,
            _ => 0f,
        };
        if (k == 0f) return t;
        int By(int value, int step) => Math.Clamp(value + (int)MathF.Round(step * k), 0, 100);
        return t with
        {
            HockeyIq = By(t.HockeyIq, DifficultyTuning.HockeyIq),
            ShotAccuracy = By(t.ShotAccuracy, DifficultyTuning.ShotAccuracy),
            ShotPower = By(t.ShotPower, DifficultyTuning.ShotPower),
            PuckControl = By(t.PuckControl, DifficultyTuning.PuckControl),
            TopSpeed = By(t.TopSpeed, DifficultyTuning.Skating),
            Acceleration = By(t.Acceleration, DifficultyTuning.Skating),
            Checking = By(t.Checking, DifficultyTuning.Checking),
        };
    }
}

/// <summary>How far easy moves each skater trait down, on the 0 to 100 scale (hard moves them up by <see cref="HardShare"/> of that), and how far each difficulty takes the AI goalie down.</summary>
public static class DifficultyTuning
{
    /// <summary>Hockey IQ: easy reads the play worse and fumbles more often.</summary>
    public const int HockeyIq = 20;

    /// <summary>Shot accuracy.</summary>
    public const int ShotAccuracy = 15;

    /// <summary>Shot power.</summary>
    public const int ShotPower = 10;

    /// <summary>Puck control: keeping and receiving the puck.</summary>
    public const int PuckControl = 12;

    /// <summary>Top speed and acceleration.</summary>
    public const int Skating = 8;

    /// <summary>Checking.</summary>
    public const int Checking = 10;

    /// <summary>How far easy takes every trait of the AI goalie down: a human's shot on target goes in about a third of the time.</summary>
    public const int GoalieEasy = 45;

    /// <summary>How far normal takes every trait of the AI goalie down: a human's shot on target goes in about one time in six.</summary>
    public const int GoalieNormal = 15;

    /// <summary>How far hard takes every trait of the AI goalie down: about one shot on target in nine goes in.</summary>
    public const int GoalieHard = 5;

    /// <summary>Hard moves each trait up by this share of easy's step, so hard is a sharper club rather than a club of stars.</summary>
    public const float HardShare = 0.75f;
}
