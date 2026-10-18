using StickAndPuck.App.Core;
using StickAndPuck.App.Screens;

namespace StickAndPuck.App.Audio
{
    /// <summary>Which track plays where: the menu track on the menus, the final track once on the results
    /// screen, the intermission track while the Zamboni drives, nothing during play.</summary>
    internal static class MusicTracks
    {
        /// <summary>The folder the tracks stream from.</summary>
        public const string ResourcePath = "art.audio.music";

        /// <summary>Gets a track's file name.</summary>
        public static string File(MusicTrack track)
        {
            return track switch
            {
                MusicTrack.Menu => "menu.wav",
                MusicTrack.Intermission => "intermission.wav",
                MusicTrack.Final => "final.wav",
                MusicTrack.None => throw new ArgumentOutOfRangeException(
                    nameof(track),
                    track,
                    "no file"
                ),
                _ => throw new ArgumentOutOfRangeException(nameof(track), track, "no file"),
            };
        }

        /// <summary>Gets a value indicating whether a track starts over when it ends.</summary>
        public static bool Loops(MusicTrack track)
        {
            return track != MusicTrack.Final;
        }

        /// <summary>Gets the track for the world on show.</summary>
        public static MusicTrack For(World world)
        {
            ScreenFlow screens = world.App.Screens;
            bool menus = screens.Active && !screens.Playing.HasValue;
            return For(
                menus,
                menus && screens.Screens.Any(s => s is ResultsScreen),
                world.Match.Phase
            );
        }

        /// <summary>Gets the track: on the menus (<paramref name="results"/> when the results screen is among
        /// them), or in a match in the given phase.</summary>
        public static MusicTrack For(bool menus, bool results, MatchPhase phase)
        {
            return menus
                ? (results ? MusicTrack.Final : MusicTrack.Menu)
                : ((phase == MatchPhase.Intermission) ? MusicTrack.Intermission : MusicTrack.None);
        }
    }
}
