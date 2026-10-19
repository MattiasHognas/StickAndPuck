namespace StickAndPuck.App.Core
{
    /// <content>The string table keys that tell the leagues apart on the screens (W118).</content>
    public static partial class Text
    {
        /// <content>The per-league screen keys.</content>
        public static partial class Keys
        {
            /// <summary>NHL  SEASON LEADERS  -  {0}</summary>
            public const string NhlLeadersHeadingSeason = nameof(NhlLeadersHeadingSeason);

            /// <summary>NHL  PLAYOFF LEADERS  -  {0}</summary>
            public const string NhlLeadersHeadingPlayoff = nameof(NhlLeadersHeadingPlayoff);

            /// <summary>STANLEY CUP FINAL: the NHL's last round.</summary>
            public const string RoundStanleyCupFinal = nameof(RoundStanleyCupFinal);

            /// <summary>LE MAT TROPHY: the SHL champion's trophy.</summary>
            public const string TrophyLeMat = nameof(TrophyLeMat);

            /// <summary>STANLEY CUP: the NHL champion's trophy.</summary>
            public const string TrophyStanleyCup = nameof(TrophyStanleyCup);

            /// <summary>{0}  -  {1}: the trophy screen's heading, the champions and the trophy.</summary>
            public const string TrophyHeading = nameof(TrophyHeading);

            /// <summary>WILD CARD: the wild card block of the NHL standings.</summary>
            public const string WildCardHeading = nameof(WildCardHeading);

            /// <summary>LEFT RIGHT SHOWS THE WILD CARD RACE</summary>
            public const string HintStandingsWildCard = nameof(HintStandingsWildCard);

            /// <summary>LEFT RIGHT SHOWS THE DIVISIONS</summary>
            public const string HintStandingsDivisions = nameof(HintStandingsDivisions);
        }
    }
}
