using StickAndPuck.App.Core;

namespace StickAndPuck.App.AI;

/// <summary>Easy, normal and hard. The difficulty scales the traits of the club no human plays for the
/// whole match: hockey IQ (every AI read rolls against it), shooting, puck control, skating,
/// checking and the goalie. Everything that reads traits then plays weaker or sharper on its own.
/// Normal keeps the data files' traits; versus and all-AI matches are never scaled, because there is
/// no single AI club facing a human. The World constructor runs every match's setup through
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
        if (level == Difficulty.Normal) return setup;
        return side == Side.Home ? setup with { Home = Scale(setup.Home, level) } : setup with { Away = Scale(setup.Away, level) };
    }

    /// <summary>Scales every player of a club.</summary>
    public static ClubData Scale(ClubData club, Difficulty level)
    {
        if (level == Difficulty.Normal) return club;
        return club with { Roster = [.. club.Roster.Select(p => p with { Traits = Scale(p.Traits, level) })] };
    }

    /// <summary>Scales one player's traits: each moves by its own step, down on easy and up on hard, kept within 0 to 100.</summary>
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
        int goalie = level == Difficulty.Easy ? DifficultyTuning.GoalieEasy : DifficultyTuning.Goalie;
        return t with
        {
            HockeyIq = By(t.HockeyIq, DifficultyTuning.HockeyIq),
            ShotAccuracy = By(t.ShotAccuracy, DifficultyTuning.ShotAccuracy),
            ShotPower = By(t.ShotPower, DifficultyTuning.ShotPower),
            PuckControl = By(t.PuckControl, DifficultyTuning.PuckControl),
            TopSpeed = By(t.TopSpeed, DifficultyTuning.Skating),
            Acceleration = By(t.Acceleration, DifficultyTuning.Skating),
            Checking = By(t.Checking, DifficultyTuning.Checking),
            GoalieReflexes = By(t.GoalieReflexes, goalie),
            GoalieGlove = By(t.GoalieGlove, goalie),
            GoalieBlocker = By(t.GoalieBlocker, goalie),
            GoalieReboundControl = By(t.GoalieReboundControl, goalie),
            GoaliePuckHandling = By(t.GoaliePuckHandling, goalie),
        };
    }
}

/// <summary>How far easy moves each trait down, on the 0 to 100 scale; hard moves them up by <see cref="HardShare"/> of that.</summary>
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

    /// <summary>Every goalie trait.</summary>
    public const int Goalie = 10;

    /// <summary>Every goalie trait on easy, a bigger step than the rest so an easy goalie is clearly beatable.</summary>
    public const int GoalieEasy = 20;

    /// <summary>Hard moves each trait up by this share of easy's step, so hard is a sharper club rather than a club of stars.</summary>
    public const float HardShare = 0.75f;
}
