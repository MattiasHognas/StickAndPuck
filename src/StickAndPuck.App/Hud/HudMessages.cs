using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Rules.Penalties;
using StickAndPuck.App.Season.Schedule;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Hud
{
    /// <summary>Turns this step's <see cref="MatchEvents"/> into the banner the HUD shows: goal (with hat trick
    /// and first goal of the season), end of period, final (with the game-winning goal and a shutout),
    /// offside, icing with the club name, and penalties. Keeps a tally of the match's goals so it can
    /// tell a hat trick or the game winner from the events alone; the season stats are not needed.</summary>
    internal sealed class HudMessages
    {
        /// <summary>How long a goal banner stays up, a little under the match's goal pause.</summary>
        public const float GoalSeconds = 3.5f;

        /// <summary>How long an end-of-period banner stays up at least.</summary>
        public const float PeriodSeconds = 4f;

        /// <summary>How long an offside or icing call stays up.</summary>
        public const float CallSeconds = 2f;

        /// <summary>How long a penalty shot banner stays up: through the whistle until the shooter is set at centre.</summary>
        public const float ShotSeconds = 3.5f;

        /// <summary>The longest a review or challenge banner stays up; the verdict replaces it sooner.</summary>
        public const float ReviewSeconds = 30f;

        /// <summary>Goals in a match that make a hat trick.</summary>
        public const int HatTrick = 3;

        private readonly List<(Side Side, int PlayerId)> goals = [];

        /// <summary>Gets or sets how many goals a club has scored earlier this season, or None outside season mode.
        /// The default reads the season being played; the hud scene swaps in a fake.</summary>
        /// <summary>Gets the banner showing now, or None.</summary>
        public Maybe<HudMessage> Current { get; private set; }

        /// <summary>Gets the goals of this match so far, in order: the scoring club and the scorer (-1 when unknown).</summary>
        public IReadOnlyList<(Side Side, int PlayerId)> Goals => goals;

        /// <summary>Reads this step's events. Call from Update; the HUD runs after rules and physics.</summary>
        public void Update(World world)
        {
            foreach (MatchEvent e in world.Events.Current)
            {
                Maybe<HudMessage> next = For(world, e);
                if (next.HasValue)
                {
                    Current = next;
                }
            }
            if (
                Current.TryGet(out HudMessage shown)
                && world.Time >= shown.Until
                && !Held(world, shown)
            )
            {
                Current = Maybe.None<HudMessage>();
            }
        }

        /// <summary>Clears the banner and the goal tally, for a new match.</summary>
        public void Clear()
        {
            Current = Maybe.None<HudMessage>();
            goals.Clear();
        }

        /// <summary>Builds the banner for one event, or None for events the HUD does not announce. Goals and the final are tallied.</summary>
        public Maybe<HudMessage> For(World world, MatchEvent e)
        {
            MatchState m = world.Match;
            string Short(Side s)
            {
                return world.Setup.Club(s).ShortName;
            }

            string Score()
            {
                return Text.Format(
                    K.ScoreLine,
                    Short(Side.Home),
                    m.Score[0],
                    m.Score[1],
                    Short(Side.Away)
                );
            }

            return e switch
            {
                GoalScored goal => Goal(world, goal),
                PeriodEnded end => PeriodEndedBanner(world, end, Score()),
                MatchWon won => MatchWonBanner(world, won, Score()),
                MatchTied => MatchTiedBanner(Score()),
                OffsideCalled => OffsideBanner(e.Time),
                IcingCalled icing => IcingBanner(world, icing, e.Time),
                PenaltyCalled penalty => PenaltyBanner(world, m, penalty, e.Time),
                FaceoffViolated violation => ViolationBanner(world, violation),
                PuckOverGlass over => OverGlassBanner(world, over),
                PenaltyShotAwarded shot => PenaltyShotBanner(world, shot),
                _ => ReviewBanner(world, e),
            };
        }

        /// <summary>The goal review's banners (W79): GOAL UNDER REVIEW while a goal is held, NO GOAL with the reason
        /// when it is taken off, and COACH'S CHALLENGE with what is challenged. The confirmed goal is the GOAL! banner.</summary>
        private static Maybe<HudMessage> ReviewBanner(World world, MatchEvent e)
        {
            return e switch
            {
                GoalUnderReview held => new HudMessage(
                    "panel",
                    Text.Get(K.GoalUnderReview),
                    ScorerText(world, held.Goal),
                    world.Setup.Club(held.Goal.Side).Id,
                    held.Time + ReviewSeconds
                ),
                GoalDisallowed off => new HudMessage(
                    "panel_red",
                    Text.Get(K.NoGoal),
                    CallText(off.Call),
                    world.Setup.Club(off.Side).Id,
                    off.Time + GoalSeconds,
                    "whistle"
                ),
                CoachChallenged c => new HudMessage(
                    "panel",
                    Text.Get(K.CoachChallenge),
                    CallText(c.Call),
                    world.Setup.Club(c.Side).Id,
                    c.Time + ReviewSeconds
                ),
                _ => Maybe.None<HudMessage>(),
            };
        }

        private static string CallText(ReviewCall call)
        {
            return Text.Get(call == ReviewCall.Offside ? K.Offside : K.GoalieInterference);
        }

        private static HudMessage PeriodEndedBanner(World world, PeriodEnded end, string score)
        {
            string title =
                end.Period > world.Setup.Periods
                    ? Text.Get(K.EndOfOvertime)
                    : Text.Format(K.EndOfPeriod, end.Period);
            return new HudMessage(
                "panel",
                title,
                score,
                "",
                end.Time + PeriodSeconds,
                Intermission: true
            );
        }

        private HudMessage MatchWonBanner(World world, MatchWon won, string score)
        {
            return new HudMessage(
                "panel_red",
                Text.Get(K.Final),
                score,
                world.Setup.Club(won.Winner).Id,
                float.PositiveInfinity
            )
            {
                Notes = FinalNotes(world, won.Winner),
            };
        }

        private static HudMessage MatchTiedBanner(string score)
        {
            return new HudMessage(
                "panel_red",
                Text.Get(K.FinalTie),
                score,
                "",
                float.PositiveInfinity
            );
        }

        private static HudMessage OffsideBanner(float at)
        {
            return new HudMessage(
                "panel",
                Text.Get(K.Offside),
                "",
                "",
                at + CallSeconds,
                "whistle"
            );
        }

        private static HudMessage IcingBanner(World world, IcingCalled icing, float at)
        {
            // the club that iced the puck, by name, with its crest
            Side offender = icing.Offender;
            return new HudMessage(
                "panel",
                Text.Get(K.Icing),
                world.Setup.Club(offender).Name,
                world.Setup.Club(offender).Id,
                at + CallSeconds
            );
        }

        private static HudMessage ViolationBanner(World world, FaceoffViolated violation)
        {
            // the skater waved out and the one taking the draw instead, with the club's crest
            PlayerData who = world.Players[violation.PlayerId].Data;
            string line =
                violation.TakerId >= 0 && violation.TakerId != violation.PlayerId
                    ? Text.Format(
                        K.FaceoffViolationSwap,
                        who.Number,
                        who.LastName,
                        world.Players[violation.TakerId].Data.Number,
                        world.Players[violation.TakerId].Data.LastName
                    )
                    : Text.Format(K.PlayerTag, who.Number, who.LastName);
            return new HudMessage(
                "panel",
                Text.Get(K.FaceoffViolation),
                line,
                world.Setup.Club(violation.Side).Id,
                violation.Time + CallSeconds
            );
        }

        private static HudMessage OverGlassBanner(World world, PuckOverGlass over)
        {
            // who put it out, with his club's crest; a delay-of-game call that follows replaces it
            string line = "",
                club = "";
            if (over.PlayerId >= 0 && over.PlayerId < world.Players.Count)
            {
                Player who = world.Players[over.PlayerId];
                line = Text.Format(K.PlayerTag, who.Data.Number, who.Data.LastName);
                club = world.Setup.Club(who.Side).Id;
            }

            return new HudMessage(
                "panel",
                Text.Get(K.PuckOverGlass),
                line,
                club,
                over.Time + CallSeconds
            );
        }

        private static HudMessage PenaltyShotBanner(World world, PenaltyShotAwarded shot)
        {
            // the shooter, with his club's crest, up until he is set at centre
            PlayerData who = world.Players[shot.ShooterId].Data;
            return new HudMessage(
                "panel_gold",
                Text.Get(K.PenaltyShot),
                Text.Format(K.PlayerTag, who.Number, who.LastName),
                world.Setup.Club(shot.Side).Id,
                shot.Time + ShotSeconds
            );
        }

        private static HudMessage PenaltyBanner(
            World world,
            MatchState m,
            PenaltyCalled penalty,
            float at
        )
        {
            // The infraction and minutes, then the player
            PenaltyState called = m.Penalties.LastOrDefault(p => p.PlayerId == penalty.PlayerId);
            PlayerData who = world.Players[penalty.PlayerId].Data;
            return new HudMessage(
                "panel_red",
                Text.Format(
                    K.PenaltyBanner,
                    Infraction.Label(called is { } served ? served.Infraction : ""),
                    MatchFlow.ClockText(penalty.Seconds)
                ),
                Text.Format(K.PlayerTag, who.Number, who.LastName),
                world.Setup.Club(penalty.Side).Id,
                at + CallSeconds + 1
            )
            {
                Notes = TrapezoidNotes(world, penalty.Side),
            };
        }

        /// <summary>A goalie's trapezoid minor (W72), which a skater serves: a line naming the goalie.</summary>
        private static List<string> TrapezoidNotes(World world, Side side)
        {
            return
                world.Events.Of<TrapezoidViolated>().FirstOrDefault(t => t.Side == side) is { } t
                && t.GoalieId >= 0
                && t.GoalieId < world.Players.Count
                ?
                [
                    Text.Format(
                        K.TrapezoidNote,
                        world.Players[t.GoalieId].Data.Number,
                        world.Players[t.GoalieId].Data.LastName
                    ),
                ]
                : [];
        }

        /// <summary>Gets the player's goals so far this match.</summary>
        public int GoalsBy(int playerId)
        {
            return playerId < 0 ? 0 : goals.Count(g => g.PlayerId == playerId);
        }

        /// <summary>A goal: GOAL! with the scorer, HAT TRICK! on the third, plus a note for a club's first goal of the season.</summary>
        private HudMessage Goal(World world, GoalScored e)
        {
            Side scorer = e.Side;
            int clubBefore = goals.Count(g => g.Side == scorer);
            Maybe<int> seasonBefore = world.Switches.SeasonGoals(world, scorer);
            int player = Scorer(world, e).TryGet(out Player p) ? p.Id : -1;
            goals.Add((scorer, player));

            string club = world.Setup.Club(scorer).Id;
            string detail = ScorerText(world, e);
            List<string> notes =
                clubBefore == 0 && seasonBefore == 0 ? [Text.Get(K.FirstGoalOfSeason)] : [];
            if (GoalReview.ChallengeFailed(world))
            {
                notes.Add(Text.Get(K.GoalStands)); // the coach's challenge against it failed
            }

            string title = Text.Get(GoalsBy(player) == HatTrick ? K.HatTrick : K.Goal);
            return new("panel_gold", title, detail, club, e.Time + GoalSeconds) { Notes = notes };
        }

        /// <summary>The final's extra lines: the game-winning goal and a shutout, when the events say so.</summary>
        private List<string> FinalNotes(World world, Side w)
        {
            MatchState m = world.Match;
            Side loser = w == Side.Home ? Side.Away : Side.Home;
            List<string> notes = [];

            // the winner's goal that put them ahead for good: one more than the loser's total.
            // A shootout win adds a goal with no Goal event, so the tally runs short and no winner is named.
            int needed = m.Score[(int)loser] + 1;
            List<(Side Side, int PlayerId)> winners = [.. goals.Where(g => g.Side == w)];
            if (
                needed <= winners.Count
                && winners[needed - 1].PlayerId is var gw
                && gw >= 0
                && gw < world.Players.Count
            )
            {
                PlayerData p = world.Players[gw].Data;
                notes.Add(Text.Format(K.GameWinningGoal, p.Number, p.LastName));
            }

            if (
                m.Score[(int)loser] == 0
                && world.OnIce(w).FirstOrDefault(p => p.IsGoalie) is { } goalie
            )
            {
                notes.Add(Text.Format(K.Shutout, goalie.Data.Number, goalie.Data.LastName));
            }

            return notes;
        }

        /// <summary>Gets the scorer when the event names a player on the scoring club, else None (an own goal).</summary>
        private static Maybe<Player> Scorer(World world, GoalScored e)
        {
            Player p = world.Players[e.ScorerId];
            return p.Side == e.Side ? p : Maybe.None<Player>();
        }

        /// <summary>Gets "#12 SURNAME" for the scorer, or the club name when nobody on that club touched it last.</summary>
        private static string ScorerText(World world, GoalScored e)
        {
            return Scorer(world, e).TryGet(out Player p)
                ? Text.Format(K.PlayerTag, p.Data.Number, p.Data.LastName)
                : world.Setup.Club(e.Side).Name;
        }

        /// <summary>Goals a club scored in the season's earlier games, or None when this match is not a season game.</summary>
        internal static Maybe<int> SeasonGoalsBefore(World world, Side side)
        {
            if (
                !world.App.Season.Current.TryGet(out SeasonState season)
                || !world.App.Season.Playing.HasValue
            )
            {
                return Maybe.None<int>();
            }

            string club = world.Setup.Club(side).Id;
            return season
                .PlayedBy(club)
                .Sum(g =>
                    g.Result.Select(r =>
                            string.Equals(g.Home, club, StringComparison.Ordinal)
                                ? r.HomeGoals
                                : r.AwayGoals
                        )
                        .Or(0)
                );
        }

        /// <summary>An end-of-period banner stays up for the whole intermission.</summary>
        private static bool Held(World world, HudMessage message)
        {
            return message.Intermission && world.Match.Phase == MatchPhase.Intermission;
        }
    }
}
