namespace StickAndPuck.App.AI
{
    /// <summary>Who last wrote an AI skater's command, why, and on which step: for debug traces and tests.</summary>
    public readonly record struct AiIntent(AiLayer Owner, string Reason, long Tick);
}
