namespace StickAndPuck.App.Core
{
    public static partial class Text
    {
        public static partial class Keys
        {
            /// <summary>RELEGATION PLAYOUT (the rules page row)</summary>
            public const string RulePlayout = nameof(RulePlayout);

            /// <summary>PLAYOUT (the stage: the bracket column and the next game line)</summary>
            public const string PlayoutTitle = nameof(PlayoutTitle);

            /// <summary>{0} DOWN (under the playout in the bracket, {0} the club's short name)</summary>
            public const string RelegatedShort = nameof(RelegatedShort);

            /// <summary>{0} ARE RELEGATED</summary>
            public const string Relegated = nameof(Relegated);

            /// <summary>RELEGATED (the trophy screen's label for the club that went down)</summary>
            public const string RelegatedLabel = nameof(RelegatedLabel);

            /// <summary>{0} STAYED UP (the human's club won the playout)</summary>
            public const string StayedUp = nameof(StayedUp);

            /// <summary>RELEGATION (the relegation screen's heading and the menu item that opens it)</summary>
            public const string Relegation = nameof(Relegation);

            /// <summary>{0}-{1} V {2} (a playout result: games won, games lost, the other club)</summary>
            public const string PlayoutLine = nameof(PlayoutLine);

            /// <summary>DOWN TO HOCKEYALLSVENSKAN (the relegation screen's last word)</summary>
            public const string RelegationNote = nameof(RelegationNote);
        }
    }
}
