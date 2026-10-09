using StickAndPuck.Core;

namespace StickAndPuck.Audio;

/// <summary>One effect to start.</summary>
/// <param name="Sound">The effect.</param>
/// <param name="Volume">Its level for this hit, 0 to 1, on top of the mix level.</param>
/// <param name="Pitch">Playback pitch, 1 as written.</param>
/// <param name="Delay">Seconds to wait before it starts.</param>
internal readonly record struct Cue(Sfx Sound, float Volume = 1, float Pitch = 1, float Delay = 0);

/// <summary>Which effects each match event plays.</summary>
internal static class SoundCues
{
    /// <summary>Gets the effects for one match event.</summary>
    /// <param name="e">The event.</param>
    /// <returns>The effects, in order.</returns>
    public static Cue[] For(MatchEvent e)
    {
        return e.Kind switch
        {
            MatchEventKind.Goal => [new(Sfx.Horn), new(Sfx.Goal, Delay: 0.2f), new(Sfx.Crowd)],
            MatchEventKind.Faceoff => [new(Sfx.Faceoff)],
            MatchEventKind.Offside => [new(Sfx.Whistle)],
            MatchEventKind.Icing => [new(Sfx.Whistle)],
            MatchEventKind.Penalty => [new(Sfx.Whistle), new(Sfx.Penalty, Delay: 0.6f)],
            MatchEventKind.Fight => [new(Sfx.Fight), new(Sfx.Crowd, 0.8f)],
            MatchEventKind.Save => [new(Sfx.Save, Hardness(e.Strength, 30))],
            MatchEventKind.LineChange => [new(Sfx.LineChange)],
            MatchEventKind.PeriodEnd => [new(Sfx.Horn)],
            MatchEventKind.MatchEnd => [new(Sfx.Horn), new(Sfx.Final, Delay: 1.4f), new(Sfx.Crowd, 0.7f, Delay: 1.4f)],
            MatchEventKind.Shot => [Shot(e.Strength)],
            MatchEventKind.Pass => [new(Sfx.Pass, Hardness(e.Strength, 20))],
            MatchEventKind.Check => [new(Sfx.Check, Hardness(e.Strength, 8))],
            MatchEventKind.PuckBoards => [new(Sfx.Boards, Hardness(e.Strength, 25))],
            MatchEventKind.PuckPost => [new(Sfx.Post, Hardness(e.Strength, 25))],
            MatchEventKind.Whistle => [new(Sfx.Whistle)],
            _ => [],
        };
    }

    /// <summary>Gets whether an event is the puck touching something, so the puck watcher does not play it twice.</summary>
    /// <param name="kind">The event kind.</param>
    /// <returns>True for shots, passes, saves, boards and posts.</returns>
    public static bool IsPuckContact(MatchEventKind kind)
    {
        return kind is MatchEventKind.Shot or MatchEventKind.Pass or MatchEventKind.Save
            or MatchEventKind.PuckBoards or MatchEventKind.PuckPost or MatchEventKind.Faceoff;
    }

    /// <summary>
    /// A level from an event's strength (shot speed, hit force) against the strength that plays at
    /// full level. Events published without a strength play at full level.
    /// </summary>
    /// <param name="strength">The event's strength, 0 when unknown.</param>
    /// <param name="full">The strength that plays at full level.</param>
    /// <returns>A level from 0.35 to 1.</returns>
    public static float Hardness(float strength, float full)
    {
        return strength <= 0 ? 1 : Math.Clamp(strength / full, 0.35f, 1);
    }

    // harder shots play louder and a little lower, so a slap shot reads as heavier than a wrister
    static Cue Shot(float strength)
    {
        float hard = Hardness(strength, 40);
        return new(Sfx.Shot, hard, 1.15f - (0.25f * hard));
    }
}
