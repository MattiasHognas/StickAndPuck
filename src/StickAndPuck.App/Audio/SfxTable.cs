namespace StickAndPuck.App.Audio
{
    /// <summary>The mix: file, level, retrigger gap and panning for each effect.</summary>
    internal static class SfxTable
    {
        /// <summary>The folder the effects load from.</summary>
        public const string ResourcePath = "art.audio.effects";

        /// <summary>Gets how an effect is played.</summary>
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
                _ => throw new ArgumentOutOfRangeException(nameof(sfx), sfx, "unknown sound"),
            };
        }
    }
}
