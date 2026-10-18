using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Rules.Offside.Dev;

namespace StickAndPuck.App.Rules.Icing
{
    /// <summary>Hybrid icing as the SHL plays it. A club that sends the puck from its own half of the red
    /// line over the opponents' goal line, untouched on the way and not into the net, ices it, unless
    /// it is short-handed. The race decides: when the puck crosses the goal line, the club whose
    /// nearest skater is closer to the end-zone dot on the puck's side wins it; the defending club wins
    /// a tie. An attacking win waves icing off. A call publishes Icing (Side = offending club) and
    /// whistles a faceoff at the offending club's own end-zone dot on the side the puck was sent from.
    /// <see cref="RuleSet.Icing"/> switches it: Hybrid as above, Touch (no race: the icing waits
    /// for the next touch and stands when a defender plays the puck first, an attacker waves it off)
    /// or Off. Under <see cref="RuleSet.IcingNoChange"/> the icing club keeps the skaters it had
    /// on the ice until the faceoff after the call (<see cref="HoldsLines"/>; the lines system reads it).</summary>
    internal sealed class IcingRule : ISystem
    {
        /// <summary>The x of the end-zone dots.</summary>
        public const float EndDotX = 20.7f;

        /// <summary>Gets the faceoff dot after icing: the offending club's own end-zone dot on the side the puck was sent from.</summary>
        public static Vector3 FaceoffDot(MatchState match, Side offender, Vector3 sentFrom)
        {
            int own = FaceoffFormation.DefendedEnd(match, offender);
            return MatchFlow.NearestDot(new Vector3(own * EndDotX, 0, sentFrom.Z >= 0 ? 1 : -1));
        }

        /// <summary>Gets whether a club iced the puck and may not change lines until the puck is dropped.</summary>
        public static bool HoldsLines(World world, Side side)
        {
            return world.Icing.TryGet(out IcingState s) && s.Held[(int)side];
        }

        /// <summary>Holds a club's lines as an icing call under <see cref="RuleSet.IcingNoChange"/> does; for the test scenes.</summary>
        internal static void HoldLines(World world, Side side)
        {
            world.Icing.Get(() => new IcingState()).Held[(int)side] = true;
        }

        public void Update(World world, float dt)
        {
            IcingState s = world.Icing.Get(() => new IcingState());
            Puck pk = world.Puck;
            if (world.Match.Phase == MatchPhase.Play)
            {
                Array.Clear(s.Held); // the puck is down: the hold is over
            }

            IcingCall call = world.Setup.Rules.Icing;
            if (
                !MatchFlow.IsRunning(world)
                || world.Match.Phase != MatchPhase.Play
                || !pk.Live
                || call == IcingCall.Off
            )
            {
                s.Touch = -1;
                s.Over = false;
                s.Iced = -1;
                return;
            }
            if (s.Iced >= 0 && Touched(world, s))
            {
                return;
            }

            // the last place the puck was played: every step it is carried, or the step it changes sticks or bodies
            if (pk.Carrier >= 0 || pk.LastTouch != s.Touch)
            {
                s.Touch = pk.LastTouch;
                s.From = pk.Position;
            }

            bool over = Math.Abs(pk.Position.X) > RinkSpec.GoalLineX + RinkSpec.PuckRadius + 0.025f;
            bool crossed = over && !s.Over;
            s.Over = over;
            if (!crossed || pk.Carrier >= 0 || s.Touch < 0 || s.Touch >= world.Players.Count)
            {
                return;
            }

            Side offender = world.Players[s.Touch].Side;
            int end = Math.Sign(pk.Position.X);
            int own = FaceoffFormation.DefendedEnd(world.Match, offender);
            // over the opponents' goal line, sent from the club's own side of the red line
            if (end == own || own * s.From.X <= 0)
            {
                return;
            }

            if (ShortHanded(world.Match, offender))
            {
                return;
            }

            if (call == IcingCall.Touch)
            {
                s.Iced = s.Touch; // touch icing: the next player on the puck decides
                s.IcedFrom = s.From;
                return;
            }
            if (!DefenceWinsRace(world, offender, end, pk.Position))
            {
                return;
            }

            Call(world, offender, s.Touch, s.From);
        }

        /// <summary>Touch icing waiting on the next touch: a defender on the puck calls it, an attacker waves it off.</summary>
        private static bool Touched(World world, IcingState s)
        {
            Puck pk = world.Puck;
            int by = pk.Carrier >= 0 ? pk.Carrier : pk.LastTouch;
            if (by == s.Iced || by < 0 || by >= world.Players.Count)
            {
                return true;
            }

            int iced = s.Iced;
            s.Iced = -1;
            Side offender = world.Players[iced].Side;
            if (world.Players[by].Side != offender)
            {
                Call(world, offender, iced, s.IcedFrom);
            }

            return true;
        }

        private static void Call(World world, Side offender, int touch, Vector3 from)
        {
            Vector3 dot = FaceoffDot(world.Match, offender, from);
            if (!MatchFlow.Whistle(world, dot))
            {
                return;
            }

            world.Events.Publish(new IcingCalled(world.Time, offender, touch, world.Puck.Position));
            if (world.Setup.Rules.IcingNoChange)
            {
                HoldLines(world, offender);
            }

            LineCallsDemo.Log(world, "Icing", offender, world.Puck.Position, dot);
        }

        /// <summary>Gets a value indicating whether a club has fewer skaters by penalty, so it may ice the puck.</summary>
        private static bool ShortHanded(MatchState match, Side side)
        {
            int own = match.Penalties.Count(p => p.Side == side && p.SecondsLeft > 0);
            int other = match.Penalties.Count(p => p.Side != side && p.SecondsLeft > 0);
            return own > other;
        }

        /// <summary>The hybrid race to the end-zone dot on the puck's side: true when a defender is at least as close as every attacker.</summary>
        private static bool DefenceWinsRace(World world, Side offender, int end, Vector3 puck)
        {
            Vector2 dot = new(end * EndDotX, puck.Z >= 0 ? 6.7f : -6.7f);
            float Nearest(Side side)
            {
                return world
                    .OnIce(side)
                    .Where(p => !p.IsGoalie)
                    .Select(p => Vector2.Distance(new Vector2(p.Position.X, p.Position.Z), dot))
                    .DefaultIfEmpty(float.PositiveInfinity)
                    .Min();
            }

            Side defender = offender == Side.Home ? Side.Away : Side.Home;
            return Nearest(defender) <= Nearest(offender);
        }
    }
}
