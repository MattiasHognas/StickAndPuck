using Raylib_cs;

namespace StickAndPuck.App.Core
{
    /// <summary>The one place match state lives: the setup, the players, the puck, the match, the camera
    /// and this step's events. Systems read all of it and change only their own part.</summary>
    public sealed class World
    {
        private readonly List<int>[] onIce =
        [
            [],
            [],
        ];
        private int onIceVersion = -1;

        /// <summary>Initializes a new world for a match.</summary>
        public World(MatchSetup setup, Settings settings)
        {
            Setup = AI.Difficulty.DifficultyScale.Apply(setup, settings.Difficulty); // easy and hard scale the AI club's traits
            Settings = settings;
            Match.Clock = setup.PeriodSeconds;
        }

        /// <summary>Gets the match setup.</summary>
        public MatchSetup Setup { get; }

        /// <summary>Gets or sets the player options.</summary>
        public Settings Settings { get; set; }

        /// <summary>Gets every player in the match, both clubs, indexed by <see cref="Player.Id"/>.</summary>
        public PlayerList Players { get; } = [];

        /// <summary>Gets the puck.</summary>
        public Puck Puck { get; } = new();

        /// <summary>Gets the score, clock and flow.</summary>
        public MatchState Match { get; } = new();

        /// <summary>Gets this step's events.</summary>
        public MatchEvents Events { get; } = new();

        /// <summary>Gets or sets the 3D camera every scene and mask pass draws with. The camera system owns it.</summary>
        public Camera3D Camera { get; set; }

        /// <summary>Gets the real seconds the world has run, in fixed steps.</summary>
        public float Time { get; internal set; }

        /// <summary>Gets the number of fixed steps run so far.</summary>
        public long Tick { get; internal set; }

        /// <summary>Gets or sets the match seed: every system's dice are a stream of it, so the same seed
        /// and the same inputs play the same match. 0 is no seed: the dice start from fixed test seeds.
        /// Starts as SAP_SEED (0 when unset); set it before the systems start.</summary>
        public int Seed { get; set; } = Seeds.Environment;

        /// <summary>Gets the arena tones the lighting sets and the arena draws with.</summary>
        public Arena.ArenaLook Look { get; } = new();

        /// <summary>Gets or sets a value indicating whether a goal replay is playing right now. Rules wait on it before the next faceoff.</summary>
        public bool ReplayPlaying { get; internal set; }

        /// <summary>Gets or sets the switches dev scenes and tests turn (these were statics).</summary>
        public WorldSwitches Switches { get; set; } = new();

        /// <summary>Gets or sets what outlives the match: the screens, the sessions, the pads. Game hands the same one to every world it runs.</summary>
        public AppState App { get; set; } = new();

        /// <summary>Gets the dev scenes' loops (these were statics).</summary>
        public DevScenes Dev { get; } = new();

        // Each system's own state, made the first time the system asks for it (these were side tables keyed on the world)

        /// <summary>Gets the AI layer that last set each player's command, by player id (<see cref="AI.AiCommand"/>).</summary>
        internal Slot<Dictionary<int, AI.AiIntent>> AiIntents { get; } = new();

        /// <summary>Gets the intents SAP_AI_TRACE last printed, by player id.</summary>
        internal Slot<Dictionary<int, AI.AiIntent>> AiIntentsPrinted { get; } = new();

        /// <summary>Gets the positioning system's board.</summary>
        internal Slot<AI.Positioning.PositioningBoard> Positioning { get; } = new();

        /// <summary>Gets the decision system's board.</summary>
        internal Slot<AI.Decisions.DecisionBoard> Decisions { get; } = new();

        /// <summary>Gets the defence system's board.</summary>
        internal Slot<AI.Defence.DefenceBoard> Defence { get; } = new();

        /// <summary>Gets the tactics system's board.</summary>
        internal Slot<AI.Tactics.TacticsBoard> Tactics { get; } = new();

        /// <summary>Gets the special teams system's board.</summary>
        internal Slot<AI.SpecialTeams.SpecialTeamsBoard> SpecialTeams { get; } = new();

        /// <summary>Gets the fight in progress and the offer before it.</summary>
        internal Slot<Fights.FightState> Fight { get; } = new();

        /// <summary>Gets the lines, the bench and the changes in progress.</summary>
        internal Slot<Lines.LinesBoard> Lines { get; } = new();

        /// <summary>Gets each club's momentum.</summary>
        internal Slot<Momentum.MomentumState> Momentum { get; } = new();

        /// <summary>Gets or sets the last pass a human made, for the AI receiver to skate onto.</summary>
        internal Maybe<Players.Puck.HumanPass> LastHumanPass { get; set; }

        internal Slot<Referee.RefereeState> Referee { get; } = new();

        /// <summary>Gets the pregame warm-up.</summary>
        internal Slot<Pregame.WarmupState> Warmup { get; } = new();

        /// <summary>Gets the icing rule's state.</summary>
        internal Slot<Rules.Icing.IcingState> Icing { get; } = new();

        /// <summary>Gets the match flow's state.</summary>
        internal Slot<Rules.Match.FlowState> Flow { get; } = new();

        /// <summary>Gets the offside rule's state.</summary>
        internal Slot<Rules.Offside.OffsideState> Offside { get; } = new();

        /// <summary>Gets the overtime and shootout state.</summary>
        internal Slot<Rules.Overtime.OvertimeState> Overtime { get; } = new();

        internal Slot<Rules.Penalties.PenaltyBook> Penalties { get; } = new();

        /// <summary>Adds a player from a club's roster.</summary>
        public Player AddPlayer(Side side, PlayerData data, System.Numerics.Vector3 position)
        {
            Player p = new()
            {
                Id = Players.Count,
                Side = side,
                Data = data,
                Position = position,
            };
            Players.Add(p);
            return p;
        }

        /// <summary>Gets the players of one club that are on the ice.</summary>
        public IEnumerable<Player> OnIce(Side side)
        {
            // Walks the cached list, which matches filtering the players in order; if the players
            // change part way, the rest is filtered as it would have been, from the next player on
            List<int> list = OnIceIndices(side);
            int version = Players.Version,
                next = 0;
            foreach (int i in list)
            {
                if (Players.Version != version)
                {
                    break;
                }

                next = i + 1;
                yield return Players[i];
            }

            if (Players.Version == version)
            {
                yield break;
            }

            for (int i = next; i < Players.Count; i++)
            {
                Player p = Players[i];
                if (p.Side == side && p.OnIce)
                {
                    yield return p;
                }
            }
        }

        /// <summary>Gets the indices of one club's players on the ice, rebuilt after the players change.</summary>
        private List<int> OnIceIndices(Side side)
        {
            if (onIceVersion != Players.Version)
            {
                onIce[0].Clear();
                onIce[1].Clear();
                for (int i = 0; i < Players.Count; i++)
                {
                    if (Players[i].OnIce)
                    {
                        onIce[(int)Players[i].Side].Add(i);
                    }
                }

                onIceVersion = Players.Version;
            }

            return onIce[(int)side];
        }

        /// <summary>Gets the player a human slot controls, if any.</summary>
        public Maybe<Player> Controlled(int slot)
        {
            return Players.Find(p => p.Slot == slot) is { } player ? player : Maybe.None<Player>();
        }
    }
}
