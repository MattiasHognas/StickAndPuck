namespace StickAndPuck.App.Core
{
    /// <summary>A league's rule book: the built-in presets a <see cref="RuleSet"/> starts from.</summary>
    public enum RulePreset
    {
        /// <summary>Default: the SHL as the game plays it.</summary>
        Shl,

        /// <summary>Loose rules for the arcade runs: no offside, no icing, a lenient referee.</summary>
        Arcade,

        /// <summary>The NHL: SHL rules with the NHL's shootout order, fight major and 4 on 4 coincidentals.</summary>
        Nhl,
    }
}
