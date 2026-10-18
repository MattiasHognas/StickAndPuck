using System.Diagnostics;
using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Audio
{
    /// <summary>Which effects each match event plays.</summary>
    internal static class SoundCues
    {
        /// <summary>Gets the effects for one match event.</summary>
        public static Cue[] For(MatchEvent e)
        {
            return e switch
            {
                GoalScored => [new(Sfx.Horn), new(Sfx.Goal, Delay: 0.2f), new(Sfx.Crowd)],
                PuckDropped => [new(Sfx.Faceoff)],
                OffsideCalled => [new(Sfx.Whistle)],
                IcingCalled => [new(Sfx.Whistle)],
                PenaltyCalled => [new(Sfx.Whistle), new(Sfx.Penalty, Delay: 0.6f)],
                FightStarted => [new(Sfx.Fight), new(Sfx.Crowd, 0.8f)],
                ShotSaved s => [new(Sfx.Save, Hardness(s.Speed, 30))],
                LineChanged => [new(Sfx.LineChange)],
                PeriodEnded => [new(Sfx.Horn)],
                MatchEnded =>
                [
                    new(Sfx.Horn),
                    new(Sfx.Final, Delay: 1.4f),
                    new(Sfx.Crowd, 0.7f, Delay: 1.4f),
                ],
                ShotTaken s => [Shot(s.Speed)],
                PassMade p => [new(Sfx.Pass, Hardness(p.Speed, 20))],
                CheckLanded c => [new(Sfx.Check, Hardness(c.Strength, 8))],
                PuckHitBoards b => [new(Sfx.Boards, Hardness(b.Speed, 25))],
                PuckHitPost p => [new(Sfx.Post, Hardness(p.Speed, 25))],
                WhistleBlown => [new(Sfx.Whistle)],
                _ => throw new UnreachableException($"no sound cues for {e.GetType().Name}"),
            };
        }

        /// <summary>Gets where on the ice an event happened, for panning; the centre for period and match ends.</summary>
        public static Vector3 Where(MatchEvent e)
        {
            return e switch
            {
                GoalScored g => g.Position,
                PuckDropped f => f.Dot,
                OffsideCalled o => o.Position,
                IcingCalled i => i.Position,
                PenaltyCalled p => p.Position,
                FightStarted f => f.Position,
                ShotSaved s => s.Position,
                LineChanged l => l.Position,
                PeriodEnded or MatchEnded => Vector3.Zero,
                ShotTaken s => s.Position,
                PassMade p => p.Position,
                CheckLanded c => c.Position,
                PuckHitBoards b => b.Position,
                PuckHitPost p => p.Position,
                WhistleBlown w => w.Position,
                _ => throw new UnreachableException($"no position for {e.GetType().Name}"),
            };
        }

        /// <summary>Gets one sample of every event type at a spot, for the audio demo.</summary>
        public static MatchEvent[] Samples(float time, Vector3 at)
        {
            return
            [
                new GoalScored(time, Side.Home, 0, at, 0),
                new PuckDropped(time, at),
                new OffsideCalled(time, Side.Home, at),
                new IcingCalled(time, Side.Home, 0, at),
                new PenaltyCalled(time, Side.Home, 0, at, 120),
                new FightStarted(time, Side.Home, 0, at),
                new ShotSaved(time, Side.Home, 0, at, 0),
                new LineChanged(time, Side.Home, 0, at),
                new PeriodEnded(time, 1),
                new MatchWon(time, Side.Home),
                new ShotTaken(time, Side.Home, 0, at, 0),
                new PassMade(time, Side.Home, 0, at, 0),
                new CheckLanded(time, Side.Home, 0, at, 0),
                new PuckHitBoards(time, at, 0),
                new PuckHitPost(time, at, 0),
                new WhistleBlown(time, at),
            ];
        }

        /// <summary>Gets whether an event is the puck touching something, so the puck watcher does not play it twice.</summary>
        public static bool IsPuckContact(MatchEvent e)
        {
            return e
                is ShotTaken
                    or PassMade
                    or ShotSaved
                    or PuckHitBoards
                    or PuckHitPost
                    or PuckDropped;
        }

        /// <summary>A level from an event's strength (shot speed, hit force) against the strength that plays at
        /// full level. Events published without a strength play at full level.</summary>
        public static float Hardness(float strength, float full)
        {
            return strength <= 0 ? 1 : Math.Clamp(strength / full, 0.35f, 1);
        }

        /// <summary>Gets the shot cue for a puck speed: harder shots play louder and a little lower, so a slap shot reads as heavier than a wrister.</summary>
        public static Cue Shot(float strength)
        {
            float hard = Hardness(strength, 40);
            return new(Sfx.Shot, hard, 1.15f - (0.25f * hard));
        }
    }
}
