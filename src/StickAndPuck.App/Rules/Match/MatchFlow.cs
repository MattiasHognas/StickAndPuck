using System.Globalization;
using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Rules.Match
{
    /// <summary>The match flow other systems call into: start a match, blow the whistle for a faceoff at a dot
    /// (offside and icing), start an overtime period, and let the referee drop the puck.
    /// <see cref="MatchRules"/> runs the flow each step. A world that never calls <see cref="Begin"/>
    /// (practice and test scenes) has no match flow: no clock, no faceoffs.</summary>
    /// <remarks>
    /// What the flow promises the readers of <see cref="World.Match"/>:
    /// the clock runs only in <see cref="MatchPhase.Play"/>; during <see cref="MatchPhase.Faceoff"/>
    /// the puck lies dead on <see cref="FaceoffDot"/> and the players hold their spots, also through the draw window after the drop;
    /// a goal (published by physics) is counted once and followed by a centre faceoff;
    /// each period end publishes PeriodEnd; after regulation a tie hands over to the overtime rules by setting
    /// <see cref="MatchPhase.Overtime"/> or <see cref="MatchPhase.Shootout"/>; otherwise MatchEnd is
    /// published once (MatchWon with the winner, or MatchTied) and the phase becomes Final.
    /// A goal in a period after regulation is sudden death.
    /// </remarks>
    public static class MatchFlow
    {
        /// <summary>Starts the match: score to zero, period 1, the intro, then the opening faceoff at centre.</summary>
        public static void Begin(World world, bool log = true)
        {
            FlowState s = State(world);
            s.Active = true;
            s.Log = log;
            MatchState m = world.Match;
            m.Score[0] = m.Score[1] = 0;
            m.Shots[0] = m.Shots[1] = 0;
            m.Period = 1;
            m.Clock = world.Setup.PeriodSeconds;
            m.Phase = MatchPhase.Intro;
            s.Timer = MatchTuning.IntroSeconds;
            s.Dot = RinkSpec.CentreDot;
            StopPuck(world, RinkSpec.CentreDot);
        }

        /// <summary>Gets a value indicating whether the world runs the match flow.</summary>
        public static bool IsRunning(World world)
        {
            return world.Flow.TryGet(out FlowState s) && s.Active;
        }

        /// <summary>Gets the dot of the coming (or last) faceoff; the referee skates here during a stoppage.</summary>
        public static Vector3 FaceoffDot(World world)
        {
            return State(world).Dot;
        }

        /// <summary>Stops play for a faceoff at a dot: puck dead, clock stopped, a Whistle event, then the faceoff
        /// after <see cref="MatchTuning.StoppageSeconds"/>. Publish your own Offside or Icing event too.</summary>
        public static bool Whistle(World world, Vector3 dot)
        {
            if (!IsRunning(world) || world.Match.Phase != MatchPhase.Play)
            {
                return false;
            }

            FlowState s = State(world);
            world.Match.Phase = MatchPhase.Stopped;
            world.Puck.Live = false;
            s.Timer = MatchTuning.StoppageSeconds;
            s.Dot = dot;
            world.Events.Publish(new WhistleBlown(world.Time, world.Puck.Position));
            return true;
        }

        /// <summary>Starts a period after regulation (a 3 on 3 overtime): clock set, faceoff at centre. A goal in it ends the match.</summary>
        public static void StartPeriod(World world, int period, float seconds)
        {
            world.Match.Period = period;
            world.Match.Clock = seconds;
            SetUpFaceoff(world, RinkSpec.CentreDot);
        }

        /// <summary>Hands puck drops to the referee: faceoffs wait for <see cref="Drop"/> instead of a timer.</summary>
        public static void SetRefereeDrops(World world, bool referee)
        {
            State(world).RefereeDrops = referee;
        }

        /// <summary>Drops the puck if a faceoff is waiting; the referee calls this. The draw window opens and play starts
        /// when <see cref="FaceoffDraw"/> decides the draw (at once after a false start).</summary>
        public static bool Drop(World world)
        {
            if (!IsRunning(world) || world.Match.Phase != MatchPhase.Faceoff)
            {
                return false;
            }

            FlowState s = State(world);
            if (s.Draw.Dropped)
            {
                return false;
            }

            world.Puck.Position = s.Dot;
            world.Puck.Velocity = Vector3.Zero;
            world.Events.Publish(new PuckDropped(world.Time, s.Dot));
            FaceoffDraw.Drop(world, s);
            return true;
        }

        /// <summary>Gets a value indicating whether the faceoff prompt shows: a human takes the coming draw.</summary>
        public static bool FaceoffPrompt(World world)
        {
            return IsRunning(world)
                && world.Match.Phase == MatchPhase.Faceoff
                && FaceoffDraw.HumanCentre(world, State(world));
        }

        /// <summary>Gets the faceoff dot nearest a point on the ice.</summary>
        public static Vector3 NearestDot(Vector3 position)
        {
            return RinkSpec.FaceoffDots.MinBy(d =>
                Vector2.DistanceSquared(new(d.X, d.Z), new(position.X, position.Z))
            );
        }

        /// <summary>Formats game seconds as m:ss, rounding up so the clock shows 0:00 only when time is out.</summary>
        public static string ClockText(float seconds)
        {
            int s = (int)MathF.Ceiling(Math.Max(0, seconds) - 0.0001f);
            return string.Create(CultureInfo.InvariantCulture, $"{s / 60}:{s % 60:00}");
        }

        internal static FlowState State(World world)
        {
            return world.Flow.Get(() =>
                new FlowState { Dice = Seeds.Dice(world.Seed, "matchflow", 43) }
            );
        }

        internal static void SetUpFaceoff(World world, Vector3 dot)
        {
            FlowState s = State(world);
            s.Dot = dot;
            s.Timer = MatchTuning.FaceoffSeconds;
            world.Match.Phase = MatchPhase.Faceoff;
            StopPuck(world, dot);
            FaceoffFormation.Place(world, dot);
            FaceoffDraw.Reset(world, s);
        }

        private static void StopPuck(World world, Vector3 dot)
        {
            Puck pk = world.Puck;
            pk.Live = false;
            pk.Carrier = -1;
            pk.LastTouch = -1;
            pk.Position = dot with { Y = 0 };
            pk.Velocity = Vector3.Zero;
        }
    }
}
