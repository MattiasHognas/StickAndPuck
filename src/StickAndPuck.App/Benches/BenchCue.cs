using System.Runtime.InteropServices;
using StickAndPuck.App.Core;
using StickAndPuck.App.Goalie;

namespace StickAndPuck.App.Benches
{
    /// <summary>A reaction a match event asks of one bench: which, how many figures, and for how long.</summary>
    /// <param name="Reaction">The reaction.</param>
    /// <param name="Count">How many figures take it (<see cref="All"/> for the whole bench).</param>
    /// <param name="Seconds">How long each holds it (<see cref="float.PositiveInfinity"/>: until the cause ends).</param>
    [StructLayout(LayoutKind.Auto)]
    public readonly record struct BenchCue(BenchReaction Reaction, int Count, float Seconds)
    {
        /// <summary>The count of a cue for the whole bench.</summary>
        public const int All = int.MaxValue;

        /// <summary>Gets whether a figure stands for a reaction (the others play it seated).</summary>
        public static bool Stands(BenchReaction r)
        {
            return r
                is BenchReaction.Point
                    or BenchReaction.FistPump
                    or BenchReaction.Bang
                    or BenchReaction.Argue
                    or BenchReaction.Cheer
                    or BenchReaction.StandFight;
        }

        /// <summary>Gets the reactions a match event asks of a club's bench, highest first; none for the
        /// events a bench takes no notice of (a pass, a shot, the puck on the boards, a faceoff, a change).</summary>
        /// <param name="e">The event.</param>
        /// <param name="bench">The bench's club.</param>
        public static IReadOnlyList<BenchCue> For(MatchEvent e, Side bench)
        {
            return e switch
            {
                GoalScored g when g.Side == bench => [new(BenchReaction.Cheer, All, 4f)],
                GoalScored => [new(BenchReaction.Slump, All, 3f)],
                MatchWon w when w.Winner == bench => [new(BenchReaction.Cheer, All, 6f)],
                MatchWon => [new(BenchReaction.Slump, All, 6f)],
                PenaltyCalled p when p.Side == bench => [new(BenchReaction.Argue, 3, 3f)],
                PenaltyCalled => [new(BenchReaction.FistPump, 2, 1f)],
                FightStarted => [new(BenchReaction.StandFight, All, float.PositiveInfinity)],
                CheckLanded c when c.Side == bench && c.Strength >= HardCheck =>
                [
                    new(BenchReaction.Bang, 4, 1.5f),
                ],
                ShotSaved s when s.Side == bench && s.Speed >= GoalieTuning.HardShot =>
                [
                    new(BenchReaction.Bang, 3, 1.5f),
                ],
                PuckHitPost => [new(BenchReaction.Ooh, 3, 1.5f)],
                ShotBlocked b when b.Side == bench && b.Slap => [new(BenchReaction.Bang, 3, 1.5f)],
                PlayerInjured i when i.Side == bench => [new(BenchReaction.Trainer, 1, 4f)],
                OffsideCalled o when o.Attacker == bench => [new(BenchReaction.Point, 1, 1.5f)],
                IcingCalled i when i.Offender == bench => [new(BenchReaction.Point, 1, 1.5f)],
                TimeOutCalled t when t.Side == bench =>
                [
                    new(BenchReaction.Chat, All, Rules.Match.MatchTuning.TimeOutSeconds),
                ],
                WhistleBlown => [new(BenchReaction.Chat, 4, 3f), new(BenchReaction.Trainer, 1, 3f)],
                PeriodEnded => [new(BenchReaction.HelmetOff, 1, HelmetSeconds)],
                _ => [],
            };
        }

        /// <summary>The check strength (about 1 for an average solid hit) the checker's bench bangs the boards for.</summary>
        public const float HardCheck = 1.5f;

        /// <summary>How long taking the helmet off takes (the helmet_off clip: 4 poses of 6 frames at 24 fps).</summary>
        public const float HelmetSeconds = 1f;

        /// <summary>The energy below which a skater coming off has had a long shift and takes his helmet off.</summary>
        public const float LongShift = 0.35f;
    }
}
