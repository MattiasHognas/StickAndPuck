using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Momentum;

namespace StickAndPuck.App.Arena
{
    /// <summary>How one section of the crowd answers one match event: the sections within
    /// <paramref name="Reach"/> metres of where it happened take <paramref name="Near"/> for
    /// <paramref name="Seconds"/>, the rest take <paramref name="Far"/> for a little less. A reaction
    /// only replaces a section's current one of the same or a lower <paramref name="Priority"/>.</summary>
    /// <param name="Near">The pose of the sections close to the action.</param>
    /// <param name="Far">The pose of the others; <see cref="Maybe.None{T}"/> leaves them as they are.</param>
    /// <param name="Reach">Metres from the event to a section's middle that still count as close.</param>
    /// <param name="Seconds">How long the close sections hold the pose.</param>
    /// <param name="Priority">Bigger moments win over smaller ones.</param>
    internal readonly record struct CrowdReaction(
        CrowdPose Near,
        Maybe<CrowdPose> Far,
        float Reach,
        float Seconds,
        int Priority
    )
    {
        /// <summary>How much shorter the far sections hold their pose.</summary>
        public const float FarShare = 0.6f;

        /// <summary>A reach that takes in the whole arena.</summary>
        public const float Everywhere = 100f;

        /// <summary>The reaction rules: what a section whose fans support <paramref name="fans"/> does
        /// after <paramref name="e"/>, and where that happened. Add a case here for a new kind of moment.</summary>
        /// <param name="e">One of this step's events.</param>
        /// <param name="fans">The club the section supports.</param>
        /// <returns>The reaction and where it started, or None when the section does not react.</returns>
        public static Maybe<(CrowdReaction Reaction, Vector3 At)> For(MatchEvent e, Side fans)
        {
            Maybe<CrowdReaction> r = e switch
            {
                // our goal: the end it went in at jumps up, the rest of the arena on its feet; theirs sits everyone down
                GoalScored g => g.Side == fans
                    ? new CrowdReaction(CrowdPose.Cheer, CrowdPose.Cheer, 30f, 7f, 4)
                    : new CrowdReaction(CrowdPose.Sit, CrowdPose.Sit, Everywhere, 4f, 4),
                MatchWon w => w.Winner == fans
                    ? new CrowdReaction(CrowdPose.Cheer, CrowdPose.Cheer, Everywhere, 10f, 5)
                    : new CrowdReaction(CrowdPose.Sit, CrowdPose.Sit, Everywhere, 10f, 5),
                // everyone gets up for a fight, the seats beside it loudest
                FightStarted => new CrowdReaction(CrowdPose.Cheer, CrowdPose.Stand, 25f, 5f, 3),
                // a big hit: cheered from the nearby seats when ours threw it, booed when theirs did
                CheckLanded c when c.Strength >= MomentumTuning.BigHitStrength => c.Side == fans
                    ? new CrowdReaction(CrowdPose.Cheer, Maybe.None<CrowdPose>(), 22f, 3f, 2)
                    : new CrowdReaction(CrowdPose.Boo, Maybe.None<CrowdPose>(), 22f, 2.5f, 2),
                // a save: applause near the net, standing up for ours, a near miss for theirs
                ShotSaved s => s.Side == fans
                    ? new CrowdReaction(CrowdPose.Stand, Maybe.None<CrowdPose>(), 25f, 2.5f, 1)
                    : new CrowdReaction(CrowdPose.Stand, Maybe.None<CrowdPose>(), 18f, 2f, 1),
                // the referee's calls: a penalty against us is booed all round, one against them cheered nearby
                PenaltyCalled p => p.Side == fans
                    ? new CrowdReaction(CrowdPose.Boo, CrowdPose.Boo, 25f, 4f, 3)
                    : new CrowdReaction(CrowdPose.Cheer, CrowdPose.Stand, 20f, 2.5f, 2),
                // a penalty shot gets the whole arena up: ours cheer it, theirs stand and wait
                PenaltyShotAwarded s => s.Side == fans
                    ? new CrowdReaction(CrowdPose.Cheer, CrowdPose.Stand, Everywhere, 6f, 3)
                    : new CrowdReaction(CrowdPose.Stand, CrowdPose.Stand, Everywhere, 6f, 3),
                OffsideCalled o when o.Attacker == fans => new CrowdReaction(
                    CrowdPose.Boo,
                    Maybe.None<CrowdPose>(),
                    25f,
                    2f,
                    1
                ),
                IcingCalled i when i.Offender == fans => new CrowdReaction(
                    CrowdPose.Boo,
                    Maybe.None<CrowdPose>(),
                    25f,
                    2f,
                    1
                ),
                _ => Maybe.None<CrowdReaction>(),
            };
            return r.TryGet(out CrowdReaction reaction)
                ? (reaction, Where(e))
                : Maybe.None<(CrowdReaction, Vector3)>();
        }

        private static Vector3 Where(MatchEvent e)
        {
            return e switch
            {
                GoalScored g => g.Position,
                FightStarted f => f.Position,
                CheckLanded c => c.Position,
                ShotSaved s => s.Position,
                PenaltyCalled p => p.Position,
                PenaltyShotAwarded s => s.Position,
                OffsideCalled o => o.Position,
                IcingCalled i => i.Position,
                _ => Vector3.Zero, // the whole arena: the final horn
            };
        }
    }
}
