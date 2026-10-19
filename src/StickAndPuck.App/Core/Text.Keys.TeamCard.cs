namespace StickAndPuck.App.Core
{
    public static partial class Text
    {
        public static partial class Keys
        {
            /// <summary>BALANCED: the balanced playstyle.</summary>
            public const string StyleBalanced = nameof(StyleBalanced);

            /// <summary>PHYSICAL: the physical playstyle.</summary>
            public const string StylePhysical = nameof(StylePhysical);

            /// <summary>SHOOT-HAPPY: the shoot-from-anywhere playstyle.</summary>
            public const string StyleShootFromAnywhere = nameof(StyleShootFromAnywhere);

            /// <summary>DEF. SHELL: the defensive-shell playstyle.</summary>
            public const string StyleDefensiveShell = nameof(StyleDefensiveShell);

            /// <summary>{0}-{1}-{2}  {3} PTS: a club's wins, losses, overtime losses and points on the stats card.</summary>
            public const string TeamCardRecord = nameof(TeamCardRecord);

            /// <summary>#{0} OF {1}: a club's place in the table on the stats card.</summary>
            public const string TeamCardPlace = nameof(TeamCardPlace);

            /// <summary>#{0} {1}  {2} G: a club's top goal scorer on the stats card.</summary>
            public const string TeamCardScorer = nameof(TeamCardScorer);
        }
    }
}
