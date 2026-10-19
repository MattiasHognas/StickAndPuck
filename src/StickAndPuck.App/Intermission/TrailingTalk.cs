using StickAndPuck.App.Core;

namespace StickAndPuck.App.Intermission
{
    /// <summary>The trailing club's intermission talk (<see cref="RuleSet.IntermissionTalk"/>). When a club a human
    /// plays is behind after period 1 or 2, the Zamboni break opens a one-choice talk: push or stay patient.
    /// The human picks with the stick (left push, right patient) and answers with shoot or pass; enter still
    /// skips the break and leaves the talk unanswered. The answer comes only from the player's command,
    /// which the input system builds from the device frames the replay records (or a scripted press,
    /// recorded in the same frame), so a replay plays the same choice. The AI club gets no talk.
    /// <para>The answer holds for the next period, scaled by <see cref="RuleSet.IntermissionTalkScale"/>: push shortens
    /// the shifts (the lines change earlier), forechecks 2-1-2 with the defence pinching and takes riskier
    /// passes, so there are more chances both ways; stay patient keeps the defence home (no aggressive
    /// forecheck) and weighs a lost pass far heavier, so the club makes fewer risky passes.</para></summary>
    public static class TrailingTalk
    {
        /// <summary>Answers are ignored for this long after the talk opens, so the button that ended the period does not answer it.</summary>
        public const float AnswerAfter = 0.5f;

        /// <summary>How long the coach's reply shows after the answer.</summary>
        public const float SaidSeconds = 2.5f;

        /// <summary>How much earlier, in energy, a pushing club's lines change at scale 1.</summary>
        public const float ShiftEarlier = 0.15f;

        /// <summary>How much less a pushing club weighs losing a pass at scale 1.</summary>
        public const float RiskierPasses = 0.4f;

        /// <summary>How much more a patient club weighs losing a pass at scale 1.</summary>
        public const float SaferPasses = 2f;

        /// <summary>The least chance, at scale 1, a patient club's carrier gives a pass before he makes it.</summary>
        public const float PatientPassChance = 0.5f;

        /// <summary>How far the stick goes over before it picks an answer.</summary>
        private const float PickStick = 0.5f;

        /// <summary>Gets the club a talk opens for when a break follows a period: a club a human plays, behind
        /// on the scoreboard after period 1 or 2 (any regulation period but the last), with the rule on.</summary>
        public static Maybe<Side> Trailing(World world, int period, bool intoOvertime)
        {
            if (
                !world.Setup.Rules.IntermissionTalk
                || intoOvertime
                || period >= world.Setup.Periods
            )
            {
                return Maybe.None<Side>();
            }

            int[] score = world.Match.Score;
            foreach (Side side in (ReadOnlySpan<Side>)[Side.Home, Side.Away])
            {
                if (
                    score[(int)side] < score[1 - (int)side]
                    && world.Players.Exists(p => p.Side == side && p.Slot >= 0)
                )
                {
                    return side;
                }
            }

            return Maybe.None<Side>();
        }

        /// <summary>Gets the club the talk is asking right now, or None.</summary>
        public static Maybe<Side> Asking(World world)
        {
            return world.Talk.TryGet(out TalkState s) ? s.Asking : Maybe.None<Side>();
        }

        /// <summary>Gets what a club's coach said for the period being played, or None.</summary>
        public static TalkChoice Choice(World world, Side side)
        {
            return
                world.Talk.TryGet(out TalkState s)
                && s.Chosen[(int)side] != TalkChoice.None
                && s.ForPeriod[(int)side] == world.Match.Period
                && world.Match.Phase != MatchPhase.Intermission
                ? s.Chosen[(int)side]
                : TalkChoice.None;
        }

        /// <summary>Gets how much earlier, in energy, a club's tired lines change this period: more than 0 while it pushes.</summary>
        public static float ShiftsEarlier(World world, Side side)
        {
            return Choice(world, side) == TalkChoice.Push
                ? ShiftEarlier * world.Setup.Rules.IntermissionTalkScale
                : 0f;
        }

        /// <summary>Gets the factor on what a club's carriers weigh losing a pass: below 1 while it pushes, above 1 while it stays patient.</summary>
        public static float TurnoverWeight(World world, Side side)
        {
            float scale = world.Setup.Rules.IntermissionTalkScale;
            return Choice(world, side) switch
            {
                TalkChoice.Push => Math.Max(0.2f, 1f - (RiskierPasses * scale)),
                TalkChoice.Patient => 1f + (SaferPasses * scale),
                TalkChoice.None => 1f,
                _ => throw new NotSupportedException("no pass weight for " + Choice(world, side)),
            };
        }

        /// <summary>Gets the least chance a club's carriers give a pass before they make it: 0 as usual, more while it
        /// stays patient, so the riskiest passes are not made at all.</summary>
        public static float SafestPass(World world, Side side)
        {
            return Choice(world, side) == TalkChoice.Patient
                ? PatientPassChance * world.Setup.Rules.IntermissionTalkScale
                : 0f;
        }

        /// <summary>Sets a club's answer for a period directly, for tests and the talk's own answer.</summary>
        internal static void Answer(World world, Side side, TalkChoice choice, int period)
        {
            TalkState s = State(world);
            s.Chosen[(int)side] = choice;
            s.ForPeriod[(int)side] = period;
        }

        /// <summary>Opens the talk for a club in the break before a period.</summary>
        internal static void Open(World world, Side side, int nextPeriod)
        {
            TalkState s = State(world);
            int[] score = world.Match.Score;
            s.Asking = side;
            s.NextPeriod = nextPeriod;
            s.Deficit = score[1 - (int)side] - score[(int)side];
            s.Highlight = TalkChoice.Push;
            s.Shown = 0;
            s.LastAnswer = true; // a button already down when the talk opens must be let go first
            s.Said = Maybe.None<TalkChoice>();
        }

        /// <summary>Closes the talk unanswered, when the break is skipped or ends.</summary>
        internal static void Close(World world)
        {
            if (world.Talk.TryGet(out TalkState s))
            {
                s.Asking = Maybe.None<Side>();
            }
        }

        /// <summary>Reads the asked club's human players for one step: the stick picks, shoot or pass answers.</summary>
        /// <returns>True when the talk was answered this step.</returns>
        internal static bool Update(World world, float dt)
        {
            if (!world.Talk.TryGet(out TalkState s))
            {
                return false;
            }

            if (s.Said.HasValue)
            {
                s.SaidFor -= dt;
                if (s.SaidFor <= 0)
                {
                    s.Said = Maybe.None<TalkChoice>();
                }
            }

            if (!s.Asking.TryGet(out Side side))
            {
                return false;
            }

            s.Shown += dt;
            float stick = 0;
            bool answer = false;
            foreach (Player p in world.Players)
            {
                if (p.Side == side && p.Slot >= 0)
                {
                    stick += p.Command.Move.X;
                    answer |= p.Command.Shoot || p.Command.Pass;
                }
            }

            if (stick <= -PickStick)
            {
                s.Highlight = TalkChoice.Push;
            }
            else if (stick >= PickStick)
            {
                s.Highlight = TalkChoice.Patient;
            }

            bool pressed = answer && !s.LastAnswer;
            s.LastAnswer = answer;
            TalkChoice picked = Scripted(world, s);
            if (picked == TalkChoice.None && pressed && s.Shown >= AnswerAfter)
            {
                picked = s.Highlight;
            }

            if (picked == TalkChoice.None)
            {
                return false;
            }

            Answer(world, side, picked, s.NextPeriod);
            s.Asking = Maybe.None<Side>();
            s.Highlight = picked;
            s.Said = picked;
            s.SaidFor = SaidSeconds;
            return true;
        }

        /// <summary>Gets the state, making it on the first ask.</summary>
        internal static TalkState State(World world)
        {
            return world.Talk.Get(() => new TalkState());
        }

        /// <summary>An answer pressed by script: played back from a replay's frame, or SAP_TALK's press, made into
        /// this step's frame so the replay presses it too.</summary>
        private static TalkChoice Scripted(World world, TalkState s)
        {
            string press = DeviceInput.Current.MenuPress;
            if (
                press.StartsWith("Talk", StringComparison.Ordinal)
                && Enum.TryParse(press.AsSpan(4), out TalkChoice played)
                && played != TalkChoice.None
            )
            {
                return played;
            }

            TalkChoice pick = world.Switches.TalkPick;
            if (pick == TalkChoice.None || DeviceInput.Playing || s.Shown < AnswerAfter * 3)
            {
                return TalkChoice.None;
            }

            DeviceInput.Scripted("Talk" + pick);
            return pick;
        }
    }
}
