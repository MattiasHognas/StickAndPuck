using StickAndPuck.App.Core;

namespace StickAndPuck.App.AI.Difficulty
{
    /// <summary>Easy, normal and hard. The difficulty scales the traits of the club no human plays for the
    /// whole match: hockey IQ (every AI read rolls against it), shooting, puck control, skating,
    /// checking and the goalie. Everything that reads traits then plays weaker or sharper on its own.
    /// Normal keeps the data files' skater traits; the AI goalie is scaled on every difficulty, normal
    /// included, so a human beats it about as often as a human expects to score. Versus and all-AI matches
    /// are never scaled, because there is no single AI club facing a human. The World constructor runs every match's setup through
    /// <see cref="Apply(MatchSetup, Core.Difficulty)"/>.</summary>
    public static class DifficultyScale
    {
        /// <summary>Gets the AI club a difficulty applies to: the one side no human plays, or None when both or neither have a human.</summary>
        public static SideOrNone AiSide(MatchSetup setup)
        {
            bool home = setup.Inputs.Any(i => i.Device != InputDevice.None && i.Side == Side.Home);
            bool away = setup.Inputs.Any(i => i.Device != InputDevice.None && i.Side == Side.Away);
            return (home == away) ? SideOrNone.None : (home ? SideOrNone.Away : SideOrNone.Home);
        }

        /// <summary>Scales the AI club of a match by the difficulty.</summary>
        /// <returns>The setup with the AI club's traits scaled, or the same setup on normal or with no single AI club.</returns>
        public static MatchSetup Apply(MatchSetup setup, Core.Difficulty level)
        {
            return AiSide(setup).TryGetSide(out Side side) ? Apply(setup, side, level) : setup;
        }

        /// <summary>Scales one club of a match by a difficulty; the difficulty test plays hard against easy this way.</summary>
        public static MatchSetup Apply(MatchSetup setup, Side side, Core.Difficulty level)
        {
            return side == Side.Home
                ? setup with
                {
                    Home = Scale(setup.Home, level),
                }
                : setup with
                {
                    Away = Scale(setup.Away, level),
                };
        }

        /// <summary>Scales every player of a club: skaters by <see cref="Scale(Traits, Core.Difficulty)"/>, goalies by <see cref="Goalie"/>.</summary>
        public static ClubData Scale(ClubData club, Core.Difficulty level)
        {
            return club with
            {
                Roster =
                [
                    .. club.Roster.Select(p =>
                        p with
                        {
                            Traits =
                                p.Position == PlayingPosition.G
                                    ? Goalie(p.Traits, level)
                                    : Scale(p.Traits, level),
                        }
                    ),
                ],
            };
        }

        /// <summary>Scales a skater's traits: each moves by its own step, down on easy and up on hard, kept within 0 to 100.</summary>
        public static Traits Scale(Traits t, Core.Difficulty level)
        {
            float k = level switch
            {
                Core.Difficulty.Easy => -1f,
                Core.Difficulty.Hard => DifficultyTuning.HardShare,
                Core.Difficulty.Normal => 0f,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(level),
                    level,
                    "unknown difficulty"
                ),
            };
            if (k == 0f)
            {
                return t;
            }

            int By(int value, int step)
            {
                return Math.Clamp(
                    value + (int)MathF.Round(step * k, MidpointRounding.ToEven),
                    0,
                    100
                );
            }

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

        /// <summary>Scales a goalie's traits: every goalie trait comes down by the difficulty's goalie step, and
        /// positioning (hockey IQ) comes down on easy only. Club goalies keep their order.</summary>
        public static Traits Goalie(Traits t, Core.Difficulty level)
        {
            int step = level switch
            {
                Core.Difficulty.Easy => DifficultyTuning.GoalieEasy,
                Core.Difficulty.Hard => DifficultyTuning.GoalieHard,
                Core.Difficulty.Normal => DifficultyTuning.GoalieNormal,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(level),
                    level,
                    "unknown difficulty"
                ),
            };
            static int Down(int value, int by)
            {
                return Math.Clamp(value - by, 0, 100);
            }

            return t with
            {
                HockeyIq = Down(
                    t.HockeyIq,
                    level == Core.Difficulty.Easy ? DifficultyTuning.HockeyIq : 0
                ),
                GoalieReflexes = Down(t.GoalieReflexes, step),
                GoalieGlove = Down(t.GoalieGlove, step),
                GoalieBlocker = Down(t.GoalieBlocker, step),
                GoalieReboundControl = Down(t.GoalieReboundControl, step),
                GoaliePuckHandling = Down(t.GoaliePuckHandling, step),
            };
        }
    }
}
