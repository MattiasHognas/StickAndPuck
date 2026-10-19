using StickAndPuck.App.Audio;
using StickAndPuck.App.Core;
using StickAndPuck.App.Momentum;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Audio
{
    /// <summary>
    /// The fans' chants (W121): the home end sings the goal song after a home goal or win and the away
    /// corner its quieter chant after the visitors'; through play the home end chants by turns, louder and
    /// more often on a home boost, and the away corner joins in on an away boost; nothing while paused or
    /// between periods. Every chant file is in the mix.
    /// </summary>
    public sealed class CrowdChantsTests : CheckedTest
    {
        private static List<Chant> Step(World world, CrowdChants chants, Maybe<MatchEvent> e)
        {
            world.Events.Clear();
            if (e.TryGet(out MatchEvent ev))
            {
                world.Events.Publish(ev with { Time = world.Time });
            }

            List<Chant> sung = chants.Update(world, Game.Step);
            world.Time += Game.Step;
            return sung;
        }

        private static List<(float At, Chant Chant)> Run(
            World world,
            CrowdChants chants,
            float seconds
        )
        {
            List<(float, Chant)> sung = [];
            for (int i = 0; i < seconds / Game.Step; i++)
            {
                float at = world.Time;
                sung.AddRange(Step(world, chants, Maybe.None<MatchEvent>()).Select(c => (at, c)));
            }

            return sung;
        }

        private static World Match()
        {
            World world = new WorldBuilder().Build();
            world.Match.Phase = MatchPhase.Play;
            return world;
        }

        [Test]
        public void EveryChantIsInTheMix()
        {
            foreach (
                Sfx sfx in new[] { Sfx.ChantClap, Sfx.ChantLetsGo, Sfx.GoalSong, Sfx.ChantAway }
            )
            {
                string file = SfxTable.Info(sfx).File;
                Check(
                    ResourceLoader.Exists($"{SfxTable.ResourcePath}.{file}"),
                    $"{sfx} plays art/audio/effects/{file}"
                );
            }

            Check(
                SfxTable.Info(Sfx.ChantAway).Volume < SfxTable.Info(Sfx.ChantClap).Volume,
                "the away chant sits under the home chants in the mix"
            );
        }

        [Test]
        public void AHomeGoalStartsTheGoalSong()
        {
            World world = Match();
            CrowdChants chants = new();
            List<Chant> sung = Step(
                world,
                chants,
                new GoalScored(0, Side.Home, 0, RinkSpec.NetMouth(1), 20)
            );
            Check(
                sung.Count == 1 && sung[0].Cue.Sound == Sfx.GoalSong && sung[0].Fans == Side.Home,
                "the home end sings the goal song"
            );
            Check(
                sung.Count == 1 && sung[0].Cue.Delay >= CrowdChants.AfterGoal,
                "after the horn and the fanfare"
            );
            Check(
                Run(world, chants, CrowdChants.SongSeconds).Count == 0,
                "and no chant cuts into it"
            );

            sung = Step(world, chants, new MatchWon(0, Side.Home));
            Check(
                sung.Exists(c => c.Cue.Sound == Sfx.GoalSong),
                "a home win gets the goal song too"
            );
        }

        [Test]
        public void AnAwayGoalIsSungFromTheAwayCorner()
        {
            World world = Match();
            CrowdChants chants = new();
            List<Chant> sung = Step(
                world,
                chants,
                new GoalScored(0, Side.Away, 0, RinkSpec.NetMouth(-1), 20)
            );
            Check(
                sung.Count == 1 && sung[0].Cue.Sound == Sfx.ChantAway && sung[0].Fans == Side.Away,
                "the visitors' corner sings after their goal"
            );
            Check(
                sung.Count == 1 && sung[0].Cue.Volume < 1f && sung[0].Cue.Delay > 0f,
                "quieter than the home end, after the horn"
            );
            Check(
                Run(world, chants, CrowdChants.SongSeconds - 0.1f)
                    .TrueForAll(c => c.Chant.Fans == Side.Away),
                "the home end falls quiet while the visitors sing"
            );
            sung = Step(world, chants, new MatchWon(0, Side.Away));
            Check(
                sung.Exists(c => c.Cue.Sound == Sfx.ChantAway),
                "an away win is sung from the corner"
            );
        }

        [Test]
        public void TheHomeEndChantsLouderAndMoreOftenOnABoost()
        {
            World world = Match();
            CrowdChants chants = new();
            List<(float At, Chant Chant)> calm = Run(world, chants, 30f);
            Check(calm.Count >= 2, $"the home end chants through play ({calm.Count} in 30 s)");
            Check(
                calm.TrueForAll(c =>
                    c.Chant.Fans == Side.Home
                    && Math.Abs(c.Chant.Cue.Volume - CrowdChants.CalmVolume) < 1e-4f
                ),
                "quietly with no momentum"
            );
            Check(
                calm.Count >= 2
                    && calm[0].Chant.Cue.Sound == Sfx.ChantClap
                    && calm[1].Chant.Cue.Sound == Sfx.ChantLetsGo,
                "the clap chant and the let's go by turns"
            );

            MomentumMeter.State(world).Level[(int)Side.Home] = 1f;
            List<(float At, Chant Chant)> hype = Run(world, chants, 30f);
            Check(hype.Count > calm.Count, $"more often on a full boost ({hype.Count} in 30 s)");
            Check(hype.TrueForAll(c => c.Chant.Cue.Volume > 0.99f), "and at full voice");
            Check(
                CrowdChants.HomeVolume(0.5f) > CrowdChants.HomeVolume(0.2f)
                    && CrowdChants.HomeGap(0.5f) < CrowdChants.HomeGap(0.2f),
                "the bigger the boost, the louder and sooner"
            );
        }

        [Test]
        public void TheAwayCornerChantsOnTheVisitorsBoost()
        {
            World world = Match();
            CrowdChants chants = new();
            Check(
                Run(world, chants, 30f).TrueForAll(c => c.Chant.Fans == Side.Home),
                "no away chant without an away boost"
            );
            MomentumMeter.State(world).Level[(int)Side.Away] = 0.8f;
            List<(float At, Chant Chant)> sung = Run(world, chants, 30f);
            List<Chant> away = [.. sung.Where(c => c.Chant.Fans == Side.Away).Select(c => c.Chant)];
            Check(away.Count >= 2, $"the away corner joins in on its club's boost ({away.Count})");
            Check(
                away.TrueForAll(c =>
                    c.Cue.Sound == Sfx.ChantAway && c.Cue.Volume <= CrowdChants.AwayVolume
                ),
                "never above the away level"
            );
        }

        [Test]
        public void NoChantsWhilePausedOrBetweenPeriods()
        {
            World world = Match();
            CrowdChants chants = new();
            world.Match.Paused = true;
            Check(
                Step(world, chants, new GoalScored(0, Side.Home, 0, RinkSpec.NetMouth(1), 20)).Count
                    == 0
                    && Run(world, chants, 30f).Count == 0,
                "nothing while the game is paused"
            );
            world.Match.Paused = false;
            world.Match.Phase = MatchPhase.Intermission;
            Check(Run(world, chants, 30f).Count == 0, "nothing between periods");
            world.Match.Phase = MatchPhase.Play;
            Check(Run(world, chants, 30f).Count > 0, "and back when play resumes");
        }
    }
}
