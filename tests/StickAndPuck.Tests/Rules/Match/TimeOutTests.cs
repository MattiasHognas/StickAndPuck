using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Lines;
using StickAndPuck.App.Rules.Icing;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Rules.Match
{
    /// <summary>
    /// The time-out: one per club per game at a stoppage, the AI's call late in a close game with a tired
    /// line on the ice, a human's button, and the rest it gives the skaters on the ice.
    /// </summary>
    public sealed class TimeOutTests : CheckedTest
    {
        private const float Period = 300;

        /// <summary>A match with 5 minute periods, lines set up and play live in the first period.</summary>
        private static World Make(Func<WorldBuilder, WorldBuilder> change)
        {
            WorldBuilder builder = new WorldBuilder()
                .Setup(s => s with { PeriodSeconds = Period })
                .Then(w => new LinesSystem().Start(w))
                .Then(Live);
            return change(builder).Build();
        }

        private static World Make()
        {
            return Make(b => b);
        }

        private static void Live(World world)
        {
            world.Match.Phase = MatchPhase.Play;
            world.Puck.Live = true;
        }

        /// <summary>Late in the third period, home a goal down, play live.</summary>
        private static void LateAndDown(World world)
        {
            world.Match.Period = world.Setup.Periods;
            world.Match.Clock = 30;
            world.Match.Score[1] = 1;
        }

        private static void Tire(World world, Side side, float energy)
        {
            foreach (Player p in world.OnIce(side).Where(p => !p.IsGoalie))
            {
                p.Energy = energy;
            }
        }

        /// <summary>Runs the lines and match rules; returns the events.</summary>
        private static List<MatchEvent> Run(World world, float seconds, Func<World, bool> until)
        {
            ISystem[] systems = [new LinesSystem(), new MatchRules()];
            List<MatchEvent> events = [];
            for (int i = 0; i < seconds / Game.Step && !until(world); i++)
            {
                world.Events.Clear();
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

        private static List<MatchEvent> Run(World world, float seconds)
        {
            return Run(world, seconds, _ => false);
        }

        private static string F(float v)
        {
            return v.ToString("0.00", CultureInfo.InvariantCulture);
        }

        [Test]
        public void OnePerClub()
        {
            World w = Make();
            Check(!TimeOuts.Call(w, Side.Home), "no time-out while the puck is in play");
            _ = MatchFlow.Whistle(w, RinkSpec.CentreDot);
            Check(TimeOuts.Call(w, Side.Home), "home calls its time-out at the whistle");
            Check(
                !TimeOuts.Call(w, Side.Away),
                "the away club cannot call one while the home time-out runs"
            );
            List<MatchEvent> events = Run(
                w,
                MatchTuning.TimeOutSeconds + 5,
                x => x.Match.Phase == MatchPhase.Faceoff
            );
            Check(
                !events.OfType<TimeOutCalled>().Any()
                    && w.Match.Phase == MatchPhase.Faceoff
                    && w.Time >= MatchTuning.TimeOutSeconds - 0.1f,
                $"the faceoff follows after the time-out, at {F(w.Time)} s"
            );
            Check(
                !TimeOuts.Running(w).HasValue && TimeOuts.Used(w, Side.Home),
                "the time-out is over and spent"
            );
            Live(w);
            _ = MatchFlow.Whistle(w, RinkSpec.CentreDot);
            Check(!TimeOuts.Call(w, Side.Home), "home has no second time-out");
            Check(TimeOuts.Call(w, Side.Away), "away still has its own");
            Check(!TimeOuts.Call(w, Side.Away), "and only one");
        }

        [Test]
        public void OffUnderArcade()
        {
            World w = Make(b => b.Rules(RuleSet.Arcade));
            _ = MatchFlow.Whistle(w, RinkSpec.CentreDot);
            Check(
                !RuleSet.Arcade.TimeOut && RuleSet.Shl.TimeOut && RuleSet.Nhl.TimeOut,
                "SHL and NHL play time-outs, Arcade does not"
            );
            Check(!TimeOuts.Call(w, Side.Home), "no time-out under arcade rules");
        }

        [Test]
        public void CalledBeforeTheDrop()
        {
            World w = Make();
            _ = MatchFlow.Whistle(w, RinkSpec.CentreDot);
            _ = Run(w, 5, x => x.Match.Phase == MatchPhase.Faceoff);
            Check(TimeOuts.Call(w, Side.Away), "a time-out at the faceoff before the drop");
            Check(
                w.Match.Phase == MatchPhase.Stopped && TimeOuts.Running(w).Is(s => s == Side.Away),
                "play goes back to the stoppage for it"
            );
        }

        [Test]
        public void AiCallsLateInACloseGame()
        {
            World w = Make(b => b.Then(LateAndDown));
            Tire(w, Side.Home, 0.35f);
            _ = MatchFlow.Whistle(w, RinkSpec.CentreDot);
            List<MatchEvent> events = Run(w, 0.1f);
            Check(
                events.OfType<TimeOutCalled>().Where(e => e.Side == Side.Home).Take(2).Count() == 1,
                "the tired home club a goal down calls its time-out late in the third"
            );
            Check(
                !events.OfType<TimeOutCalled>().Any(e => e.Side == Side.Away),
                "the leading away club does not"
            );
        }

        [Test]
        public void AiCallsAfterAnIcingByItsTiredLine()
        {
            World w = Make(b => b.Then(LateAndDown).Then(x => x.Match.Score[0] = 1));
            Tire(w, Side.Home, 0.7f);
            Check(!TimeOuts.AiWants(w, Side.Home), "a line at 70 % does not want one by itself");
            IcingRule.HoldLines(w, Side.Home);
            _ = MatchFlow.Whistle(w, RinkSpec.CentreDot);
            List<MatchEvent> events = Run(w, 0.1f);
            Check(
                events.OfType<TimeOutCalled>().Any(e => e.Side == Side.Home),
                "tied, with the line held on the ice by its icing, the home club calls it"
            );
        }

        [Test]
        public void AiWaitsOtherwise()
        {
            World early = Make(b =>
                b.Then(LateAndDown).Then(x => x.Match.Period = x.Setup.Periods - 1)
            );
            Tire(early, Side.Home, 0.2f);
            Check(!TimeOuts.AiWants(early, Side.Home), "not in the second period");

            World soon = Make(b => b.Then(LateAndDown).Then(x => x.Match.Clock = 120));
            Tire(soon, Side.Home, 0.2f);
            Check(!TimeOuts.AiWants(soon, Side.Home), "not with two minutes of five left");

            World far = Make(b => b.Then(LateAndDown).Then(x => x.Match.Score[1] = 2));
            Tire(far, Side.Home, 0.2f);
            Check(!TimeOuts.AiWants(far, Side.Home), "not two goals down");

            World fresh = Make(b => b.Then(LateAndDown));
            Tire(fresh, Side.Home, 0.9f);
            Check(!TimeOuts.AiWants(fresh, Side.Home), "not with a fresh line");

            World human = Make(b => b.Human().Then(LateAndDown));
            Tire(human, Side.Home, 0.2f);
            _ = MatchFlow.Whistle(human, RinkSpec.CentreDot);
            List<MatchEvent> events = Run(human, 0.1f);
            Check(
                !events.OfType<TimeOutCalled>().Any(e => e.Side == Side.Home),
                "a human's club is never called for him"
            );
        }

        [Test]
        public void HumanButton()
        {
            World w = Make(b => b.Human());
            Player me = w.Players.First(p => p.Slot == 0);
            _ = MatchFlow.Whistle(w, RinkSpec.CentreDot);
            me.Command = me.Command with { TimeOut = true };
            List<MatchEvent> events = Run(w, Game.Step);
            Check(
                events.OfType<TimeOutCalled>().Any(e => e.Side == Side.Home),
                "the time-out button calls the human's time-out at a stoppage"
            );
        }

        [Test]
        public void RestsTheSkatersOnTheIce()
        {
            World w = Make();
            Tire(w, Side.Home, 0.2f);
            Tire(w, Side.Away, 0.2f);
            List<Player> on =
            [
                .. w.OnIce(Side.Home).Concat(w.OnIce(Side.Away)).Where(p => !p.IsGoalie),
            ];

            // an ordinary stoppage first: the rest on the ice is slow
            _ = MatchFlow.Whistle(w, RinkSpec.CentreDot);
            _ = Run(w, MatchTuning.StoppageSeconds - 0.1f);
            float plain = on.Average(p => p.Energy);
            Check(plain < 0.25f, $"a plain stoppage barely rests them ({F(plain)})");

            _ = TimeOuts.Call(w, Side.Home);
            _ = Run(w, 10);
            float tenSeconds = on.Average(p => p.Energy);
            Check(
                tenSeconds >= plain + (LinesTuning.TimeOutRest * 10 * 0.95f),
                $"ten seconds into the time-out both clubs' skaters on the ice have rested ({F(plain)} -> {F(tenSeconds)})"
            );
            _ = Run(w, MatchTuning.TimeOutSeconds, x => x.Match.Phase != MatchPhase.Stopped);
            float after = on.Average(p => p.Energy);
            Check(after >= 0.9f, $"after the time-out they are nearly fresh ({F(after)})");
        }
    }
}
