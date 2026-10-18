namespace StickAndPuck.App.Core
{
    /// <summary>How hard the AI plays (the AI club's traits scale by it).</summary>
    public enum Difficulty
    {
        /// <summary>The AI makes more mistakes.</summary>
        Easy,

        /// <summary>Default: the AI as the traits say.</summary>
        Normal,

        /// <summary>The AI plays sharper.</summary>
        Hard,
    }
}
