namespace StickAndPuck.App.Core
{
    /// <summary>What a trailing human club's coach told the players in the intermission, for the next period.</summary>
    public enum TalkChoice
    {
        /// <summary>No talk, or no answer: the club plays as its tactics say.</summary>
        None,

        /// <summary>Push: shorter shifts, an aggressive forecheck with the defence pinching, riskier passes.</summary>
        Push,

        /// <summary>Stay patient: no pinching, steady positioning, fewer risky passes.</summary>
        Patient,
    }
}
