using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match;

namespace StickAndPuck.App.Arena
{
    /// <summary>W120: when the home tifo goes up and comes down, on <see cref="CrowdState.Tifo"/>. It goes up
    /// over the far centre stand as the match starts (the intro and the warm-up) and over the far stand at the
    /// scoring end after a home goal; an away goal takes it down at once, and otherwise it comes down once the
    /// puck is back in play and it has been up <see cref="MinUp"/> seconds. Off with
    /// <see cref="Settings.Supporters"/>. Presentation only: it reads the world's events and phase, rolls no
    /// dice and writes nothing the simulation reads, so replays stay the same.</summary>
    internal sealed class TifoMoments
    {
        /// <summary>The shortest time the tifo stays up, in seconds.</summary>
        public const float MinUp = 8f;

        /// <summary>The stand the match-start tifo goes up in: square to the broadcast camera.</summary>
        public const CrowdSection StartSection = CrowdSection.FarCentre;

        /// <summary>A home goal this far toward -x (metres) raises the tifo in the far stand's -x third; any
        /// other goal in the far centre. The +x third is the visitors' corner (<see cref="CrowdFans"/>).</summary>
        public const float LeftEndFrom = -8f;

        private bool started;

        /// <summary>Raises or lowers the tifo for this step.</summary>
        /// <param name="world">The match: its settings, events, phase, puck and crowd.</param>
        public void Update(World world)
        {
            TifoState tifo = world.Crowd.Tifo;
            float now = world.Time;
            if (!world.Settings.Supporters)
            {
                tifo.Lower(now);
                return;
            }

            bool flow = MatchFlow.IsRunning(world);
            if (!started && flow && world.Match.Phase == MatchPhase.Intro)
            {
                started = true;
                tifo.Raise(StartSection, now);
            }

            foreach (GoalScored goal in world.Events.Of<GoalScored>())
            {
                if (goal.Side == Side.Home)
                {
                    tifo.Raise(SectionFor(goal.Position), now);
                }
                else
                {
                    tifo.Lower(now); // the home end goes quiet
                }
            }

            bool inPlay = !flow || world.Match.Phase == MatchPhase.Play;
            if (tifo.Up && inPlay && world.Puck.Live && now - tifo.RaisedAt >= MinUp)
            {
                tifo.Lower(now);
            }
        }

        /// <summary>Gets the far stand section that raises the tifo for a home goal.</summary>
        /// <param name="at">Where the puck went in.</param>
        /// <returns><see cref="CrowdSection.FarLeft"/> at the -x end, else <see cref="CrowdSection.FarCentre"/>.</returns>
        public static CrowdSection SectionFor(Vector3 at)
        {
            return at.X <= LeftEndFrom ? CrowdSection.FarLeft : CrowdSection.FarCentre;
        }
    }
}
