using StickAndPuck.App.Core;
using StickAndPuck.App.Momentum;

namespace StickAndPuck.App.Audio
{
    /// <summary>W121: the fans' chants. The home end sings its clap chant and its "let's go" by turns
    /// through play, more often and louder the bigger the home club's momentum, and swells into the goal
    /// song after a home goal or a home win. The visitors' corner sings its thinner chant after an away
    /// goal or win, and now and then while the away club has momentum, always quieter than the home end.
    /// Presentation only: <see cref="AudioSystem"/> plays what <see cref="Update"/> returns; it rolls no
    /// dice and writes nothing the simulation reads, so replays stay the same.</summary>
    internal sealed class CrowdChants
    {
        /// <summary>Seconds after a goal before its chant, once the horn and fanfare have had their say.</summary>
        public const float AfterGoal = 1.8f;

        /// <summary>Seconds between home chants with no momentum.</summary>
        public const float CalmGap = 14f;

        /// <summary>Seconds between home chants at a full boost.</summary>
        public const float HypeGap = 6f;

        /// <summary>Seconds between the away corner's chants while its club has momentum.</summary>
        public const float AwayGap = 10f;

        /// <summary>The home chant's level with no momentum; a full boost plays it at 1.</summary>
        public const float CalmVolume = 0.35f;

        /// <summary>The away chant's level at most: a corner of the arena against the rest.</summary>
        public const float AwayVolume = 0.6f;

        /// <summary>Seconds the goal song lasts (art/audio/effects/goal_song.wav), so no chant cuts in.</summary>
        public const float SongSeconds = 4.4f;

        private float home = CalmGap / 2f;
        private float away = AwayGap;
        private bool letsGo;

        /// <summary>Gets whether the fans sing in this phase: through play and its stoppages, not before
        /// the first faceoff, between periods, in a shootout or after the final horn.</summary>
        /// <param name="phase">The match phase.</param>
        /// <returns>True while the chants run.</returns>
        public static bool Sings(MatchPhase phase)
        {
            return phase
                is MatchPhase.Faceoff
                    or MatchPhase.Play
                    or MatchPhase.Stopped
                    or MatchPhase.Goal
                    or MatchPhase.Overtime;
        }

        /// <summary>Gets the home chant's level for the home club's boost.</summary>
        /// <param name="level">The boost, 0 to 1.</param>
        /// <returns>The level, <see cref="CalmVolume"/> to 1.</returns>
        public static float HomeVolume(float level)
        {
            return CalmVolume + ((1f - CalmVolume) * Math.Clamp(level, 0f, 1f));
        }

        /// <summary>Gets the seconds to the next home chant for the home club's boost.</summary>
        /// <param name="level">The boost, 0 to 1.</param>
        /// <returns><see cref="CalmGap"/> down to <see cref="HypeGap"/>.</returns>
        public static float HomeGap(float level)
        {
            return CalmGap + ((HypeGap - CalmGap) * Math.Clamp(level, 0f, 1f));
        }

        /// <summary>Reads this step's events and the clubs' momentum and gives the chants to start now.</summary>
        /// <param name="world">The match: its events, phase, pause and momentum.</param>
        /// <param name="dt">Seconds since the last step.</param>
        /// <returns>The chants, usually none.</returns>
        public List<Chant> Update(World world, float dt)
        {
            List<Chant> chants = [];
            if (world.Match.Paused)
            {
                return chants;
            }

            foreach (MatchEvent e in world.Events.Current)
            {
                Maybe<Side> cheer = e switch
                {
                    GoalScored g => g.Side,
                    MatchWon w => w.Winner,
                    _ => Maybe.None<Side>(),
                };
                if (cheer.TryGet(out Side side))
                {
                    chants.Add(Celebrate(side));
                }
            }

            if (!Sings(world.Match.Phase))
            {
                return chants;
            }

            home -= dt;
            if (home <= 0f)
            {
                float level = MomentumMeter.Of(world, Side.Home);
                chants.Add(
                    new Chant(
                        new Cue(letsGo ? Sfx.ChantLetsGo : Sfx.ChantClap, HomeVolume(level)),
                        Side.Home
                    )
                );
                letsGo = !letsGo;
                home = HomeGap(level);
            }

            away -= dt;
            float awayLevel = MomentumMeter.Of(world, Side.Away);
            if (away <= 0f && awayLevel >= MomentumTuning.SwellBelow)
            {
                chants.Add(new Chant(new Cue(Sfx.ChantAway, AwayVolume * awayLevel), Side.Away));
                away = AwayGap;
            }

            return chants;
        }

        private Chant Celebrate(Side side)
        {
            if (side == Side.Home)
            {
                home = SongSeconds + HypeGap; // the song first, the chants after it
                return new Chant(new Cue(Sfx.GoalSong, 1f, Delay: AfterGoal), Side.Home);
            }

            home = Math.Max(home, SongSeconds); // the home end falls quiet while the visitors sing
            away = AwayGap;
            return new Chant(new Cue(Sfx.ChantAway, AwayVolume, Delay: AfterGoal), Side.Away);
        }
    }
}
