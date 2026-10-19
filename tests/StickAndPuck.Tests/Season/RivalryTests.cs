using System.Globalization;
using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Fights;
using StickAndPuck.App.Lines;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Referee;
using StickAndPuck.App.Replay;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Rules.Penalties;
using StickAndPuck.App.Season.DressingRoom;
using StickAndPuck.App.Season.Morale;
using StickAndPuck.App.Season.Schedule;
using StickAndPuck.Tests.Support;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.Tests.Season
{
    /// <summary>
    /// Rivalries (W131): the rival pairs in data/season/{league}/rivals.json, the rule presets, a rivalry game's
    /// setup and replay, its scaled hit and fight chances (and more hits in a played period), the bigger
    /// morale swing of a result between rivals, the dressing room's derby news, and that a game between
    /// clubs that are not rivals, or with rivalries off, is unchanged.
    /// </summary>
    public sealed class RivalryTests : CheckedTest
    {
        private static IReadOnlyList<ClubData> Shl => WorldBuilder.Clubs;

        private static string S(FormattableString s)
        {
            return s.ToString(CultureInfo.InvariantCulture);
        }

        private static bool Near(float a, float b)
        {
            return MathF.Abs(a - b) < 1e-5f;
        }

        private static ClubData Club(string id)
        {
            return Shl.Concat(Clubs.LoadNhl())
                .First(c => string.Equals(c.Id, id, StringComparison.Ordinal));
        }

        private static MatchSetup Setup(string home, string away, RuleSet rules)
        {
            return new MatchSetup
            {
                Home = Club(home),
                Away = Club(away),
                Rivalry = Rivals.Are(home, away),
            }.WithRules(rules);
        }

        [Test]
        public void PairsAreRealClubs()
        {
            Dictionary<string, League> leagues = Shl.Concat(Clubs.LoadNhl())
                .ToDictionary(c => c.Id, c => c.League, StringComparer.Ordinal);
            Check(
                Rivals.Pairs.Count >= 10,
                S($"the data file has rival pairs ({Rivals.Pairs.Count})")
            );
            foreach ((string a, string b) in Rivals.Pairs)
            {
                Check(
                    leagues.TryGetValue(a, out League la)
                        && leagues.TryGetValue(b, out League lb)
                        && la == lb
                        && !string.Equals(a, b, StringComparison.Ordinal),
                    S($"{a} and {b} are two clubs of the same league")
                );
            }

            Check(
                Rivals.Are("frolunda", "farjestad")
                    && Rivals.Are("farjestad", "frolunda")
                    && Rivals.Are("lulea", "skelleftea")
                    && Rivals.Are("bruins", "canadiens")
                    && Rivals.Are("oilers", "flames"),
                "real rivalries are marked, in either order"
            );
            Check(
                !Rivals.Are("frolunda", "timra")
                    && !Rivals.Are("frolunda", "frolunda")
                    && !Rivals.Are("oilers", "panthers"),
                "other clubs are not rivals"
            );
        }

        [Test]
        public void Presets()
        {
            Check(
                RuleSet.Shl.Rivalries
                    && RuleSet.Shl.RivalryStrength == RivalryStrength.Normal
                    && RuleSet.Nhl.Rivalries
                    && RuleSet.Nhl.RivalryStrength == RivalryStrength.Strong
                    && !RuleSet.Arcade.Rivalries,
                "SHL plays normal rivalries, NHL strong ones, Arcade none"
            );
            Check(
                Near(RuleSet.Arcade.RivalryScale, 0f)
                    && Near(RuleSet.Shl.RivalryScale, 1f)
                    && Near(RuleSet.Nhl.RivalryScale, 1.5f)
                    && Near(
                        (RuleSet.Shl with { RivalryStrength = RivalryStrength.Mild }).RivalryScale,
                        0.5f
                    ),
                "the strength scales what a rivalry adds, off is nothing"
            );
            Check(
                RuleRows.All.Any(r =>
                    string.Equals(r.Key, nameof(RuleSet.Rivalries), StringComparison.Ordinal)
                    && string.Equals(r.Group, K.RuleGroupSeason, StringComparison.Ordinal)
                )
                    && RuleRows.All.Any(r =>
                        string.Equals(
                            r.Key,
                            nameof(RuleSet.RivalryStrength),
                            StringComparison.Ordinal
                        )
                    ),
                "both settings are rows in the rules page's season group"
            );
        }

        [Test]
        public void HitAndFightChancesScale()
        {
            MatchSetup derby = Setup("lulea", "skelleftea", RuleSet.Shl);
            MatchSetup plain = Setup("lulea", "timra", RuleSet.Shl);
            MatchSetup nhl = Setup("oilers", "flames", RuleSet.Nhl);
            MatchSetup off = Setup("lulea", "skelleftea", RuleSet.Shl with { Rivalries = false });
            Check(
                derby.Rivalry && !plain.Rivalry,
                "the setup marks the derby and not the other game"
            );
            Check(
                Near(Rivals.HitFactor(derby), 1f + Rivals.HitBoost)
                    && Near(Rivals.FightFactor(derby), 1f + Rivals.FightBoost),
                S(
                    $"an SHL derby checks {Rivals.HitFactor(derby):0.00}x and drops the gloves {Rivals.FightFactor(derby):0.00}x as often"
                )
            );
            Check(
                Rivals.HitFactor(nhl) > Rivals.HitFactor(derby)
                    && Rivals.FightFactor(nhl) > Rivals.FightFactor(derby),
                "NHL rivalries are stronger"
            );
            Check(
                Near(Rivals.HitFactor(plain), 1f)
                    && Near(Rivals.FightFactor(plain), 1f)
                    && Near(Rivals.HitFactor(off), 1f)
                    && Near(Rivals.FightFactor(off), 1f),
                "a game between non-rivals, or with rivalries off, is unchanged"
            );
        }

        [Test]
        public void SetupAndReplayKeepTheRivalry()
        {
            SeasonState season = SeasonState.New(Shl, "lulea", 3);
            Fixture derby = season.Games.First(g =>
                g.Involves("lulea") && g.Involves("skelleftea")
            );
            Fixture other = season.Games.First(g => g.Involves("lulea") && g.Involves("timra"));
            MatchSetup setup = season.SetupFor(derby, Shl);
            Check(
                setup.Rivalry && !season.SetupFor(other, Shl).Rivalry,
                "a season derby is set up as one, another game is not"
            );
            Check(
                ReplaySetup.Of(setup).ToSetup(Shl).Rivalry
                    && !ReplaySetup.Of(season.SetupFor(other, Shl)).ToSetup(Shl).Rivalry,
                "the replay keeps whether the game was a rivalry game"
            );
        }

        // a hard check from the away centre on the home centre, as checking publishes one, under the fight rules
        private static bool HardCheckFights(MatchSetup setup, int dice)
        {
            World w = new WorldBuilder()
                .Setup(setup with { PeriodSeconds = 600 })
                .Switches(s => s.PenaltyStrictness = 0)
                .Build();
            ISystem[] systems =
            [
                new LinesSystem(),
                new PuckPhysics(),
                new MatchRules(),
                new PenaltyRules(),
                new FightSystem(),
                new RefereeSystem(),
            ];
            void Step(Action<World> each)
            {
                w.Events.Clear();
                each(w);
                foreach (ISystem s in systems)
                {
                    s.Update(w, Game.Step);
                }

                w.Time += Game.Step;
                w.Tick++;
            }

            for (int i = 0; i < 15 / Game.Step && w.Match.Phase != MatchPhase.Play; i++)
            {
                Step(_ => { });
            }

            FightState state = Fighting.State(w);
            state.OfferChance = 0.48f;
            state.ForceAccept = true;
            state.Rng = Seeds.Dice(dice);
            Player checker = w.OnIce(Side.Away).First(p => p.Data.Position == PlayingPosition.C);
            Player target = w.OnIce(Side.Home).First(p => p.Data.Position == PlayingPosition.C);
            checker.Position = target.Position + new Vector3(0.8f, 0, 0);
            Step(x =>
                x.Events.Publish(
                    new CheckLanded(x.Time, checker.Side, checker.Id, target.Position, 1.8f)
                )
            );
            return Fighting.Current(w).HasValue;
        }

        [Test]
        public void HardChecksFightMoreInARivalry()
        {
            MatchSetup rivals = Setup("oilers", "flames", RuleSet.Nhl);
            MatchSetup plain = Setup("oilers", "panthers", RuleSet.Nhl);
            const int tries = 16;
            int rivalFights = Enumerable.Range(0, tries).Count(d => HardCheckFights(rivals, d));
            int plainFights = Enumerable.Range(0, tries).Count(d => HardCheckFights(plain, d));
            Check(
                rivalFights == tries,
                S(
                    $"at a 48% base chance an NHL rivalry's hard check always drops the gloves ({rivalFights}/{tries})"
                )
            );
            Check(
                plainFights is > 0 and < tries,
                S(
                    $"the same checks between non-rivals fight at the base chance ({plainFights}/{tries})"
                )
            );
        }

        // checks landed by both clubs in one all-AI period
        private static int PeriodHits(MatchSetup setup, int seed)
        {
            MatchSetup ai = setup with
            {
                PeriodSeconds = 600,
                Inputs =
                [
                    new InputSlot(InputDevice.None, Side.Home),
                    new InputSlot(InputDevice.None, Side.Away),
                ],
            };
            World world = new WorldBuilder().Setup(ai).Seed(seed).Build();
            ISystem[] systems = AllAiMatches.Systems(world, seed);
            int hits = 0;
            for (int i = 0; i < 600 / Game.Step && world.Match.Period == 1; i++)
            {
                world.Events.Clear();
                foreach (ISystem s in systems)
                {
                    s.Update(world, Game.Step);
                }

                hits += world.Events.Of<CheckLanded>().Count();
                world.Time += Game.Step;
                world.Tick++;
            }

            return hits;
        }

        [Test]
        public void ARivalryPeriodHasMoreHits()
        {
            MatchSetup derby = Setup("lulea", "skelleftea", RuleSet.Shl);
            int rival = PeriodHits(derby, 4) + PeriodHits(derby, 5);
            int plain =
                PeriodHits(derby with { Rivalry = false }, 4)
                + PeriodHits(derby with { Rivalry = false }, 5);
            Check(
                rival > plain,
                S(
                    $"the same clubs and seeds hit more as rivals ({plain} -> {rival} checks in two periods)"
                )
            );
        }

        // the user's club's morale after one regulation result against an opponent
        private static int AfterOne(RuleSet rules, string opponent, bool win)
        {
            SeasonState season = SeasonState.New(Shl, "lulea", 3);
            season.Rules = rules;
            Fixture g = season.Games.First(f => f.Involves("lulea") && f.Involves(opponent));
            bool home = string.Equals(g.Home, "lulea", StringComparison.Ordinal);
            int mine = win ? 3 : 1,
                theirs = win ? 1 : 3;
            season.Record(
                g.Number,
                home
                    ? new FixtureResult(mine, theirs, ResultKind.Regulation)
                    : new FixtureResult(theirs, mine, ResultKind.Regulation)
            );
            return TeamMorale.Of(season, "lulea") - TeamMorale.Neutral;
        }

        [Test]
        public void ARivalryResultSwingsMoraleFurther()
        {
            int derbyWin = AfterOne(RuleSet.Shl, "skelleftea", win: true),
                plainWin = AfterOne(RuleSet.Shl, "timra", win: true),
                derbyLoss = AfterOne(RuleSet.Shl, "skelleftea", win: false),
                plainLoss = AfterOne(RuleSet.Shl, "timra", win: false);
            Check(
                derbyWin > plainWin && plainWin > 0,
                S($"a derby win lifts morale more ({plainWin} -> {derbyWin})")
            );
            Check(
                derbyLoss < plainLoss && plainLoss < 0,
                S($"a derby loss drops it more ({plainLoss} -> {derbyLoss})")
            );
            RuleSet off = RuleSet.Shl with { Rivalries = false };
            Check(
                AfterOne(off, "skelleftea", win: true) == plainWin,
                "with rivalries off a derby swings like any game"
            );
            Check(
                Near(Rivals.MoraleFactor(RuleSet.Shl, "lulea", "timra"), 1f)
                    && Near(
                        Rivals.MoraleFactor(RuleSet.Shl, "lulea", "skelleftea"),
                        1f + Rivals.MoraleBoost
                    ),
                "only rivals get the morale factor"
            );
        }

        [Test]
        public void DressingRoomNamesTheComingDerby()
        {
            SeasonState season = SeasonState.New(Shl, "lulea", 3);
            // play every game before the first derby, so the derby is next
            Fixture derby = season.Games.First(g =>
                g.Involves("lulea") && g.Involves("skelleftea")
            );
            foreach (
                Fixture g in season
                    .Games.Where(g => g.Involves("lulea") && g.Number < derby.Number)
                    .ToList()
            )
            {
                season.Record(g.Number, new FixtureResult(2, 1, ResultKind.Regulation));
            }

            Check(season.NextGame.Is(g => g.Number == derby.Number), "the derby is next");
            string line = Text.Format(K.NewsNextDerby, Club("skelleftea").Name.ToUpperInvariant());
            DressingRoomReport report = DressingRoomSources.Build(
                DressingRoomContext.Of(season, Shl, RuleSet.Shl)
            );
            Check(
                report.News.Any(n =>
                    string.Equals(n.Text, line, StringComparison.Ordinal) && n.Highlight
                ),
                S($"the news says so: {line}")
            );
            DressingRoomReport off = DressingRoomSources.Build(
                DressingRoomContext.Of(season, Shl, RuleSet.Shl with { Rivalries = false })
            );
            Check(
                !off.News.Any(n => string.Equals(n.Text, line, StringComparison.Ordinal)),
                "with rivalries off it does not"
            );
        }
    }
}
