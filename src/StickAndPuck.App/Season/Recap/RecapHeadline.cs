namespace StickAndPuck.App.Season.Recap
{
    /// <summary>One newspaper headline of the recap: what it is about and the words and numbers it fills in
    /// (<see cref="RecapText.Headline"/> words it in the language shown).</summary>
    /// <param name="Kind">What happened.</param>
    /// <param name="Name">The club, player or offer name the headline is about.</param>
    /// <param name="Other">A second name: the opponent, the rival, the player traded away; "" when none.</param>
    /// <param name="Number">A number: the streak, the hat-tricks, the age, the challenges met; 0 when none.</param>
    /// <param name="Second">A second number: the losses against the rival, the challenges set; 0 when none.</param>
    public sealed record RecapHeadline(
        HeadlineKind Kind,
        string Name,
        string Other = "",
        int Number = 0,
        int Second = 0
    );
}
