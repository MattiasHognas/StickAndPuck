using System.Globalization;
using System.Numerics;
using StickAndPuck.App.AI.Decisions;
using StickAndPuck.App.AI.Positioning;
using StickAndPuck.App.Core;
using StickAndPuck.App.Fights;
using StickAndPuck.App.Goalie;
using StickAndPuck.App.Lines;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players.Checks;
using StickAndPuck.App.Players.Movement;
using StickAndPuck.App.Players.Puck;
using StickAndPuck.App.Referee;
using StickAndPuck.App.Rules.Icing;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Rules.Offside;
using StickAndPuck.App.Rules.Penalties;
using StickAndPuck.Tests.Support;
using TUnit.Core.Enums;

namespace StickAndPuck.Tests.Fights
{
    /// <summary>
    /// Scripted fights under the match rules, the bench, the penalty box and the referee, then full AI
    /// matches. Covers that a fight can start, be won by timing, and sends the loser to the box; and
    /// Each preset's fight penalty (Arcade the loser's minor, NHL coincidental majors, SHL majors
    /// and game misconducts that take both fighters off the squad). Formerly SAP_SCENE=fighttest.
    /// </summary>
    public sealed class FightTests : CheckedTest
    {
        private const float Period = 600;

        /// <summary>
        /// A match in play with 10:00 periods, under Arcade unless other rules are given (for the checks of
        /// the loser's two minutes). The scripted hits must not be called as slashing or tripping first,
        /// so it turns the referee's strictness down to 0 before building, as the scene did.
        /// </summary>
        /// <summary>Gets the fight on the ice, if any.</summary>
        private static Maybe<Fight> Current(World w)
        {
            return Fighting.Current(w);
        }

        /// <summary>Gets the offer waiting for an answer, if any.</summary>
        private static Maybe<FightOffer> Pending(World w)
        {
            return Fighting.Pending(w);
        }

        private static T Must<T>(Maybe<T> m)
            where T : notnull
        {
            return m.TryGet(out T v) ? v : throw new InvalidOperationException("expected a value");
        }

        private static World Make(int period = 1, Maybe<RuleSet> rules = default)
        {
            return Make(period, s => s, rules);
        }

        private static World Make(
            int period,
            Func<Settings, Settings> options,
            Maybe<RuleSet> rules = default
        )
        {
            return new WorldBuilder()
                .Setup(s => s with { PeriodSeconds = Period })
                .Switches(s => s.PenaltyStrictness = 0)
                .Rules(rules.Or(RuleSet.Arcade))
                .Settings(options)
                .Then(w =>
                {
                    if (period > 1)
                    {
                        MatchFlow.StartPeriod(w, period, Period);
                    }

                    Run(w, x => x.Match.Phase == MatchPhase.Play, 15);
                })
                .Build();
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
            World,
            ISystem[]
        > Sets = [];

        /// <summary>Physics, the match rules, the bench, penalties, fights and the referee: everything but the players moving themselves.</summary>
        private static ISystem[] Systems(World world)
        {
            return Sets.GetValue(
                world,
                _ =>
                    [
                        new LinesSystem(),
                        new PuckPhysics(),
                        new MatchRules(),
                        new OffsideRule(),
                        new IcingRule(),
                        new PenaltyRules(),
                        new FightSystem(),
                        new RefereeSystem(),
                    ]
            );
        }

        private static void Run(World world, Func<World, bool> until, float limit = 10)
        {
            _ = Run(world, until, _ => { }, limit);
        }

        private static List<MatchEvent> Run(
            World world,
            Func<World, bool> until,
            Action<World> each,
            float limit = 10
        )
        {
            ISystem[] systems = Systems(world);
            List<MatchEvent> events = [];
            for (int i = 0; i < limit / Game.Step && !until(world); i++)
            {
                world.Events.Clear();
                each(world);
                foreach (ISystem s in systems)
                {
                    s.Update(world, Game.Step);
                }

                events.AddRange(world.Events.Current);
                world.Time += Game.Step;
                world.Tick++;
            }
            return events;
        }

        private static Player Skater(World w, Side side, PlayingPosition position)
        {
            return w.OnIce(side).First(p => p.Data.Position == position);
        }

        /// <summary>A hit from the away centre on the home centre, published as checking would.</summary>
        private static List<MatchEvent> Hit(World w, float strength)
        {
            Player checker = Skater(w, Side.Away, PlayingPosition.C);
            Player target = Skater(w, Side.Home, PlayingPosition.C);
            checker.Position = target.Position + new Vector3(0.8f, 0, 0);
            bool sent = false;
            return Run(
                w,
                _ => sent,
                x =>
                {
                    x.Events.Publish(
                        new CheckLanded(x.Time, checker.Side, checker.Id, target.Position, strength)
                    );
                    sent = true;
                },
                1
            );
        }

        /// <summary>Starts a fight between the away centre (checker) and the home centre, both AI unless a slot is set.</summary>
        private static Fight StartFight(World w, bool aiAccepts = true)
        {
            FightState s = Fighting.State(w);
            s.ForceAccept = aiAccepts;
            _ = Fighting.Offer(
                w,
                Skater(w, Side.Away, PlayingPosition.C).Id,
                Skater(w, Side.Home, PlayingPosition.C).Id
            );
            // a human on either side accepts
            _ = Run(
                w,
                x => Current(x).HasValue,
                x =>
                {
                    if (x.Controlled(0).TryGet(out Player me))
                    {
                        me.Command = new PlayerCommand { AcceptFight = true };
                    }
                },
                0.1f
            );
            return Must(Current(w));
        }

        /// <summary>Human slot 0's command each step: punch when the marker first passes <paramref name="at"/>, block as asked.</summary>
        private static Action<World> Human(float at)
        {
            return Human(at, (_, _) => false);
        }

        /// <summary>Human slot 0's command each step: punch when the marker first passes <paramref name="at"/>, block as asked.</summary>
        private static Action<World> Human(float at, Func<World, Fight, bool> block)
        {
            return w =>
            {
                if (!w.Controlled(0).TryGet(out Player me))
                {
                    return;
                }

                PlayerCommand cmd = default;
                if (Current(w).TryGet(out Fight f) && f.Of(me.Id).TryGet(out Fighter mine))
                {
                    cmd.Punch =
                        f.Stage == FightStage.Exchange
                        && !mine.Punched
                        && f.Sweep + (Game.Step / FightTuning.ExchangeSeconds) >= at;
                    cmd.Block = block(w, f);
                }
                me.Command = cmd;
            };
        }

        [Test]
        public void HardCheckStartsFight()
        {
            World w = Make();
            FightState s = Fighting.State(w);
            s.OfferChance = 1;
            s.ForceAccept = true;
            List<MatchEvent> events = Hit(w, 1.8f);
            float clock = w.Match.Clock;
            Maybe<Fight> f = Current(w);
            Player a = Skater(w, Side.Away, PlayingPosition.C);
            Player h = Skater(w, Side.Home, PlayingPosition.C);
            Check(
                f.Is(x => x.Checker.PlayerId == a.Id && x.Victim.PlayerId == h.Id),
                "a hard check (1.8) drops the gloves and both accept: the checker and the skater he hit fight"
            );
            Check(
                events.Exists(e => e is FightStarted)
                    && events.Exists(e => e is WhistleBlown)
                    && w.Match.Phase == MatchPhase.Stopped,
                "the fight publishes a Fight event and the referee whistles play dead"
            );
            Check(
                a.Action == PlayerAction.Fight
                    && h.Action == PlayerAction.Fight
                    && Math.Abs(
                        Vector3.Distance(a.Position, h.Position) - (2 * FightTuning.HalfGap)
                    ) < 0.01f
                    && Fighting
                        .Pose(w, a)
                        .Is(p => string.Equals(p.Clip, "grab", StringComparison.Ordinal)),
                "both square off, an arm's length apart, grabbing sweaters"
            );

            Run(w, x => !Current(x).HasValue, 20);
            Check(
                Math.Abs(w.Match.Clock - clock) < 0.001f,
                "the clock does not run during the fight"
            );
            Check(
                Math.Abs(Vector3.Distance(w.Puck.Position, Must(f).Spot) - 1.2f) < 0.01f,
                "the dead puck lies by the fight, so the camera stays on it"
            );
        }

        [Test]
        public void SoftCheckOrDecline()
        {
            World soft = Make();
            World decline = Make();
            Fighting.State(soft).OfferChance = 1;
            Fighting.State(soft).ForceAccept = true;
            _ = Hit(soft, 1.2f);
            Check(
                !Current(soft).HasValue
                    && !Pending(soft).HasValue
                    && soft.Match.Phase == MatchPhase.Play,
                "a check under 1.5 never starts a fight"
            );

            Fighting.State(decline).OfferChance = 1;
            Fighting.State(decline).ForceAccept = false;
            _ = Hit(decline, 2f);
            Run(decline, _ => false, 0.1f);
            Check(
                !Current(decline).HasValue
                    && !Pending(decline).HasValue
                    && decline.Match.Phase == MatchPhase.Play
                    && Fighting.State(decline).Declined == 1,
                "an AI who declines skates away and play goes on"
            );
        }

        [Test]
        public void HumanAccepts()
        {
            World w = Make();
            Player me = Skater(w, Side.Home, PlayingPosition.C);
            me.Slot = 0;
            FightState s = Fighting.State(w);
            s.ForceAccept = true;
            Player them = Skater(w, Side.Away, PlayingPosition.C);
            _ = Fighting.Offer(w, them.Id, me.Id);
            Run(w, _ => false, 1);
            Check(
                Pending(w).HasValue && !Current(w).HasValue && w.Match.Phase == MatchPhase.Play,
                "an offer to a human waits for his answer while play goes on"
            );
            Run(w, x => !Pending(x).HasValue, FightTuning.OfferSeconds + 0.5f);
            Check(
                !Current(w).HasValue && s.Declined == 1,
                "a human who lets the offer pass declines it"
            );

            _ = Fighting.Offer(w, them.Id, me.Id);
            Run(w, _ => false, 0.5f);
            _ = Run(
                w,
                x => Current(x).HasValue,
                x => Must(x.Controlled(0)).Command = new PlayerCommand { AcceptFight = true },
                0.1f
            );
            Check(Current(w).HasValue, "a human who presses accept (F, or Y on a pad) fights");
        }

        [Test]
        public void WonByTiming()
        {
            World w = Make();
            Player me = Skater(w, Side.Home, PlayingPosition.C);
            Player them = Skater(w, Side.Away, PlayingPosition.C);
            me.Slot = 0;
            FightState s = Fighting.State(w);
            s.AiPunches = false;
            s.AiBlocks = false;
            Fight fight = StartFight(w);
            List<MatchEvent> events = Run(w, x => !Current(x).HasValue, Human(0.5f), 20);
            Check(
                fight.Knockdown && fight.LoserId == them.Id && fight.Exchange <= 3,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"punches timed in the middle of the bar knock the other fighter down (in {fight.Exchange + 1} exchanges, {fight.Victim.Landed} landed)"
                )
            );

            Maybe<PenaltyState> call = w.Match.Penalties.FirstOrNone(p => p.PlayerId == them.Id);
            Check(
                call.Is(c =>
                    string.Equals(c.Infraction, FightTuning.Infraction, StringComparison.Ordinal)
                    && Math.Abs(c.SecondsLeft - PenaltyCalls.MinorSeconds(w)) < 0.01f
                )
                    && PenaltyCalls.OnTheWay(w, them.Id)
                    && PenaltyCalls.InBox(w, them.Id),
                "the loser skates to the box for two minutes for fighting"
            );
            Check(
                events.Exists(e => e is PenaltyCalled pen && pen.PlayerId == them.Id)
                    && me.OnIce
                    && !PenaltyCalls.InBox(w, me.Id)
                    && me.Action == PlayerAction.None,
                "a Penalty event is published for him; the winner stays on the ice"
            );
            Run(w, x => x.Match.Phase == MatchPhase.Play, 15);
            Vector3 dot = MatchFlow.FaceoffDot(w);
            Check(
                w.Match.Phase == MatchPhase.Play
                    && PenaltyCalls.PowerPlay(w) == SideOrNone.Home
                    && Math.Abs(dot.X) > 15
                    && Math.Sign(dot.X) == FaceoffFormation.DefendedEnd(w.Match, Side.Away),
                "play restarts with a faceoff in the loser's end and the winner's club on the power play"
            );
        }

        [Test]
        public void BadTimingLoses()
        {
            World w = Make();
            Player me = Skater(w, Side.Home, PlayingPosition.C);
            me.Slot = 0;
            FightState s = Fighting.State(w);
            s.AiBlocks = false;
            Fight fight = StartFight(w);
            _ = Run(w, x => !Current(x).HasValue, Human(0.05f), 20);
            Check(
                fight.LoserId == me.Id && fight.Victim.Missed >= 1 && fight.Victim.Landed == 0,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"punches thrown at the edge of the bar miss, and the human loses ({fight.Victim.Missed} missed, {fight.Checker.Landed} taken)"
                )
            );
            Check(
                PenaltyCalls.InBox(w, me.Id)
                    && w.Controlled(0).TryGet(out Player now)
                    && now != me
                    && now.OnIce
                    && now.Side == Side.Home,
                "the losing human's skater goes to the box and control moves to a teammate"
            );
        }

        [Test]
        public void BlockTiming()
        {
            World fresh = Make();
            World stale = Make();
            // block just as the AI winds up: every punch is stopped
            Player me = Skater(fresh, Side.Home, PlayingPosition.C);
            me.Slot = 0;
            Fighting.State(fresh).AiBlocks = false;
            Fight fight = StartFight(fresh);
            _ = Run(
                fresh,
                x => Current(x).Is(f => f.Exchange >= 3),
                Human(2f, (x, f) => !float.IsNaN(f.Checker.LandsAt)),
                10
            );
            Check(
                fight.Checker.Blocked >= 2
                    && fight.Checker.Landed == 0
                    && fight.Victim.Balance >= 1f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"a guard raised during the wind-up blocks the punch ({fight.Checker.Blocked} blocked)"
                )
            );

            // a guard held up the whole time has dropped by the time the punch lands
            Player me2 = Skater(stale, Side.Home, PlayingPosition.C);
            me2.Slot = 0;
            Fighting.State(stale).AiBlocks = false;
            Fight fight2 = StartFight(stale);
            _ = Run(
                stale,
                x =>
                    !Current(x).TryGet(out Fight f)
                    || f.Exchange >= 3
                    || f.Stage == FightStage.Over,
                Human(2f, (_, _) => true),
                10
            );
            Check(
                fight2.Checker.Blocked == 0
                    && fight2.Checker.Landed >= 1
                    && fight2.Victim.Balance < 1f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"a guard held up from the start is too old to block ({fight2.Checker.Landed} landed)"
                )
            );
        }

        [Test]
        public void BrokenUp()
        {
            World w = Make();
            FightState s = Fighting.State(w);
            s.AiPunches = false;
            Player a = Skater(w, Side.Away, PlayingPosition.C);
            Fight fight = StartFight(w);
            Run(w, x => !Current(x).HasValue, 20);
            Check(
                fight.Exchange == FightTuning.MaxExchanges
                    && !fight.Knockdown
                    && fight.LoserId == a.Id
                    && PenaltyCalls.InBox(w, a.Id),
                "with nobody down after the last exchange the linesmen break it up; on even balance the checker who started it goes to the box"
            );
        }

        [Test]
        public void NoFight()
        {
            World overtime = Make(4);
            World off = Make(1, s => s with { Fights = false });
            World w = Make();
            Fighting.State(overtime).ForceAccept = true;
            Check(
                !Fighting.Offer(
                    overtime,
                    Skater(overtime, Side.Away, PlayingPosition.C).Id,
                    Skater(overtime, Side.Home, PlayingPosition.C).Id
                ),
                "no fights in overtime"
            );
            Fighting.State(off).ForceAccept = true;
            Check(
                !Fighting.Offer(
                    off,
                    Skater(off, Side.Away, PlayingPosition.C).Id,
                    Skater(off, Side.Home, PlayingPosition.C).Id
                ),
                "no fights when fights are off in the settings"
            );

            Player goalie = w.OnIce(Side.Home).First(p => p.IsGoalie);
            Check(
                !Fighting.Offer(w, Skater(w, Side.Away, PlayingPosition.C).Id, goalie.Id),
                "goalies never fight"
            );
            // two home skaters already in the box: a fight could put home three short
            PenaltyBook book = PenaltyCalls.Book(w);
            PenaltyCalls.Assess(
                w,
                book,
                new DelayedPenalty(
                    Side.Home,
                    Skater(w, Side.Home, PlayingPosition.LW).Id,
                    Infraction.Tripping,
                    w.Time
                )
            );
            PenaltyCalls.Assess(
                w,
                book,
                new DelayedPenalty(
                    Side.Home,
                    Skater(w, Side.Home, PlayingPosition.RW).Id,
                    Infraction.Hooking,
                    w.Time
                )
            );
            Check(
                !Fighting.Offer(
                    w,
                    Skater(w, Side.Away, PlayingPosition.C).Id,
                    Skater(w, Side.Home, PlayingPosition.C).Id
                ),
                "no fight when a club already serves two penalties"
            );
        }

        /// <summary>A fight the home centre wins by knockdown; returns the events from the gloves dropping to the end.</summary>
        private static List<MatchEvent> HomeWins(World w)
        {
            Player me = Skater(w, Side.Home, PlayingPosition.C);
            me.Slot = 0;
            FightState s = Fighting.State(w);
            s.AiPunches = false;
            s.AiBlocks = false;
            _ = StartFight(w);
            return Run(w, x => !Current(x).HasValue, Human(0.5f), 20);
        }

        private static int Skaters(World w, Side side)
        {
            return w.OnIce(side).Count(p => !p.IsGoalie);
        }

        private static Maybe<PenaltyState> Fighting5(World w, Player p)
        {
            return w.Match.Penalties.FirstOrNone(x =>
                x.PlayerId == p.Id
                && string.Equals(x.Infraction, FightTuning.Infraction, StringComparison.Ordinal)
                && Math.Abs(x.SecondsLeft - PenaltyCalls.ClubSeconds(w, Severity.Major)) < 0.01f
            );
        }

        [Test]
        public void ArcadePenalty()
        {
            World w = Make(rules: RuleSet.Arcade);
            Player me = Skater(w, Side.Home, PlayingPosition.C);
            Player them = Skater(w, Side.Away, PlayingPosition.C);
            _ = HomeWins(w);
            // to the drop: the penalty clock starts with the game clock there
            Run(w, x => x.Match.Phase == MatchPhase.Play || MatchFlow.State(x).Draw.Dropped, 15);
            Maybe<PenaltyState> call = w.Match.Penalties.FirstOrNone(p => p.PlayerId == them.Id);
            Check(
                call.Is(c => Math.Abs(c.SecondsLeft - PenaltyCalls.MinorSeconds(w)) < 0.01f)
                    && w.Match.Penalties.Count == 1
                    && me.OnIce
                    && PenaltyCalls.PowerPlay(w) == SideOrNone.Home
                    && !PenaltyCalls.Ejected(w, them.Id),
                "Arcade: the loser gets two minutes, the winner stays on and his club has the power play"
            );
        }

        [Test]
        public void MajorsStay()
        {
            World w = Make(rules: RuleSet.Nhl);
            Player me = Skater(w, Side.Home, PlayingPosition.C);
            Player them = Skater(w, Side.Away, PlayingPosition.C);
            List<MatchEvent> events = HomeWins(w);
            Check(
                Fighting5(w, me).HasValue
                    && Fighting5(w, them).HasValue
                    && PenaltyCalls.InBox(w, me.Id)
                    && PenaltyCalls.InBox(w, them.Id)
                    && PenaltyCalls.OnTheWay(w, me.Id)
                    && PenaltyCalls.OnTheWay(w, them.Id)
                    && events.Where(e => e is PenaltyCalled).Take(3).Count() == 2
                    && !PenaltyCalls.Ejected(w, me.Id)
                    && !PenaltyCalls.Ejected(w, them.Id),
                "NHL: both fighters get a five minute major for fighting, winner and loser, skate to the box, and neither is ejected"
            );
            Run(w, x => x.Match.Phase == MatchPhase.Play, 15);
            Check(
                Skaters(w, Side.Home) == 5
                    && Skaters(w, Side.Away) == 5
                    && PenaltyCalls.PowerPlay(w) == SideOrNone.None,
                "NHL: the majors are coincidental and substituted, so play goes on five on five"
            );
            Squad homeSquad = Must(Bench.SquadOf(w, Side.Home));
            Squad awaySquad = Must(Bench.SquadOf(w, Side.Away));
            _ = Run(
                w,
                x => x.Match.Penalties.Count == 0,
                x =>
                {
                    if (x.Tick % 180 == 0)
                    {
                        Bench.RequestChange(x, Side.Home);
                        Bench.RequestChange(x, Side.Away);
                    }
                },
                400
            );
            Check(
                w.Match.Penalties.Count == 0
                    && !PenaltyCalls.InBox(w, me.Id)
                    && !PenaltyCalls.InBox(w, them.Id)
                    && homeSquad.UnitOf(me).HasValue
                    && awaySquad.UnitOf(them).HasValue,
                "NHL: when the five minutes are up both fighters leave the box and stay in their lines"
            );
        }

        [Test]
        public void MajorsAndEjection()
        {
            World w = Make(rules: RuleSet.Shl);
            Player me = Skater(w, Side.Home, PlayingPosition.C);
            Player them = Skater(w, Side.Away, PlayingPosition.C);
            Squad homeSquad = Must(Bench.SquadOf(w, Side.Home));
            Squad awaySquad = Must(Bench.SquadOf(w, Side.Away));
            List<MatchEvent> events = HomeWins(w);
            Check(
                Fighting5(w, me).HasValue
                    && Fighting5(w, them).HasValue
                    && events.Where(e => e is PenaltyCalled).Take(3).Count() == 2,
                "SHL: both fighters get a five minute major for fighting, winner and loser, on the scorebug"
            );
            Check(
                PenaltyCalls.Ejected(w, me.Id)
                    && PenaltyCalls.Ejected(w, them.Id)
                    && !homeSquad.UnitOf(me).HasValue
                    && !awaySquad.UnitOf(them).HasValue
                    && !me.OnIce
                    && !them.OnIce,
                "SHL: both also get a game misconduct and are off the squad, the lines refill without them"
            );
            Run(w, x => x.Match.Phase == MatchPhase.Play, 15);
            Check(
                Skaters(w, Side.Home) == 5
                    && Skaters(w, Side.Away) == 5
                    && PenaltyCalls.PowerPlay(w) == SideOrNone.None,
                "SHL: the majors are coincidental and substituted, so play goes on five on five"
            );
            bool back = false;
            _ = Run(
                w,
                x => x.Match.Penalties.Count == 0,
                x =>
                {
                    back |= me.OnIce || them.OnIce;
                    if (x.Tick % 180 == 0)
                    {
                        Bench.RequestChange(x, Side.Home);
                        Bench.RequestChange(x, Side.Away);
                    }
                },
                400
            );
            Check(
                w.Match.Penalties.Count == 0
                    && !back
                    && !me.OnIce
                    && !them.OnIce
                    && Skaters(w, Side.Home) == 5
                    && Skaters(w, Side.Away) == 5,
                "SHL: when the five minutes are up neither fighter comes back, both clubs play five on five"
            );
        }

        [Test]
        public void MajorsNeverShort()
        {
            World w = Make(rules: RuleSet.Shl);
            // two home skaters already serve: substituted majors cannot put home three short, so the fight may go on
            PenaltyBook book = PenaltyCalls.Book(w);
            PenaltyCalls.Assess(
                w,
                book,
                new DelayedPenalty(
                    Side.Home,
                    Skater(w, Side.Home, PlayingPosition.LW).Id,
                    Infraction.Tripping,
                    w.Time
                )
            );
            PenaltyCalls.Assess(
                w,
                book,
                new DelayedPenalty(
                    Side.Home,
                    Skater(w, Side.Home, PlayingPosition.RW).Id,
                    Infraction.Hooking,
                    w.Time
                )
            );
            Fighting.State(w).ForceAccept = true;
            Check(
                Fighting.Offer(
                    w,
                    Skater(w, Side.Away, PlayingPosition.C).Id,
                    Skater(w, Side.Home, PlayingPosition.C).Id
                ),
                "SHL: a club serving two penalties can still fight, since coincidental majors never leave it shorter"
            );
        }

        private static readonly RuleSet[] Presets = [RuleSet.Arcade, RuleSet.Shl, RuleSet.Nhl];

        /// <summary>Gets the full matches' results, shared by the class's tests.</summary>
        [ClassDataSource<Played<FightCount>>(Shared = SharedType.PerClass)]
        public required Played<FightCount> FullMatchesPlayed { get; init; }

        /// <summary>Gets the matches: every seed under each preset in turn.</summary>
        public static IEnumerable<int> MatchNumbers()
        {
            return Enumerable.Range(0, Presets.Length * MatchSeeds.Length);
        }

        /// <summary>One full AI match under a preset, with the referee at full strictness; <see cref="FullMatches"/> judges them.</summary>
        [Test]
        [ExecutionPriority(Priority.High)]
        [MethodDataSource(nameof(MatchNumbers))]
        public void PlayMatch(int i)
        {
            FullMatchesPlayed.Add(
                i,
                Match(
                    WorldBuilder.DefaultSetup.WithRules(Presets[i / MatchSeeds.Length]) with
                    {
                        PeriodSeconds = 600,
                    },
                    MatchSeeds[i % MatchSeeds.Length]
                )
            );
        }

        /// <summary>Full AI matches under each preset, with the referee at full strictness.</summary>
        [Test]
        [DependsOn(nameof(PlayMatch), [typeof(int)])]
        public void FullMatches()
        {
            FightCount[] played = FullMatchesPlayed.All(Presets.Length * MatchSeeds.Length);
            for (int r = 0; r < Presets.Length; r++)
            {
                MatchSetup setup = WorldBuilder.DefaultSetup.WithRules(Presets[r]);
                int perFight = setup.Rules.FightPenalty == FightPenalty.LoserMinor ? 1 : 2;
                int fights = 0,
                    boxed = 0,
                    broken = 0,
                    open = 0;
                foreach (
                    (int f, int b, int x, int o) in played
                        .Skip(r * MatchSeeds.Length)
                        .Take(MatchSeeds.Length)
                )
                {
                    fights += f;
                    boxed += b;
                    broken += x;
                    open += o;
                }
                Check(
                    fights >= 1 && boxed == fights * perFight && broken == 0 && open == 0,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{setup.Rules.Preset}: AI matches of 3 x 10:00: {fights} fight(s) in {MatchSeeds.Length} matches, {boxed} fighting penalties ({perFight} a fight), clubs short by exactly their penalties"
                    )
                );
            }
        }

        /// <summary>What one full match came to: its fights, the fighting penalties, steps a club was not at its strength, and penalties left open.</summary>
        public sealed record FightCount(int Fights, int Boxed, int Broken, int Open);

        private static readonly int[] MatchSeeds = [2, 3, 4, 5];

        private static FightCount Match(MatchSetup setup, int seed)
        {
            World w = new WorldBuilder()
                .Setup(
                    setup with
                    {
                        Inputs =
                        [
                            new InputSlot(InputDevice.None, Side.Home),
                            new InputSlot(InputDevice.None, Side.Away),
                        ],
                    }
                )
                .Build();
            PenaltyCalls.Book(w).Rng = Seeds.Dice(seed);
            Fighting.State(w).Rng = Seeds.Dice(seed);
            ISystem[] systems =
            [
                new PositioningSystem(Seeds.Dice(seed)),
                new DecisionSystem(Seeds.Dice(seed)),
                new LinesSystem(),
                new SkatingSystem(),
                new CheckingSystem(Seeds.Dice(seed)),
                new PuckHandlingSystem(Seeds.Dice(seed)),
                new GoalieSystem { Dice = Seeds.Dice(seed) },
                new PuckPhysics(),
                new MatchRules(),
                new OffsideRule(),
                new IcingRule(),
                new PenaltyRules(),
                new FightSystem(),
                new RefereeSystem(),
            ];
            foreach (
                ISystem s in systems.Where(s =>
                    s
                        is PositioningSystem
                            or DecisionSystem
                            or LinesSystem
                            or SkatingSystem
                            or CheckingSystem
                            or PuckHandlingSystem
                )
            )
            {
                s.Start(w);
            }

            int boxed = 0,
                broken = 0;
            return CollectMatchSummary(w, systems, ref boxed, ref broken);
        }

        private static FightCount CollectMatchSummary(
            World w,
            ISystem[] systems,
            ref int boxed,
            ref int broken
        )
        {
            for (
                int i = 0;
                i < 4000 * 60
                    && w.Match.Phase
                        is not (MatchPhase.Final or MatchPhase.Overtime or MatchPhase.Shootout);
                i++
            )
            {
                w.Events.Clear();
                foreach (ISystem s in systems)
                {
                    s.Update(w, Game.Step);
                }

                w.Time += Game.Step;
                w.Tick++;
                foreach (
                    PenaltyCalled e in w
                        .Events.Of<PenaltyCalled>()
                        .Where(e =>
                            string.Equals(
                                w.Match.Penalties.LastOrDefault(p =>
                                    p.PlayerId == e.PlayerId
                                )?.Infraction,
                                FightTuning.Infraction,
                                StringComparison.Ordinal
                            )
                        )
                )
                {
                    boxed++;
                }
                // strength is checked in play: between a whistle and the faceoff the bench is still filling the gaps (as the penalty tests do)
                if (w.Match.Phase != MatchPhase.Play)
                {
                    continue;
                }

                foreach (
                    Side side in new[] { Side.Home, Side.Away }.Where(side =>
                        w.OnIce(side)
                            .Where(p => !p.IsGoalie)
                            .Take(PenaltyCalls.TargetSkaters(w, side) + 1)
                            .Count() != PenaltyCalls.TargetSkaters(w, side)
                    )
                )
                {
                    broken++;
                }
            }
            return new FightCount(Fighting.Started(w), boxed, broken, Current(w).HasValue ? 1 : 0);
        }
    }
}
