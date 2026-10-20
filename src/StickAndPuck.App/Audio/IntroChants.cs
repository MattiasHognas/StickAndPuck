using System.Collections.Immutable;
using StickAndPuck.App.Core;
using StickAndPuck.App.Screens;

namespace StickAndPuck.App.Audio
{
    /// <summary>W144: the crowd heard through the title intro (<see cref="TitleIntro"/>). Silent while the
    /// letters drop; the roar comes up as the home tifo goes up the far stand, the home end's clap chant
    /// starts as the camera comes down from the rafters (<see cref="Arena.IntroCrowd.Rise"/>) and a second
    /// roar meets it at the broadcast view, all under one swell that peaks while the tifo is up and settles
    /// to quiet as the start menu shows (<see cref="Arena.IntroCrowd.Settle"/>). A skip cuts it in a few
    /// hundredths of a second, so it ends without a click. The menu music ducks under the swell.
    /// Presentation only: <see cref="AudioSystem"/> starts what <see cref="Update"/> returns and holds the
    /// started chants at <see cref="Level"/>; nothing in the simulation reads it.</summary>
    internal sealed class IntroChants
    {
        /// <summary>When the swell starts to rise: with the tifo, a beat before the camera moves.</summary>
        public const float RiseFrom = TitleIntro.TifoAt;

        /// <summary>When the swell is all the way up: as the logo starts for its place over the menu.</summary>
        public const float PeakFrom = TitleIntro.RiseFrom;

        /// <summary>When the swell starts to settle: as the shade behind the logo starts to fade.</summary>
        public const float SettleFrom = TitleIntro.BackingFrom;

        /// <summary>Seconds a skipped intro's chants take to fall silent.</summary>
        public const float CutSeconds = 0.06f;

        /// <summary>How far the menu music ducks at the swell's peak: 0.65 leaves it at a third.</summary>
        public const float MusicDuck = 0.65f;

        private float started = -1f;

        /// <summary>Gets the intro's chant track: each effect, its level and when it starts, in intro seconds.</summary>
        public static ImmutableArray<(float At, Cue Cue)> Track { get; } =
        [
            (TitleIntro.TifoAt, new Cue(Sfx.Crowd, 0.9f)), // the stands come up with the tifo
            (TitleIntro.MoveFrom, new Cue(Sfx.ChantClap, 1f)), // the home end as the camera comes down
            (TitleIntro.MoveFrom + 0.8f, new Cue(Sfx.Crowd, 1f)), // and the roar meets it at the broadcast view
        ];

        /// <summary>Gets the level the started chants play at now, 0 to 1, on top of the mix and effects volume.</summary>
        public float Level { get; private set; }

        /// <summary>Gets a value indicating whether chants from the intro are still sounding.</summary>
        public bool Sounding { get; private set; }

        /// <summary>Gets the swell along the intro's timeline, 0 to 1: nothing before the tifo, rising to full
        /// as the camera comes down, held while the tifo is up and back to nothing as the menu shows.</summary>
        /// <param name="elapsed">Seconds the intro has played.</param>
        /// <returns>The level, 0 to 1.</returns>
        public static float Swell(float elapsed)
        {
            float rise = TitleIntro.EaseInOut((elapsed - RiseFrom) / (PeakFrom - RiseFrom));
            float settle = TitleIntro.EaseInOut(
                (elapsed - SettleFrom) / (TitleIntro.Seconds - SettleFrom)
            );
            return rise * (1f - settle);
        }

        /// <summary>Gets the menu music's level under the swell, 1 with no swell.</summary>
        /// <param name="level">The swell, 0 to 1.</param>
        /// <returns>The music's gain.</returns>
        public static float MusicGain(float level)
        {
            return 1f - (MusicDuck * Math.Clamp(level, 0f, 1f));
        }

        /// <summary>Follows the intro one step and gives the chants to start now.</summary>
        /// <param name="intro">The intro on screen, or None once it has closed (played out or skipped).</param>
        /// <param name="dt">Seconds since the last step.</param>
        /// <returns>The chants to start, usually none.</returns>
        public List<Cue> Update(Maybe<TitleIntro> intro, float dt)
        {
            List<Cue> start = [];
            if (!intro.TryGet(out TitleIntro timeline) || timeline.Done)
            {
                // closed: fade out what still sounds, quickly enough to be a cut on a skip
                Level = Math.Max(0f, Level - (dt / CutSeconds));
                Sounding &= Level > 0f;
                return start;
            }

            float now = timeline.Elapsed;
            foreach ((float at, Cue cue) in Track)
            {
                if (at > started && at <= now)
                {
                    start.Add(cue);
                }
            }

            started = Math.Max(started, now);
            Level = Swell(now);
            Sounding |= start.Count > 0;
            return start;
        }
    }
}
