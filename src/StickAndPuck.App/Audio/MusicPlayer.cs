using Raylib_cs;
using StickAndPuck.App.Core;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Audio
{
    /// <summary>Streams the track <see cref="MusicTracks.For(World)"/> picks at the music volume, starting it
    /// when the pick changes. <see cref="AudioSystem"/> owns it, so it lives on the same audio device.</summary>
    internal sealed class MusicPlayer : IDisposable
    {
        private readonly Dictionary<MusicTrack, Music> tracks = [];
        private MusicTrack playing = MusicTrack.None;

        /// <summary>Loads the tracks that are there; a missing one stays silent.</summary>
        public void Load()
        {
            foreach (
                MusicTrack track in Enum.GetValues<MusicTrack>().Where(t => t != MusicTrack.None)
            )
            {
                Music music = ResourceLoader.Music(
                    $"{MusicTracks.ResourcePath}.{MusicTracks.File(track)}"
                );
                if (!IsMusicValid(music))
                {
                    continue;
                }

                music.Looping = MusicTracks.Loops(track);
                tracks[track] = music;
            }

            Console.WriteLine(
                $"SAP: audio: {tracks.Count} music tracks loaded from embedded assembly resources"
            );
        }

        /// <summary>Starts, stops and feeds the stream for the world on show.</summary>
        public void Update(World world)
        {
            MusicTrack track = MusicTracks.For(world);
            if (track != playing)
            {
                if (tracks.TryGetValue(playing, out Music old))
                {
                    StopMusicStream(old);
                }

                playing = track;
                if (tracks.TryGetValue(track, out Music next))
                {
                    PlayMusicStream(next);
                }

                Console.WriteLine($"SAP: audio: music {track}");
            }
            if (!tracks.TryGetValue(playing, out Music music))
            {
                return;
            }

            SetMusicVolume(music, Math.Clamp(world.Settings.MusicVolume, 0, 1));
            UpdateMusicStream(music);
        }

        public void Dispose()
        {
            foreach (Music music in tracks.Values)
            {
                UnloadMusicStream(music);
            }

            tracks.Clear();
        }
    }
}
