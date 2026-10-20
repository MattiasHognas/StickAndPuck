namespace StickAndPuck.App.Rules.Match
{
    /// <summary>Where the review of a held goal is.</summary>
    internal enum ReviewStage
    {
        /// <summary>The officials look at the goal: GOAL UNDER REVIEW.</summary>
        Review,

        /// <summary>The goal stood; the human coach of the club scored on is asked whether to challenge it.</summary>
        Prompt,

        /// <summary>A coach's challenge is looked at.</summary>
        Challenge,
    }
}
