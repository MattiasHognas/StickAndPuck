namespace StickAndPuck.App.Season.DressingRoom
{
    /// <summary>What a scouting tip (W141) is about: one of the opponent's tactics, its playstyle, its top
    /// scorer, or a gap between its strengths and the human's club.</summary>
    public enum ScoutKind
    {
        /// <summary>A balanced forecheck.</summary>
        ForecheckBalanced,

        /// <summary>An aggressive 2-1-2 forecheck.</summary>
        ForecheckAggressive,

        /// <summary>A 1-3-1 neutral-zone trap.</summary>
        ForecheckTrap,

        /// <summary>Carries the puck into the zone.</summary>
        EntryCarry,

        /// <summary>Dumps the puck in and chases.</summary>
        EntryDump,

        /// <summary>No playstyle of its own.</summary>
        StyleBalanced,

        /// <summary>The physical playstyle.</summary>
        StylePhysical,

        /// <summary>The shoot-from-anywhere playstyle.</summary>
        StyleShootFromAnywhere,

        /// <summary>The defensive-shell playstyle.</summary>
        StyleDefensiveShell,

        /// <summary>An umbrella power play.</summary>
        PowerPlayUmbrella,

        /// <summary>A 1-3-1 power play.</summary>
        PowerPlayOneThreeOne,

        /// <summary>A power play that mixes both shapes.</summary>
        PowerPlayMixed,

        /// <summary>The opponent's top goal scorer this regular season.</summary>
        TopScorer,

        /// <summary>Its goalie rates clearly above the human's club's.</summary>
        StrongGoalie,

        /// <summary>Its goalie rates clearly below the human's club's.</summary>
        WeakGoalie,

        /// <summary>Its attack rates clearly above the human's club's.</summary>
        StrongAttack,

        /// <summary>Its attack rates clearly below the human's club's.</summary>
        WeakAttack,

        /// <summary>Its defence rates clearly above the human's club's.</summary>
        StrongDefence,

        /// <summary>Its defence rates clearly below the human's club's.</summary>
        WeakDefence,
    }
}
