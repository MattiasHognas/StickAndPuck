using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Physics
{
    /// <summary>Who last played the puck, from where, and whether it has gone untouched since: what
    /// <see cref="PuckOverGlass"/> reports. A carried puck, a shot or a pass is played cleanly by its
    /// player; a deflection (a body, a blocked shot, a goalie's rebound) makes the deflector the player
    /// and the play not clean; the boards, glass, posts or net make it not clean. Landing on the ice keeps it.</summary>
    internal sealed class PuckPlay
    {
        /// <summary>Gets the player who last played the puck, or -1.</summary>
        public int PlayerId { get; private set; } = -1;

        /// <summary>Gets where the puck was last played.</summary>
        public Vector3 From { get; private set; }

        /// <summary>Gets a value indicating whether the puck has touched nothing but the ice since its player released it.</summary>
        public bool Clean { get; private set; }

        /// <summary>Reads the puck before physics moves it: a carrier, this step's shot or pass, or a new last touch from outside physics.</summary>
        public void Before(World world)
        {
            Puck pk = world.Puck;
            if (pk.Carrier >= 0)
            {
                Played(pk.Carrier, pk.Position, clean: true);
                return;
            }

            if (pk.LastTouch == PlayerId)
            {
                return;
            }

            // a one-timer or a goalie's pass is a fresh release; anything else that changed the last touch deflected it
            foreach (MatchEvent e in world.Events.Current)
            {
                if (
                    (e is ShotTaken s && s.ShooterId == pk.LastTouch)
                    || (e is PassMade p && p.PasserId == pk.LastTouch)
                )
                {
                    Played(pk.LastTouch, pk.Position, clean: true);
                    return;
                }
            }

            Played(pk.LastTouch, pk.Position, clean: false);
        }

        /// <summary>Reads one impact of the step.</summary>
        public void Hit(PuckImpact impact)
        {
            if (impact.Kind == PuckHit.Body)
            {
                Played(impact.PlayerId, impact.Position, clean: false);
            }
            else if (impact.Kind != PuckHit.Ice)
            {
                Clean = false;
            }
        }

        private void Played(int playerId, Vector3 from, bool clean)
        {
            PlayerId = playerId;
            From = from;
            Clean = clean;
        }
    }
}
