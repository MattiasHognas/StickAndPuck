namespace StickAndPuck.App.Core
{
    public static partial class Text
    {
        public static partial class Keys
        {
            /// <summary>ONLINE VERSUS</summary>
            public const string OnlineVersus = nameof(OnlineVersus);

            /// <summary>HOST A MATCH</summary>
            public const string NetHostGame = nameof(NetHostGame);

            /// <summary>JOIN A MATCH</summary>
            public const string NetJoinGame = nameof(NetJoinGame);

            /// <summary>ADDRESS</summary>
            public const string NetAddress = nameof(NetAddress);

            /// <summary>PORT</summary>
            public const string NetPort = nameof(NetPort);

            /// <summary>INPUT DELAY</summary>
            public const string NetDelay = nameof(NetDelay);

            /// <summary>{0} STEPS</summary>
            public const string NetDelaySteps = nameof(NetDelaySteps);

            /// <summary>TYPE THE ADDRESS AND PORT   ENTER PICKS   ESC BACK</summary>
            public const string HintLobby = nameof(HintLobby);

            /// <summary>YOU HOST: PICK YOUR CLUB</summary>
            public const string NetPickHome = nameof(NetPickHome);

            /// <summary>PICK THE OPPONENT&apos;S CLUB</summary>
            public const string NetPickAway = nameof(NetPickAway);

            /// <summary>HOSTING ON PORT {0}</summary>
            public const string NetHosting = nameof(NetHosting);

            /// <summary>WAITING FOR A PLAYER TO JOIN</summary>
            public const string NetWaitingForPlayer = nameof(NetWaitingForPlayer);

            /// <summary>YOUR ADDRESS: {0}</summary>
            public const string NetYourAddress = nameof(NetYourAddress);

            /// <summary>HOME: {0} (YOU)</summary>
            public const string NetHomeClub = nameof(NetHomeClub);

            /// <summary>AWAY: {0}</summary>
            public const string NetAwayClub = nameof(NetAwayClub);

            /// <summary>JOINING {0}</summary>
            public const string NetJoining = nameof(NetJoining);

            /// <summary>CONNECTING...</summary>
            public const string NetConnecting = nameof(NetConnecting);

            /// <summary>CONNECTED. WAITING FOR THE HOST</summary>
            public const string NetWaitingForHost = nameof(NetWaitingForHost);

            /// <summary>COULD NOT CONNECT</summary>
            public const string NetCouldNotConnect = nameof(NetCouldNotConnect);

            /// <summary>THE HOST DID NOT ANSWER</summary>
            public const string NetNoAnswer = nameof(NetNoAnswer);

            /// <summary>CONNECTION LOST</summary>
            public const string NetConnectionLost = nameof(NetConnectionLost);

            /// <summary>THE OPPONENT LEFT THE MATCH</summary>
            public const string NetOpponentLeft = nameof(NetOpponentLeft);

            /// <summary>OUT OF SYNC AT STEP {0}: MATCH ENDED</summary>
            public const string NetDesync = nameof(NetDesync);

            /// <summary>THE OTHER SIDE RUNS ANOTHER VERSION</summary>
            public const string NetVersionDiffers = nameof(NetVersionDiffers);

            /// <summary>THE CLUB FILES DIFFER</summary>
            public const string NetClubsDiffer = nameof(NetClubsDiffer);

            /// <summary>PORT {0} CANNOT BE USED</summary>
            public const string NetPortTaken = nameof(NetPortTaken);

            /// <summary>WAITING FOR THE OPPONENT</summary>
            public const string NetWaiting = nameof(NetWaiting);

            /// <summary>PRESS AGAIN TO LEAVE THE MATCH</summary>
            public const string NetLeaveAgain = nameof(NetLeaveAgain);

            /// <summary>ONLINE  YOU PLAY {0}</summary>
            public const string NetOnline = nameof(NetOnline);

            /// <summary>ESC BACK</summary>
            public const string HintNetWait = nameof(HintNetWait);

            /// <summary>ENTER TRIES AGAIN   ESC BACK</summary>
            public const string HintNetRetry = nameof(HintNetRetry);
        }
    }
}
