using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Offside;
using StickAndPuck.App.Rules.Penalties;

namespace StickAndPuck.App.Rules.Match
{
    /// <summary>The goal review (W79, W80, W81). Under <see cref="RuleSet.GoalReview"/> a puck that goes in during play
    /// is not a goal yet: physics publishes <see cref="GoalUnderReview"/> instead of <see cref="GoalScored"/>, play
    /// stops and the officials look at it for a moment (longer when an attacker was near the goalie). An attacker who
    /// ran into the goalie in or around his crease as it went in disallows it for goalie interference
    /// (<see cref="GoalDisallowed"/>, faceoff at the neutral-zone dot outside that zone); otherwise the goal is
    /// confirmed and published as <see cref="GoalScored"/>, which is when the score, stats, crowd, horn, replay and
    /// momentum react to it.</summary>
    /// <remarks>
    /// Under <see cref="RuleSet.GoalChallenge"/> (the NHL) a goal that stood may then be challenged, once a game, by
    /// the club scored on: for goalie interference (holds when an attacker stood in the crease as it went in) or for
    /// offside on the zone entry (holds when the linesman missed one, see <see cref="OffsideRule.MissedOffside"/>).
    /// A human coach is asked (pass: interference, shoot: offside, start or waiting it out lets the goal stand); the AI
    /// challenges now and then when it has a case and very rarely when it has none. A challenge that holds disallows
    /// the goal; a failed one lets it stand and costs a delay-of-game minor. Start (the pause button) skips the review.
    /// </remarks>
    public static class GoalReview
    {
        /// <summary>The review of a goal with nobody near the goalie.</summary>
        public const float QuickSeconds = 1.5f;

        /// <summary>The review of a goal with an attacker in the crease or on the goalie.</summary>
        public const float LongSeconds = 3f;

        /// <summary>How long a human coach is asked whether to challenge before the goal stands.</summary>
        public const float PromptSeconds = 5f;

        /// <summary>The look at a coach's challenge.</summary>
        public const float ChallengeSeconds = 3f;

        /// <summary>Contact this long before the goal or less is goalie interference.</summary>
        public const float ContactWindow = 0.5f;

        /// <summary>An attacker in the crease this long before the goal or less is a case for a challenge.</summary>
        public const float CreaseWindow = 0.25f;

        /// <summary>Attacker and goalie closer than this, centre to centre, are in contact (two bodies touching).</summary>
        public const float ContactDistance = 0.75f;

        /// <summary>How far around the crease a goalie still counts as in it for contact.</summary>
        public const float AroundCrease = 0.6f;

        /// <summary>The chance an AI coach challenges a goal it has a case against.</summary>
        public const float AiChallenge = 0.25f;

        /// <summary>The chance an AI coach challenges a goal with no case: a gamble that costs a minor.</summary>
        public const float AiGamble = 0.01f;

        /// <summary>Seconds after the review begins before start can skip it, so a press meant for something else does not.</summary>
        public const float SkipAfter = 0.3f;

        /// <summary>Gets a value indicating whether a goal going in now is held for review: the rule is on and the match flow is in play.</summary>
        public static bool Holds(World world)
        {
            return world.Setup.Rules.GoalReview
                && MatchFlow.IsRunning(world)
                && world.Match.Phase == MatchPhase.Play;
        }

        /// <summary>Gets a value indicating whether a goal is held under review (or waits on a coach's challenge) now.</summary>
        public static bool UnderReview(World world)
        {
            return MatchFlow.IsRunning(world) && MatchFlow.State(world).Review.Goal.HasValue;
        }

        /// <summary>Gets the club whose human coach is being asked whether to challenge the goal, or None.</summary>
        public static Maybe<Side> Asking(World world)
        {
            return
                UnderReview(world)
                && MatchFlow.State(world).Review is { Stage: ReviewStage.Prompt } r
                && r.Goal.TryGet(out GoalUnderReview held)
                ? Other(held.Goal.Side)
                : Maybe.None<Side>();
        }

        /// <summary>Gets a value indicating whether the goal confirmed last stood after a failed coach's challenge.</summary>
        public static bool ChallengeFailed(World world)
        {
            return MatchFlow.IsRunning(world) && MatchFlow.State(world).Review.Failed;
        }

        /// <summary>Gets a value indicating whether a club has used its coach's challenge this match.</summary>
        public static bool ChallengeUsed(World world, Side side)
        {
            return MatchFlow.State(world).Review.Used[(int)side];
        }

        /// <summary>Skips the stage under way at the next step, as start does; for tests and scripts.</summary>
        internal static void Skip(World world)
        {
            MatchFlow.State(world).Review.SkipAsked = true;
        }

        /// <summary>Notes, each step of play, when an attacker touches a goalie in or around his crease, or stands in the crease.</summary>
        internal static void Watch(World world)
        {
            if (!world.Setup.Rules.GoalReview && !world.Setup.Rules.GoalChallenge)
            {
                return;
            }

            ReviewState r = MatchFlow.State(world).Review;
            foreach (int end in (ReadOnlySpan<int>)[-1, 1])
            {
                Side defender = end < 0 ? world.Match.DefendsLeft : Other(world.Match.DefendsLeft);
                if (world.OnIce(defender).FirstOrDefault(p => p.IsGoalie) is not { } goalie)
                {
                    continue; // an empty net: nobody to get in the way of
                }

                bool goalieIn = RinkSpec.InCrease(goalie.Position, end, AroundCrease);
                foreach (
                    Vector3 a in world
                        .OnIce(Other(defender))
                        .Where(p => !p.IsGoalie)
                        .Select(p => p.Position)
                )
                {
                    if (
                        goalieIn
                        && RinkSpec.InCrease(a, end)
                        && Flat(a - goalie.Position) < ContactDistance
                    )
                    {
                        r.ContactAt[Index(end)] = world.Time;
                    }

                    if (RinkSpec.InCrease(a, end))
                    {
                        r.CreaseAt[Index(end)] = world.Time;
                    }
                }
            }
        }

        /// <summary>A goal went in under review: play stops, the puck stays in the net and the officials take a look.</summary>
        internal static void Begin(World world, FlowState s, GoalUnderReview held)
        {
            ReviewState r = s.Review;
            int end = held.Goal.Position.X < 0 ? -1 : 1;
            r.Goal = held;
            r.Stage = ReviewStage.Review;
            r.Held = 0;
            r.Failed = false;
            r.Armed = false;
            r.SkipAsked = false;
            r.Contact = world.Time - r.ContactAt[Index(end)] <= ContactWindow;
            r.InCrease = world.Time - r.CreaseAt[Index(end)] <= CreaseWindow;
            r.Offside = OffsideRule.MissedOffside(world, held.Goal.Side);
            r.Timer = r.Contact || r.InCrease ? LongSeconds : QuickSeconds;
            world.Match.Phase = MatchPhase.Stopped;
            world.Puck.Live = false;
            s.Timer = float.PositiveInfinity; // the review hands the stoppage on itself
            s.Dot = RinkSpec.CentreDot;
            s.Draw.Dropped = false;
            Log(
                world,
                $"goal under review ({(r.Contact ? "contact with the goalie" : r.InCrease ? "attacker in the crease" : "clean")}{(r.Offside ? ", missed offside on the entry" : "")})"
            );
        }

        /// <summary>Runs the review one step; true while a goal is held, so the match flow waits. Also calls a failed
        /// challenge's minor the step after its goal counted.</summary>
        internal static bool Update(World world, FlowState s, float dt)
        {
            ReviewState r = s.Review;
            if (r.Minor.TryGet(out Side owes))
            {
                r.Minor = Maybe.None<Side>();
                FailedMinor(world, s, owes);
            }

            if (!r.Goal.TryGet(out GoalUnderReview held))
            {
                return false;
            }

            r.Held += dt;
            r.Timer -= dt;
            bool skip = Skipped(world, r);
            switch (r.Stage)
            {
                case ReviewStage.Review:
                    if (r.Timer <= 0 || skip)
                    {
                        Reviewed(world, s, held);
                    }

                    break;
                case ReviewStage.Prompt:
                    Prompt(world, s, held, skip);
                    break;
                case ReviewStage.Challenge:
                    if (r.Timer <= 0 || skip)
                    {
                        Challenged(world, s, held);
                    }

                    break;
                default:
                    break;
            }

            return true;
        }

        /// <summary>The officials' look is over: interference takes the goal off, otherwise a challenge may follow, or it counts.</summary>
        private static void Reviewed(World world, FlowState s, GoalUnderReview held)
        {
            ReviewState r = s.Review;
            if (r.Contact)
            {
                Disallow(world, s, held, ReviewCall.GoalieInterference, challenged: false);
                return;
            }

            Side against = Other(held.Goal.Side);
            if (!world.Setup.Rules.GoalChallenge || r.Used[(int)against])
            {
                Confirm(world, s, held);
                return;
            }

            if (Human(world, against))
            {
                r.Stage = ReviewStage.Prompt;
                r.Timer = PromptSeconds;
                r.LastAnswer = Answers(world, against);
                return;
            }

            // the AI coach: a case now and then, a gamble very rarely
            float roll = (float)r.Dice.NextDouble();
            if (r.Offside && roll < AiChallenge)
            {
                Challenge(world, s, against, ReviewCall.Offside);
            }
            else if (r.InCrease && roll < AiChallenge)
            {
                Challenge(world, s, against, ReviewCall.GoalieInterference);
            }
            else if (!r.Offside && !r.InCrease && roll < AiGamble)
            {
                Challenge(world, s, against, ReviewCall.GoalieInterference);
            }
            else
            {
                Confirm(world, s, held);
            }
        }

        /// <summary>The human coach of the club scored on answers: pass challenges for interference, shoot for offside, start or the clock lets the goal stand.</summary>
        private static void Prompt(World world, FlowState s, GoalUnderReview held, bool skip)
        {
            ReviewState r = s.Review;
            Side against = Other(held.Goal.Side);
            (bool pass, bool shoot) = Answers(world, against);
            (bool wasPass, bool wasShoot) = r.LastAnswer;
            r.LastAnswer = (pass, shoot);
            if (pass && !wasPass)
            {
                Challenge(world, s, against, ReviewCall.GoalieInterference);
            }
            else if (shoot && !wasShoot)
            {
                Challenge(world, s, against, ReviewCall.Offside);
            }
            else if (skip || r.Timer <= 0)
            {
                Confirm(world, s, held);
            }
        }

        private static void Challenge(World world, FlowState s, Side side, ReviewCall call)
        {
            ReviewState r = s.Review;
            r.Used[(int)side] = true;
            r.Challenge = call;
            r.Stage = ReviewStage.Challenge;
            r.Timer = ChallengeSeconds;
            r.Armed = false;
            world.Events.Publish(new CoachChallenged(world.Time, side, call));
            Log(world, $"coach's challenge by {world.Setup.Club(side).ShortName}: {call}");
        }

        /// <summary>The challenge has been looked at: it holds, and the goal is off, or it fails, and the goal counts with a minor to follow.</summary>
        private static void Challenged(World world, FlowState s, GoalUnderReview held)
        {
            ReviewState r = s.Review;
            bool holds = r.Challenge == ReviewCall.Offside ? r.Offside : r.InCrease || r.Contact;
            if (holds)
            {
                Disallow(world, s, held, r.Challenge, challenged: true);
                return;
            }

            r.Failed = true;
            r.Minor = Other(held.Goal.Side);
            Confirm(world, s, held);
        }

        /// <summary>The goal counts: published as <see cref="GoalScored"/> now, with the seconds it was held.</summary>
        private static void Confirm(World world, FlowState s, GoalUnderReview held)
        {
            ReviewState r = s.Review;
            r.Goal = Maybe.None<GoalUnderReview>();
            GoalScored goal = held.Goal with { Time = world.Time, ReviewSeconds = r.Held };
            world.Events.Publish(goal);
            MatchRules.CountGoal(world, s, goal);
        }

        /// <summary>No goal: play restarts with a faceoff at the neutral-zone dot outside the zone it went in at.</summary>
        private static void Disallow(
            World world,
            FlowState s,
            GoalUnderReview held,
            ReviewCall call,
            bool challenged
        )
        {
            ReviewState r = s.Review;
            r.Goal = Maybe.None<GoalUnderReview>();
            GoalScored g = held.Goal;
            int end = g.Position.X < 0 ? -1 : 1;
            world.Events.Publish(
                new GoalDisallowed(world.Time, g.Side, g.ScorerId, g.Position, call, challenged)
            );
            world.Match.Phase = MatchPhase.Stopped;
            s.Timer = MatchTuning.StoppageSeconds;
            s.Dot = OffsideRule.FaceoffDot(end, g.Position);
            s.Draw.Dropped = false;
            Log(world, $"no goal, {call}{(challenged ? " on a coach's challenge" : "")}");
        }

        /// <summary>The failed challenge's delay-of-game minor, on the club's goalie (a skater serves it); the goal's pause and centre faceoff carry on.</summary>
        private static void FailedMinor(World world, FlowState s, Side side)
        {
            MatchState m = world.Match;
            Maybe<Player> who =
                world.OnIce(side).FirstOrDefault(p => p.IsGoalie) is { } goalie ? goalie
                : world.OnIce(side).FirstOrDefault() is { } skater ? skater
                : Maybe.None<Player>();
            if (m.Phase != MatchPhase.Goal || !who.TryGet(out Player offender))
            {
                return;
            }

            float timer = s.Timer;
            Vector3 dot = s.Dot;
            if (DelayOfGame.Call(world, offender.Id, "failed coach's challenge"))
            {
                // the call stops play for a faceoff in its zone; after a goal the faceoff is at centre, after the goal's pause
                m.Phase = MatchPhase.Goal;
                s.Timer = timer;
                s.Dot = dot;
            }
        }

        /// <summary>Whether start was pressed by a human (after <see cref="SkipAfter"/>, once every human's start was seen up), or a skip was asked for.</summary>
        private static bool Skipped(World world, ReviewState r)
        {
            bool down = world.Players.Any(p => p.Slot >= 0 && p.Command.Pause);
            bool asked = r.SkipAsked;
            r.SkipAsked = false;
            if (!down)
            {
                r.Armed = true;
                return asked;
            }

            return asked || (r.Armed && r.Held >= SkipAfter);
        }

        private static (bool Pass, bool Shoot) Answers(World world, Side side)
        {
            bool pass = false,
                shoot = false;
            foreach (Player p in world.Players)
            {
                if (p.Side == side && p.Slot >= 0)
                {
                    pass |= p.Command.Pass;
                    shoot |= p.Command.Shoot;
                }
            }

            return (pass, shoot);
        }

        private static bool Human(World world, Side side)
        {
            return world.Players.Any(p => p.Side == side && p.Slot >= 0);
        }

        private static float Flat(Vector3 v)
        {
            return new Vector2(v.X, v.Z).Length();
        }

        private static int Index(int end)
        {
            return end < 0 ? 0 : 1;
        }

        private static Side Other(Side side)
        {
            return side == Side.Home ? Side.Away : Side.Home;
        }

        private static void Log(World world, string what)
        {
            if (MatchFlow.State(world).Log)
            {
                Console.WriteLine($"SAP: review: {what}");
            }
        }
    }
}
