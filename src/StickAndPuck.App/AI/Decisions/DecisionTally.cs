namespace StickAndPuck.App.AI;

/// <summary>What one club's passing, shooting and checking came to in a match, for the HUD line and tests.</summary>
public sealed class DecisionTally
{
    /// <summary>Gets or sets the passes made, by humans and AI.</summary>
    public int Passes { get; set; }

    /// <summary>Gets or sets the passes a teammate took.</summary>
    public int Completed { get; set; }

    /// <summary>Gets or sets the passes an opponent took.</summary>
    public int Intercepted { get; set; }

    /// <summary>Gets or sets the shots taken.</summary>
    public int Shots { get; set; }

    /// <summary>Gets or sets the checks the AI threw.</summary>
    public int Checks { get; set; }

    /// <summary>Gets or sets the AI passes aimed at a teammate who was skating (over 2 m/s) when the puck left.</summary>
    public int ToMoving { get; set; }

    /// <summary>Gets or sets the AI passes aimed at a teammate with no opponent within 3 m when the puck left.</summary>
    public int ToOpen { get; set; }

    /// <summary>Gets or sets the AI passes aimed at a teammate.</summary>
    public int Aimed { get; set; }

    /// <summary>Gets or sets the reads and executions the AI rolled against hockey IQ (the jitter).</summary>
    public int Rolls { get; set; }

    /// <summary>Gets or sets the rolls that came out as a fumble.</summary>
    public int Fumbles { get; set; }

    /// <summary>Gets or sets the rolls that came out lucky.</summary>
    public int Lucky { get; set; }

    /// <summary>Gets or sets the rolls by skaters with hockey IQ 95 or more.</summary>
    public int StarRolls { get; set; }

    /// <summary>Gets or sets the fumbles by skaters with hockey IQ 95 or more.</summary>
    public int StarFumbles { get; set; }

    /// <summary>Gets or sets the times an AI carrier fired the puck out of his own zone under pressure.</summary>
    public int Clears { get; set; }

    /// <summary>Gets or sets the clears fired hard up the ice, the kind that may go the length for icing.</summary>
    public int HardClears { get; set; }

    /// <summary>Gets or sets the times an AI carrier held up at the blue line for a teammate offside to tag up.</summary>
    public int HoldUps { get; set; }

    /// <summary>Gets or sets the times an AI carrier came up to the blue line with a teammate offside and weighed holding up.</summary>
    public int HoldLooks { get; set; }

    /// <summary>Gets or sets the dekes the AI pressed.</summary>
    public int Dekes { get; set; }

    /// <summary>Gets or sets the passes an AI receiver held shoot for, to one-time them.</summary>
    public int OneTimers { get; set; }

    /// <summary>Gets the share of passes that reached a teammate, 0 to 1.</summary>
    public float Completion => Passes == 0 ? 0f : (float)Completed / Passes;
}
