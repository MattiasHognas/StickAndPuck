namespace StickAndPuck.App.Core
{
    /// <summary>How long the league sits a player out, when <see cref="RuleSet.Suspensions"/> is on.</summary>
    public enum SuspensionStrength
    {
        /// <summary>One game less, never under one.</summary>
        Mild,

        /// <summary>Default: one game, two for a dangerous hit's major and game misconduct or for two offences in one game.</summary>
        Normal,

        /// <summary>One game more, never over three.</summary>
        Strong,
    }
}
