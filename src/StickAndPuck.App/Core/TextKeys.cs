namespace StickAndPuck.App.Core;

/// <content>The string table keys (W53).</content>
public static partial class Text
{
    /// <summary>Every key in the language files; each is documented with its English text.</summary>
    public static class Keys
    {
        /// <summary>ENGLISH</summary>
        public const string LanguageName = nameof(LanguageName);

        /// <summary>RULES</summary>
        public const string Rules = nameof(Rules);

        /// <summary>CONTROL SWITCHING</summary>
        public const string ControlSwitching = nameof(ControlSwitching);

        /// <summary>AUTO</summary>
        public const string SwitchAuto = nameof(SwitchAuto);

        /// <summary>PASSES ONLY</summary>
        public const string SwitchPassesOnly = nameof(SwitchPassesOnly);

        /// <summary>MANUAL</summary>
        public const string SwitchManual = nameof(SwitchManual);

        /// <summary>LINE CHANGES</summary>
        public const string LineChanges = nameof(LineChanges);

        /// <summary>AUTOMATIC</summary>
        public const string LinesAutomatic = nameof(LinesAutomatic);

        /// <summary>MANUAL</summary>
        public const string LinesManual = nameof(LinesManual);

        /// <summary>DIFFICULTY</summary>
        public const string Difficulty = nameof(Difficulty);

        /// <summary>EASY</summary>
        public const string DifficultyEasy = nameof(DifficultyEasy);

        /// <summary>NORMAL</summary>
        public const string DifficultyNormal = nameof(DifficultyNormal);

        /// <summary>HARD</summary>
        public const string DifficultyHard = nameof(DifficultyHard);

        /// <summary>PERIOD LENGTH</summary>
        public const string PeriodLength = nameof(PeriodLength);

        /// <summary>{0} MIN</summary>
        public const string PeriodMinutes = nameof(PeriodMinutes);

        /// <summary>SIM THIS GAME</summary>
        public const string SimThisGame = nameof(SimThisGame);

        /// <summary>AUTO DEKE</summary>
        public const string AutoDeke = nameof(AutoDeke);

        /// <summary>ONE-TIMERS</summary>
        public const string OneTimers = nameof(OneTimers);

        /// <summary>CONTROLS</summary>
        public const string Controls = nameof(Controls);

        /// <summary>PLAYER 2 CONTROLS</summary>
        public const string Player2Controls = nameof(Player2Controls);

        /// <summary>KEY MAPPING</summary>
        public const string KeyMapping = nameof(KeyMapping);

        /// <summary>CONTROL MARKER</summary>
        public const string ControlMarker = nameof(ControlMarker);

        /// <summary>CAMERA ZOOM</summary>
        public const string CameraZoom = nameof(CameraZoom);

        /// <summary>LIGHTING</summary>
        public const string Lighting = nameof(Lighting);

        /// <summary>RANDOM</summary>
        public const string LightingRandom = nameof(LightingRandom);

        /// <summary>EARLY</summary>
        public const string LightingEarly = nameof(LightingEarly);

        /// <summary>AFTERNOON</summary>
        public const string LightingAfternoon = nameof(LightingAfternoon);

        /// <summary>LATE</summary>
        public const string LightingLate = nameof(LightingLate);

        /// <summary>SWAP KITS</summary>
        public const string SwapKits = nameof(SwapKits);

        /// <summary>VOLUME</summary>
        public const string Volume = nameof(Volume);

        /// <summary>LANGUAGE</summary>
        public const string Language = nameof(Language);

        /// <summary>FPS LINE</summary>
        public const string FpsLine = nameof(FpsLine);

        /// <summary>MATCH DEBUG LINE</summary>
        public const string MatchDebugLine = nameof(MatchDebugLine);

        /// <summary>LINES PANEL</summary>
        public const string LinesPanel = nameof(LinesPanel);

        /// <summary>BACK</summary>
        public const string Back = nameof(Back);

        /// <summary>SETTINGS</summary>
        public const string Settings = nameof(Settings);

        /// <summary>- MORE -</summary>
        public const string More = nameof(More);

        /// <summary>LEFT RIGHT CHANGES   ESC BACK</summary>
        public const string HintSettings = nameof(HintSettings);

        /// <summary>ON</summary>
        public const string On = nameof(On);

        /// <summary>OFF</summary>
        public const string Off = nameof(Off);

        /// <summary>CONTINUE SEASON</summary>
        public const string ContinueSeason = nameof(ContinueSeason);

        /// <summary>SIM NEXT GAME</summary>
        public const string SimNextGame = nameof(SimNextGame);

        /// <summary>NEW SEASON</summary>
        public const string NewSeason = nameof(NewSeason);

        /// <summary>NEW GAME</summary>
        public const string NewGame = nameof(NewGame);

        /// <summary>STANDINGS</summary>
        public const string Standings = nameof(Standings);

        /// <summary>QUIT</summary>
        public const string Quit = nameof(Quit);

        /// <summary>{0} - GAME {1} OF {2}</summary>
        public const string StartGameOf = nameof(StartGameOf);

        /// <summary>{0} - SEASON OVER</summary>
        public const string StartSeasonOver = nameof(StartSeasonOver);

        /// <summary>{0} - PLAYOFFS</summary>
        public const string StartPlayoffs = nameof(StartPlayoffs);

        /// <summary>ARROWS MOVE   ENTER PICKS   ESC BACK</summary>
        public const string HintMenu = nameof(HintMenu);

        /// <summary>PICK YOUR CLUB</summary>
        public const string PickYourClub = nameof(PickYourClub);

        /// <summary>PICK THE OPPONENT</summary>
        public const string PickOpponent = nameof(PickOpponent);

        /// <summary>REPLACE THE SAVED SEASON?</summary>
        public const string ReplaceSave = nameof(ReplaceSave);

        /// <summary>RESUME</summary>
        public const string Resume = nameof(Resume);

        /// <summary>QUIT TO MENU</summary>
        public const string QuitToMenu = nameof(QuitToMenu);

        /// <summary>LEAVE THIS MATCH?</summary>
        public const string LeaveMatch = nameof(LeaveMatch);

        /// <summary>PAUSED</summary>
        public const string Paused = nameof(Paused);

        /// <summary>CHAMPIONS</summary>
        public const string Champions = nameof(Champions);

        /// <summary>SEASON STATS</summary>
        public const string SeasonStats = nameof(SeasonStats);

        /// <summary>MAIN MENU</summary>
        public const string MainMenu = nameof(MainMenu);

        /// <summary>TROPHY</summary>
        public const string Trophy = nameof(Trophy);

        /// <summary>BEST RUNS</summary>
        public const string BestRuns = nameof(BestRuns);

        /// <summary>NEXT GAME</summary>
        public const string NextGame = nameof(NextGame);

        /// <summary>PLAY AGAIN</summary>
        public const string PlayAgain = nameof(PlayAgain);

        /// <summary>FINAL</summary>
        public const string Final = nameof(Final);

        /// <summary>FINAL - {0}</summary>
        public const string FinalDecided = nameof(FinalDecided);

        /// <summary>SHOTS  {0} - {1}</summary>
        public const string ResultShots = nameof(ResultShots);

        /// <summary>NO GOALS</summary>
        public const string NoGoals = nameof(NoGoals);

        /// <summary>PLAYER OF THE GAME  {0}  {1} SAVES</summary>
        public const string PlayerOfGameSaves = nameof(PlayerOfGameSaves);

        /// <summary>PLAYER OF THE GAME  {0}  {1}G {2}A</summary>
        public const string PlayerOfGamePoints = nameof(PlayerOfGamePoints);

        /// <summary>+{0} MORE</summary>
        public const string MoreGoals = nameof(MoreGoals);

        /// <summary>START PLAYOFFS</summary>
        public const string StartPlayoffsItem = nameof(StartPlayoffsItem);

        /// <summary>PLAY NEXT GAME</summary>
        public const string PlayNextGame = nameof(PlayNextGame);

        /// <summary>PLAYOFFS</summary>
        public const string Playoffs = nameof(Playoffs);

        /// <summary>GP</summary>
        public const string ColGp = nameof(ColGp);

        /// <summary>W</summary>
        public const string ColW = nameof(ColW);

        /// <summary>OTW</summary>
        public const string ColOtw = nameof(ColOtw);

        /// <summary>OTL</summary>
        public const string ColOtl = nameof(ColOtl);

        /// <summary>L</summary>
        public const string ColL = nameof(ColL);

        /// <summary>GOALS</summary>
        public const string ColGoals = nameof(ColGoals);

        /// <summary>PTS</summary>
        public const string ColPts = nameof(ColPts);

        /// <summary>SHL  FINAL TABLE</summary>
        public const string ShlFinalTable = nameof(ShlFinalTable);

        /// <summary>SHL  ROUND {0} OF {1}</summary>
        public const string ShlRound = nameof(ShlRound);

        /// <summary>NEXT: {0} - {1}</summary>
        public const string NextMatch = nameof(NextMatch);

        /// <summary>REGULAR SEASON OVER</summary>
        public const string RegularSeasonOver = nameof(RegularSeasonOver);

        /// <summary>WAITING FOR THE ROUND</summary>
        public const string WaitingForRound = nameof(WaitingForRound);

        /// <summary>PLAY-IN</summary>
        public const string RoundPlayIn = nameof(RoundPlayIn);

        /// <summary>QUARTER-FINALS</summary>
        public const string RoundQuarterFinals = nameof(RoundQuarterFinals);

        /// <summary>SEMI-FINALS</summary>
        public const string RoundSemiFinals = nameof(RoundSemiFinals);

        /// <summary>FINAL</summary>
        public const string RoundFinal = nameof(RoundFinal);

        /// <summary>SHL PLAYOFFS</summary>
        public const string ShlPlayoffs = nameof(ShlPlayoffs);

        /// <summary>SHL CHAMPIONS: {0}</summary>
        public const string ShlChampionsOf = nameof(ShlChampionsOf);

        /// <summary>{0} MISSED THE PLAYOFFS ({1}TH)</summary>
        public const string MissedPlayoffsPlace = nameof(MissedPlayoffsPlace);

        /// <summary>NEXT: {0} GAME {1} V {2}</summary>
        public const string NextPlayoffGameV = nameof(NextPlayoffGameV);

        /// <summary>{0} WAIT FOR THE PLAY-IN</summary>
        public const string WaitForPlayIn = nameof(WaitForPlayIn);

        /// <summary>{0} ARE OUT</summary>
        public const string AreOut = nameof(AreOut);

        /// <summary>ROUND {0}</summary>
        public const string SimRound = nameof(SimRound);

        /// <summary>{0} GAME {1}</summary>
        public const string SimStage = nameof(SimStage);

        /// <summary>{0} MISSED THE PLAYOFFS</summary>
        public const string MissedPlayoffs = nameof(MissedPlayoffs);

        /// <summary>NEXT: {0} GAME {1} - {2}</summary>
        public const string NextPlayoffGame = nameof(NextPlayoffGame);

        /// <summary>{0} WAIT FOR THE NEXT ROUND</summary>
        public const string WaitForNextRound = nameof(WaitForNextRound);

        /// <summary>TABLE</summary>
        public const string TableItem = nameof(TableItem);

        /// <summary>POINTS</summary>
        public const string ViewPoints = nameof(ViewPoints);

        /// <summary>GOALS</summary>
        public const string ViewGoals = nameof(ViewGoals);

        /// <summary>ASSISTS</summary>
        public const string ViewAssists = nameof(ViewAssists);

        /// <summary>HITS</summary>
        public const string ViewHits = nameof(ViewHits);

        /// <summary>GOALIES</summary>
        public const string ViewGoalies = nameof(ViewGoalies);

        /// <summary>STARS</summary>
        public const string ViewStars = nameof(ViewStars);

        /// <summary>YOUR CLUB</summary>
        public const string ViewYourClub = nameof(ViewYourClub);

        /// <summary>SHOW</summary>
        public const string Show = nameof(Show);

        /// <summary>GAMES</summary>
        public const string Games = nameof(Games);

        /// <summary>SEASON</summary>
        public const string SeasonWord = nameof(SeasonWord);

        /// <summary>NO SEASON PLAYED YET</summary>
        public const string NoSeasonYet = nameof(NoSeasonYet);

        /// <summary>{0}  -  {1}</summary>
        public const string ClubViewHeading = nameof(ClubViewHeading);

        /// <summary>SHL  SEASON LEADERS  -  {0}</summary>
        public const string LeadersHeadingSeason = nameof(LeadersHeadingSeason);

        /// <summary>SHL  PLAYOFF LEADERS  -  {0}</summary>
        public const string LeadersHeadingPlayoff = nameof(LeadersHeadingPlayoff);

        /// <summary>SV</summary>
        public const string ColSv = nameof(ColSv);

        /// <summary>SV/GP</summary>
        public const string ColSvGp = nameof(ColSvGp);

        /// <summary>STARS</summary>
        public const string ColStars = nameof(ColStars);

        /// <summary>G</summary>
        public const string ColG = nameof(ColG);

        /// <summary>A</summary>
        public const string ColA = nameof(ColA);

        /// <summary>SH</summary>
        public const string ColSh = nameof(ColSh);

        /// <summary>HITS</summary>
        public const string ColHits = nameof(ColHits);

        /// <summary>PLAYER</summary>
        public const string ColPlayer = nameof(ColPlayer);

        /// <summary>POS</summary>
        public const string ColPos = nameof(ColPos);

        /// <summary>CLUB</summary>
        public const string ColClub = nameof(ColClub);

        /// <summary>NO PLAYOFF GAMES PLAYED YET</summary>
        public const string NoPlayoffGamesYet = nameof(NoPlayoffGamesYet);

        /// <summary>NO GAMES PLAYED YET</summary>
        public const string NoGamesYet = nameof(NoGamesYet);

        /// <summary>PLAYER OF THE GAME: {0} ({1}) {2}  IN {3} {4}-{5} {6}</summary>
        public const string StarLine = nameof(StarLine);

        /// <summary>PLAYER OF THE GAME: NOBODY YET</summary>
        public const string StarNobody = nameof(StarNobody);

        /// <summary>LEFT RIGHT CHANGES THE LIST   ENTER PICKS   ESC BACK</summary>
        public const string HintStats = nameof(HintStats);

        /// <summary>CONTINUE</summary>
        public const string Continue = nameof(Continue);

        /// <summary> - OT</summary>
        public const string KindOt = nameof(KindOt);

        /// <summary> - SHOOTOUT</summary>
        public const string KindShootout = nameof(KindShootout);

        /// <summary>SIMMED  {0}{1}</summary>
        public const string SimmedHeading = nameof(SimmedHeading);

        /// <summary>SETTLED ON PAPER</summary>
        public const string SettledOnPaper = nameof(SettledOnPaper);

        /// <summary>FINAL</summary>
        public const string TrophyFinal = nameof(TrophyFinal);

        /// <summary>WON {0}-{1} V {2}</summary>
        public const string TrophyFinalLine = nameof(TrophyFinalLine);

        /// <summary>TOP SCORER</summary>
        public const string TopScorer = nameof(TopScorer);

        /// <summary>{0} {1} {2} PTS</summary>
        public const string TopScorerLine = nameof(TopScorerLine);

        /// <summary>MOST GOALS</summary>
        public const string MostGoals = nameof(MostGoals);

        /// <summary>{0} {1} {2}</summary>
        public const string MostGoalsLine = nameof(MostGoalsLine);

        /// <summary>BEST GOALIE</summary>
        public const string BestGoalie = nameof(BestGoalie);

        /// <summary>{0} {1} {2} SV</summary>
        public const string BestGoalieLine = nameof(BestGoalieLine);

        /// <summary>PLAYOFF STAR</summary>
        public const string PlayoffStar = nameof(PlayoffStar);

        /// <summary>YOUR CLUB</summary>
        public const string YourClub = nameof(YourClub);

        /// <summary>CHAMPIONS!</summary>
        public const string ChampionsShout = nameof(ChampionsShout);

        /// <summary>{0} WENT OUT</summary>
        public const string WentOut = nameof(WentOut);

        /// <summary>SHL CHAMPIONS</summary>
        public const string ShlChampions = nameof(ShlChampions);

        /// <summary>RULE</summary>
        public const string Rule = nameof(Rule);

        /// <summary>SUDDEN DEATH</summary>
        public const string SuddenDeath = nameof(SuddenDeath);

        /// <summary>THREE LIVES</summary>
        public const string ThreeLives = nameof(ThreeLives);

        /// <summary>RECORD</summary>
        public const string Record = nameof(Record);

        /// <summary>{0} BEATEN, {1} LOSS</summary>
        public const string RecordLineOne = nameof(RecordLineOne);

        /// <summary>{0} BEATEN, {1} LOSSES</summary>
        public const string RecordLineMany = nameof(RecordLineMany);

        /// <summary>GOALS</summary>
        public const string GoalsLabel = nameof(GoalsLabel);

        /// <summary>{0} {1} PTS</summary>
        public const string TopScorerArcade = nameof(TopScorerArcade);

        /// <summary>PLACE {0} OF {1}</summary>
        public const string PlaceOf = nameof(PlaceOf);

        /// <summary>ARCADE CHAMPIONS</summary>
        public const string ArcadeChampions = nameof(ArcadeChampions);

        /// <summary>ARCADE  BEST RUNS</summary>
        public const string ArcadeBestRuns = nameof(ArcadeBestRuns);

        /// <summary>RULE</summary>
        public const string ColRule = nameof(ColRule);

        /// <summary>RUN</summary>
        public const string ColRun = nameof(ColRun);

        /// <summary>TOP SCORER</summary>
        public const string ColTopScorer = nameof(ColTopScorer);

        /// <summary>NO RUNS FINISHED YET</summary>
        public const string NoRunsYet = nameof(NoRunsYet);

        /// <summary>SUDDEN</summary>
        public const string RuleSudden = nameof(RuleSudden);

        /// <summary>3 LIVES</summary>
        public const string RuleLives = nameof(RuleLives);

        /// <summary>{0} BEATEN</summary>
        public const string Beaten = nameof(Beaten);

        /// <summary>{0} SV</summary>
        public const string SummarySaves = nameof(SummarySaves);

        /// <summary>{0}G {1}A</summary>
        public const string SummaryPoints = nameof(SummaryPoints);

        /// <summary>ARCADE  CHAMPIONS</summary>
        public const string ArcadeHeadChampions = nameof(ArcadeHeadChampions);

        /// <summary>ARCADE  RUN OVER</summary>
        public const string ArcadeHeadRunOver = nameof(ArcadeHeadRunOver);

        /// <summary>ARCADE</summary>
        public const string ArcadeHead = nameof(ArcadeHead);

        /// <summary>BEATEN {0} OF {1}</summary>
        public const string BeatenOf = nameof(BeatenOf);

        /// <summary>LIVES {0} OF {1}</summary>
        public const string LivesOf = nameof(LivesOf);

        /// <summary> OT</summary>
        public const string SuffixOt = nameof(SuffixOt);

        /// <summary> SO</summary>
        public const string SuffixSo = nameof(SuffixSo);

        /// <summary>W</summary>
        public const string Win = nameof(Win);

        /// <summary>L</summary>
        public const string Loss = nameof(Loss);

        /// <summary>VS {0}</summary>
        public const string VsClub = nameof(VsClub);

        /// <summary>AT {0}</summary>
        public const string AtClub = nameof(AtClub);

        /// <summary>NEXT  VS {0}</summary>
        public const string NextVs = nameof(NextVs);

        /// <summary>NEXT  AT {0}</summary>
        public const string NextAt = nameof(NextAt);

        /// <summary>BEST RUNS</summary>
        public const string BestRunsHead = nameof(BestRunsHead);

        /// <summary>{0} {1}</summary>
        public const string BestRunLine = nameof(BestRunLine);

        /// <summary>SD</summary>
        public const string RuleSd = nameof(RuleSd);

        /// <summary>3L</summary>
        public const string Rule3l = nameof(Rule3l);

        /// <summary>ARCADE - BEST RUNS</summary>
        public const string ArcadeBestRunsItem = nameof(ArcadeBestRunsItem);

        /// <summary>ENTER PICKS   ESC BACK</summary>
        public const string HintPick = nameof(HintPick);

        /// <summary>THIS MODE PLAYS ITS OWN RULES   UP BACK</summary>
        public const string HintOwnRules = nameof(HintOwnRules);

        /// <summary>LEFT RIGHT PICKS THE RULES   UP BACK</summary>
        public const string HintPickRules = nameof(HintPickRules);

        /// <summary>ATTACK</summary>
        public const string Attack = nameof(Attack);

        /// <summary>DEFENCE</summary>
        public const string Defence = nameof(Defence);

        /// <summary>SPEED</summary>
        public const string Speed = nameof(Speed);

        /// <summary>GOALIE</summary>
        public const string Goalie = nameof(Goalie);

        /// <summary>OVERALL {0}</summary>
        public const string Overall = nameof(Overall);

        /// <summary>NO</summary>
        public const string No = nameof(No);

        /// <summary>YES</summary>
        public const string Yes = nameof(Yes);

        /// <summary>EXHIBITION</summary>
        public const string Exhibition = nameof(Exhibition);

        /// <summary>{0} OF {1} BEATEN</summary>
        public const string ArcadeStatusWins = nameof(ArcadeStatusWins);

        /// <summary>ARCADE CHAMPIONS - {0}</summary>
        public const string ArcadeStatusChampions = nameof(ArcadeStatusChampions);

        /// <summary>RUN OVER - {0}</summary>
        public const string ArcadeStatusOver = nameof(ArcadeStatusOver);

        /// <summary>{0} - LIVES {1} - NEXT {2}</summary>
        public const string ArcadeStatusNext = nameof(ArcadeStatusNext);

        /// <summary>ARCADE - THREE LIVES</summary>
        public const string ArcadeThreeLives = nameof(ArcadeThreeLives);

        /// <summary>ARCADE - SUDDEN DEATH</summary>
        public const string ArcadeSuddenDeath = nameof(ArcadeSuddenDeath);

        /// <summary>ARCADE - CONTINUE RUN</summary>
        public const string ArcadeContinue = nameof(ArcadeContinue);

        /// <summary>2-1-2 FORECHECK</summary>
        public const string Forecheck212 = nameof(Forecheck212);

        /// <summary>1-3-1 TRAP</summary>
        public const string Trap131 = nameof(Trap131);

        /// <summary>BALANCED FORECHECK</summary>
        public const string ForecheckBalanced = nameof(ForecheckBalanced);

        /// <summary>DUMP IN</summary>
        public const string DumpIn = nameof(DumpIn);

        /// <summary>CARRY IN</summary>
        public const string CarryIn = nameof(CarryIn);

        /// <summary>UMBRELLA PP</summary>
        public const string PpUmbrella = nameof(PpUmbrella);

        /// <summary>1-3-1 PP</summary>
        public const string Pp131 = nameof(Pp131);

        /// <summary>MIXED PP</summary>
        public const string PpMixed = nameof(PpMixed);

        /// <summary>L SHIFT</summary>
        public const string KeyLShift = nameof(KeyLShift);

        /// <summary>R SHIFT</summary>
        public const string KeyRShift = nameof(KeyRShift);

        /// <summary>L CTRL</summary>
        public const string KeyLCtrl = nameof(KeyLCtrl);

        /// <summary>R CTRL</summary>
        public const string KeyRCtrl = nameof(KeyRCtrl);

        /// <summary>L ALT</summary>
        public const string KeyLAlt = nameof(KeyLAlt);

        /// <summary>R ALT</summary>
        public const string KeyRAlt = nameof(KeyRAlt);

        /// <summary>L SUPER</summary>
        public const string KeyLSuper = nameof(KeyLSuper);

        /// <summary>R SUPER</summary>
        public const string KeyRSuper = nameof(KeyRSuper);

        /// <summary>PG UP</summary>
        public const string KeyPgUp = nameof(KeyPgUp);

        /// <summary>PG DOWN</summary>
        public const string KeyPgDown = nameof(KeyPgDown);

        /// <summary>CAPS</summary>
        public const string KeyCaps = nameof(KeyCaps);

        /// <summary>BKSP</summary>
        public const string KeyBksp = nameof(KeyBksp);

        /// <summary>KP {0}</summary>
        public const string KeyKp = nameof(KeyKp);

        /// <summary>D-UP</summary>
        public const string PadUp = nameof(PadUp);

        /// <summary>D-DOWN</summary>
        public const string PadDown = nameof(PadDown);

        /// <summary>D-LEFT</summary>
        public const string PadLeft = nameof(PadLeft);

        /// <summary>D-RIGHT</summary>
        public const string PadRight = nameof(PadRight);

        /// <summary>A</summary>
        public const string PadA = nameof(PadA);

        /// <summary>B</summary>
        public const string PadB = nameof(PadB);

        /// <summary>X</summary>
        public const string PadX = nameof(PadX);

        /// <summary>Y</summary>
        public const string PadY = nameof(PadY);

        /// <summary>LB</summary>
        public const string PadLb = nameof(PadLb);

        /// <summary>LT</summary>
        public const string PadLt = nameof(PadLt);

        /// <summary>RB</summary>
        public const string PadRb = nameof(PadRb);

        /// <summary>RT</summary>
        public const string PadRt = nameof(PadRt);

        /// <summary>BACK</summary>
        public const string PadBack = nameof(PadBack);

        /// <summary>GUIDE</summary>
        public const string PadGuide = nameof(PadGuide);

        /// <summary>START</summary>
        public const string PadStart = nameof(PadStart);

        /// <summary>LS</summary>
        public const string PadLs = nameof(PadLs);

        /// <summary>RS</summary>
        public const string PadRs = nameof(PadRs);

        /// <summary>KEYBOARD</summary>
        public const string DeviceKeyboard = nameof(DeviceKeyboard);

        /// <summary>OFF</summary>
        public const string DeviceOff = nameof(DeviceOff);

        /// <summary>NO GAMEPAD {0}</summary>
        public const string NoGamepad = nameof(NoGamepad);

        /// <summary>GAMEPAD {0}</summary>
        public const string GamepadN = nameof(GamepadN);

        /// <summary>PLAYER VS PLAYER</summary>
        public const string PlayerVsPlayer = nameof(PlayerVsPlayer);

        /// <summary>PLAYER {0} ({1}): PICK YOUR CLUB</summary>
        public const string VersusPick = nameof(VersusPick);

        /// <summary>CUSTOM</summary>
        public const string RulesCustom = nameof(RulesCustom);

        /// <summary>SKATE UP</summary>
        public const string ActionSkateUp = nameof(ActionSkateUp);

        /// <summary>SKATE DOWN</summary>
        public const string ActionSkateDown = nameof(ActionSkateDown);

        /// <summary>SKATE LEFT</summary>
        public const string ActionSkateLeft = nameof(ActionSkateLeft);

        /// <summary>SKATE RIGHT</summary>
        public const string ActionSkateRight = nameof(ActionSkateRight);

        /// <summary>SPRINT</summary>
        public const string ActionSprint = nameof(ActionSprint);

        /// <summary>SHOOT</summary>
        public const string ActionShoot = nameof(ActionShoot);

        /// <summary>PASS</summary>
        public const string ActionPass = nameof(ActionPass);

        /// <summary>CHECK</summary>
        public const string ActionCheck = nameof(ActionCheck);

        /// <summary>DEKE</summary>
        public const string ActionDeke = nameof(ActionDeke);

        /// <summary>SWITCH</summary>
        public const string ActionSwitch = nameof(ActionSwitch);

        /// <summary>LINE CHANGE</summary>
        public const string ActionLineChange = nameof(ActionLineChange);

        /// <summary>BLOCK</summary>
        public const string ActionBlock = nameof(ActionBlock);

        /// <summary>ACCEPT FIGHT</summary>
        public const string ActionAcceptFight = nameof(ActionAcceptFight);

        /// <summary>SWAP KITS</summary>
        public const string ActionKit = nameof(ActionKit);

        /// <summary>CAMERA ZOOM</summary>
        public const string ActionZoom = nameof(ActionZoom);

        /// <summary>PAUSE</summary>
        public const string ActionPause = nameof(ActionPause);

        /// <summary>RESET TO DEFAULTS</summary>
        public const string ResetDefaults = nameof(ResetDefaults);

        /// <summary>KEYBOARD</summary>
        public const string Keyboard = nameof(Keyboard);

        /// <summary>GAMEPAD</summary>
        public const string Gamepad = nameof(Gamepad);

        /// <summary>PRESS A KEY</summary>
        public const string PressAKey = nameof(PressAKey);

        /// <summary>OR BUTTON</summary>
        public const string OrButton = nameof(OrButton);

        /// <summary>PRESS A KEY OR PAD BUTTON   ESC CANCELS</summary>
        public const string HintListening = nameof(HintListening);

        /// <summary>ENTER REBINDS   ESC BACK</summary>
        public const string HintKeys = nameof(HintKeys);

        /// <summary>ON</summary>
        public const string RuleValueOn = nameof(RuleValueOn);

        /// <summary>OFF</summary>
        public const string RuleValueOff = nameof(RuleValueOff);

        /// <summary>HYBRID</summary>
        public const string RuleValueHybrid = nameof(RuleValueHybrid);

        /// <summary>TOUCH</summary>
        public const string RuleValueTouch = nameof(RuleValueTouch);

        /// <summary>3 ON 3 + SO</summary>
        public const string RuleValueThreeOnThree = nameof(RuleValueThreeOnThree);

        /// <summary>5 ON 5 SUDDEN</summary>
        public const string RuleValueSuddenDeath = nameof(RuleValueSuddenDeath);

        /// <summary>SHOOTOUT</summary>
        public const string RuleValueShootoutOnly = nameof(RuleValueShootoutOnly);

        /// <summary>TIE</summary>
        public const string RuleValueTie = nameof(RuleValueTie);

        /// <summary>LENIENT</summary>
        public const string RuleValueLenient = nameof(RuleValueLenient);

        /// <summary>NORMAL</summary>
        public const string RuleValueNormal = nameof(RuleValueNormal);

        /// <summary>STRICT</summary>
        public const string RuleValueStrict = nameof(RuleValueStrict);

        /// <summary>AWAY</summary>
        public const string RuleValueAway = nameof(RuleValueAway);

        /// <summary>HOME</summary>
        public const string RuleValueHome = nameof(RuleValueHome);

        /// <summary>HOME CHOOSES</summary>
        public const string RuleValueHomeChooses = nameof(RuleValueHomeChooses);

        /// <summary>5 + EJECTION</summary>
        public const string RuleValueMajorAndEjection = nameof(RuleValueMajorAndEjection);

        /// <summary>LOSER 2 MIN</summary>
        public const string RuleValueLoserMinor = nameof(RuleValueLoserMinor);

        /// <summary>5 MIN MAJOR</summary>
        public const string RuleValueMajor = nameof(RuleValueMajor);

        /// <summary>FULL</summary>
        public const string RuleValueFull = nameof(RuleValueFull);

        /// <summary>MINORS ONLY</summary>
        public const string RuleValueMinorsOnly = nameof(RuleValueMinorsOnly);

        /// <summary>SUBSTITUTE</summary>
        public const string RuleValueSubstitute = nameof(RuleValueSubstitute);

        /// <summary>4 ON 4</summary>
        public const string RuleValueFourOnFour = nameof(RuleValueFourOnFour);

        /// <summary>OFFSIDE</summary>
        public const string RuleOffside = nameof(RuleOffside);

        /// <summary>GAME</summary>
        public const string RuleGroupGame = nameof(RuleGroupGame);

        /// <summary>ICING</summary>
        public const string RuleIcing = nameof(RuleIcing);

        /// <summary>GOALIE PULL</summary>
        public const string RuleGoaliePull = nameof(RuleGoaliePull);

        /// <summary>OVERTIME</summary>
        public const string RuleOvertime = nameof(RuleOvertime);

        /// <summary>OVERTIME</summary>
        public const string RuleGroupOvertime = nameof(RuleGroupOvertime);

        /// <summary>PLAYOFF OVERTIME</summary>
        public const string RulePlayoffOvertime = nameof(RulePlayoffOvertime);

        /// <summary>SHOOTOUT SHOOTERS</summary>
        public const string RuleShootoutShooters = nameof(RuleShootoutShooters);

        /// <summary>SHOOTS FIRST</summary>
        public const string RuleShootoutFirst = nameof(RuleShootoutFirst);

        /// <summary>PENALTIES</summary>
        public const string RulePenalties = nameof(RulePenalties);

        /// <summary>PENALTIES</summary>
        public const string RuleGroupPenalties = nameof(RuleGroupPenalties);

        /// <summary>REFEREE</summary>
        public const string RuleStrictness = nameof(RuleStrictness);

        /// <summary>CARRY INTO OT</summary>
        public const string RulePenaltiesCarryIntoOvertime = nameof(RulePenaltiesCarryIntoOvertime);

        /// <summary>THIRD PENALTY WAITS</summary>
        public const string RuleStackPenalties = nameof(RuleStackPenalties);

        /// <summary>COINCIDENTAL MINORS</summary>
        public const string RuleCoincidentalMinors = nameof(RuleCoincidentalMinors);

        /// <summary>PENALTY LENGTHS</summary>
        public const string RulePenaltySeverity = nameof(RulePenaltySeverity);

        /// <summary>INTERFERENCE</summary>
        public const string RuleInterference = nameof(RuleInterference);

        /// <summary>INFRACTIONS</summary>
        public const string RuleGroupInfractions = nameof(RuleGroupInfractions);

        /// <summary>HOLDING</summary>
        public const string RuleHolding = nameof(RuleHolding);

        /// <summary>HIGH-STICKING</summary>
        public const string RuleHighSticking = nameof(RuleHighSticking);

        /// <summary>CROSS-CHECKING</summary>
        public const string RuleCrossChecking = nameof(RuleCrossChecking);

        /// <summary>BOARDING</summary>
        public const string RuleBoarding = nameof(RuleBoarding);

        /// <summary>ROUGHING</summary>
        public const string RuleRoughing = nameof(RuleRoughing);

        /// <summary>NO CHANGE AFTER ICING</summary>
        public const string RuleIcingNoChange = nameof(RuleIcingNoChange);

        /// <summary>FIGHTS</summary>
        public const string RuleFights = nameof(RuleFights);

        /// <summary>FIGHTS</summary>
        public const string RuleGroupFights = nameof(RuleGroupFights);

        /// <summary>FIGHT PENALTY</summary>
        public const string RuleFightPenalty = nameof(RuleFightPenalty);

        /// <summary>PRESET</summary>
        public const string Preset = nameof(Preset);

        /// <summary>- {0} -</summary>
        public const string RuleGroupHeading = nameof(RuleGroupHeading);

        /// <summary>RESET TO PRESET</summary>
        public const string ResetToPreset = nameof(ResetToPreset);

        /// <summary>CHANGES PLAY FROM THE NEXT MATCH   ESC BACK</summary>
        public const string HintRulesNextMatch = nameof(HintRulesNextMatch);

        /// <summary>LEFT RIGHT CHANGES   * NOT THE PRESET   ESC BACK</summary>
        public const string HintRules = nameof(HintRules);

        /// <summary>END OF OVERTIME</summary>
        public const string EndOfOvertime = nameof(EndOfOvertime);

        /// <summary>END OF PERIOD {0}</summary>
        public const string EndOfPeriod = nameof(EndOfPeriod);

        /// <summary>{0} {1} - {2} {3}</summary>
        public const string ScoreLine = nameof(ScoreLine);

        /// <summary>FINAL - TIE</summary>
        public const string FinalTie = nameof(FinalTie);

        /// <summary>OFFSIDE</summary>
        public const string Offside = nameof(Offside);

        /// <summary>ICING</summary>
        public const string Icing = nameof(Icing);

        /// <summary>{0} {1}</summary>
        public const string PenaltyBanner = nameof(PenaltyBanner);

        /// <summary>#{0} {1}</summary>
        public const string PlayerTag = nameof(PlayerTag);

        /// <summary>FIRST GOAL OF THE SEASON</summary>
        public const string FirstGoalOfSeason = nameof(FirstGoalOfSeason);

        /// <summary>HAT TRICK!</summary>
        public const string HatTrick = nameof(HatTrick);

        /// <summary>GOAL!</summary>
        public const string Goal = nameof(Goal);

        /// <summary>GAME-WINNING GOAL #{0} {1}</summary>
        public const string GameWinningGoal = nameof(GameWinningGoal);

        /// <summary>SHUTOUT #{0} {1}</summary>
        public const string Shutout = nameof(Shutout);

        /// <summary>TRIPPING</summary>
        public const string InfTripping = nameof(InfTripping);

        /// <summary>HOOKING</summary>
        public const string InfHooking = nameof(InfHooking);

        /// <summary>SLASHING</summary>
        public const string InfSlashing = nameof(InfSlashing);

        /// <summary>INTERFERENCE</summary>
        public const string InfInterference = nameof(InfInterference);

        /// <summary>HOLDING</summary>
        public const string InfHolding = nameof(InfHolding);

        /// <summary>HIGH-STICKING</summary>
        public const string InfHighSticking = nameof(InfHighSticking);

        /// <summary>CROSS-CHECKING</summary>
        public const string InfCrossChecking = nameof(InfCrossChecking);

        /// <summary>BOARDING</summary>
        public const string InfBoarding = nameof(InfBoarding);

        /// <summary>ROUGHING</summary>
        public const string InfRoughing = nameof(InfRoughing);

        /// <summary>CHECKING FROM BEHIND</summary>
        public const string InfCheckingFromBehind = nameof(InfCheckingFromBehind);

        /// <summary>FIGHTING</summary>
        public const string InfFighting = nameof(InfFighting);

        /// <summary>PENALTY</summary>
        public const string InfPenalty = nameof(InfPenalty);

        /// <summary>PP</summary>
        public const string PowerPlay = nameof(PowerPlay);

        /// <summary> {0}V{1}</summary>
        public const string PowerPlayStrength = nameof(PowerPlayStrength);

        /// <summary>OT</summary>
        public const string PeriodOt = nameof(PeriodOt);

        /// <summary>SO</summary>
        public const string PeriodSo = nameof(PeriodSo);

        /// <summary>{0} WIN {1}-{2}</summary>
        public const string ShootoutWin = nameof(ShootoutWin);

        /// <summary>ROUND {0}  #{1} {2}</summary>
        public const string ShootoutRound = nameof(ShootoutRound);

        /// <summary>VS</summary>
        public const string Vs = nameof(Vs);

        /// <summary>#{0} {1} GM</summary>
        public const string PenaltyBoxGm = nameof(PenaltyBoxGm);

        /// <summary>#{0} {1}</summary>
        public const string PenaltyBoxRow = nameof(PenaltyBoxRow);

        /// <summary>#{0} NEXT {1}</summary>
        public const string PenaltyBoxNext = nameof(PenaltyBoxNext);

        /// <summary>#{0} {1} MISC</summary>
        public const string PenaltyBoxMisc = nameof(PenaltyBoxMisc);

        /// <summary>DELAYED</summary>
        public const string Delayed = nameof(Delayed);

        /// <summary>EXTRA ATTACKER</summary>
        public const string ExtraAttacker = nameof(ExtraAttacker);

        /// <summary>{0} SHOOTS   {1} FIRST   {2} SECOND</summary>
        public const string ShootoutChoice = nameof(ShootoutChoice);

        /// <summary>SHOOT</summary>
        public const string ChoiceShoot = nameof(ChoiceShoot);

        /// <summary>PASS</summary>
        public const string ChoicePass = nameof(ChoicePass);

        /// <summary>GLOVES OFF!</summary>
        public const string GlovesOff = nameof(GlovesOff);

        /// <summary>F OR Y: FIGHT</summary>
        public const string FightHint = nameof(FightHint);

        /// <summary>FIGHT!</summary>
        public const string Fight = nameof(Fight);

        /// <summary>ROUND {0}</summary>
        public const string FightRound = nameof(FightRound);

        /// <summary>DOWN!</summary>
        public const string FightDown = nameof(FightDown);

        /// <summary>BROKEN UP</summary>
        public const string FightBrokenUp = nameof(FightBrokenUp);

        /// <summary>SPACE/A PUNCH  CTRL/RB BLOCK</summary>
        public const string FightKeys = nameof(FightKeys);

        /// <summary>WARM-UP   PRESS START</summary>
        public const string WarmUp = nameof(WarmUp);

        /// <summary>ENTER  SKIP</summary>
        public const string Skip = nameof(Skip);

        /// <summary>MOMENTUM</summary>
        public const string Momentum = nameof(Momentum);

        /// <summary>REPLAY</summary>
        public const string Replay = nameof(Replay);

        /// <summary>SHOOTOUT</summary>
        public const string Shootout = nameof(Shootout);

        /// <summary>SHOOTOUT WINNER</summary>
        public const string ShootoutWinner = nameof(ShootoutWinner);

        /// <summary>{0}OT</summary>
        public const string OvertimeNumber = nameof(OvertimeNumber);
    }
}
