namespace StickAndPuck.App.Replays;

/// <summary>What a replayed step led to.</summary>
public enum ReplayStep
{
    /// <summary>Nothing new: keep going.</summary>
    Next,

    /// <summary>The SAP_AT moment was reached on this step.</summary>
    Target,

    /// <summary>The recording has no more steps.</summary>
    End,
}
