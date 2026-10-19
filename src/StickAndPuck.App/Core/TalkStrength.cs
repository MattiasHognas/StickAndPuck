namespace StickAndPuck.App.Core
{
    /// <summary>How much the coach's pre-game talk counts, when <see cref="RuleSet.PreGameTalk"/> is on.</summary>
    public enum TalkStrength
    {
        /// <summary>Half the effect.</summary>
        Mild,

        /// <summary>Default.</summary>
        Normal,

        /// <summary>Half again the effect.</summary>
        Strong,
    }
}
