namespace StickAndPuck.App.Audio;

/// <summary>The sound effects, one per file in art/audio/ (written by tools/sfx/sfx.py).</summary>
internal enum Sfx
{
    /// <summary>A shot leaving the stick.</summary>
    Shot,

    /// <summary>A pass leaving the stick.</summary>
    Pass,

    /// <summary>The puck landing on a stick or glancing off something.</summary>
    Stick,

    /// <summary>The puck hitting the boards or glass.</summary>
    Boards,

    /// <summary>The puck ringing off a post or the crossbar.</summary>
    Post,

    /// <summary>The referee's whistle.</summary>
    Whistle,

    /// <summary>The goal and period horn.</summary>
    Horn,

    /// <summary>The goal fanfare.</summary>
    Goal,

    /// <summary>A goalie stopping the puck.</summary>
    Save,

    /// <summary>A body check.</summary>
    Check,

    /// <summary>The puck dropped at a faceoff.</summary>
    Faceoff,

    /// <summary>The crowd rising.</summary>
    Crowd,

    /// <summary>The sting after a penalty call.</summary>
    Penalty,

    /// <summary>A fight starting.</summary>
    Fight,

    /// <summary>A line change.</summary>
    LineChange,

    /// <summary>The end-of-match cadence.</summary>
    Final,
}

/// <summary>How one effect is played.</summary>
/// <param name="File">The file name in art/audio/.</param>
/// <param name="Volume">Its level in the mix, 0 to 1.</param>
/// <param name="Gap">The shortest time in seconds between two starts, so a burst of events plays it once.</param>
/// <param name="Spatial">Whether it pans with its position on the ice.</param>
internal readonly record struct SfxInfo(string File, float Volume, float Gap, bool Spatial);

/// <summary>The mix: file, level, retrigger gap and panning for each effect.</summary>
internal static class SfxTable
{
    /// <summary>The folder the effects load from.</summary>
    public const string Folder = "art/audio";

    /// <summary>Gets how an effect is played.</summary>
    /// <param name="sfx">The effect.</param>
    /// <returns>Its settings.</returns>
    public static SfxInfo Info(Sfx sfx)
    {
        return sfx switch
        {
            Sfx.Shot => new("shot.wav", 0.8f, 0.05f, Spatial: true),
            Sfx.Pass => new("pass.wav", 0.6f, 0.05f, Spatial: true),
            Sfx.Stick => new("stick.wav", 0.45f, 0.08f, Spatial: true),
            Sfx.Boards => new("boards.wav", 0.75f, 0.06f, Spatial: true),
            Sfx.Post => new("post.wav", 0.7f, 0.15f, Spatial: true),
            Sfx.Whistle => new("whistle.wav", 0.6f, 0.4f, Spatial: false),
            Sfx.Horn => new("horn.wav", 0.7f, 1.5f, Spatial: false),
            Sfx.Goal => new("goal.wav", 0.6f, 1.0f, Spatial: false),
            Sfx.Save => new("save.wav", 0.7f, 0.08f, Spatial: true),
            Sfx.Check => new("check.wav", 0.8f, 0.1f, Spatial: true),
            Sfx.Faceoff => new("faceoff.wav", 0.6f, 0.2f, Spatial: true),
            Sfx.Crowd => new("crowd.wav", 0.35f, 1.0f, Spatial: false),
            Sfx.Penalty => new("penalty.wav", 0.6f, 0.8f, Spatial: false),
            Sfx.Fight => new("fight.wav", 0.6f, 1.0f, Spatial: false),
            Sfx.LineChange => new("linechange.wav", 0.4f, 0.3f, Spatial: false),
            Sfx.Final => new("final.wav", 0.6f, 2.0f, Spatial: false),
            _ => throw new ArgumentOutOfRangeException(nameof(sfx), sfx, message: null),
        };
    }
}
