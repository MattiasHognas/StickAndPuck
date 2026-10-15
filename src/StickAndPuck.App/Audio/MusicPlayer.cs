using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Screens;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Audio;

/// <summary>The music tracks in art/audio/music/.</summary>
internal enum MusicTrack
{
    /// <summary>No music: live play, the pause screen, the test scenes.</summary>
    None,

    /// <summary>The menus, looped.</summary>
    Menu,

    /// <summary>The Zamboni break between periods, looped.</summary>
    Intermission,

    /// <summary>The results screen at the end of a game, played once.</summary>
    Final,
}

/// <summary>Which track plays where: the menu track on the menus, the final track once on the results
/// screen, the intermission track while the Zamboni drives, nothing during play.</summary>
internal static class MusicTracks
{
    /// <summary>The folder the tracks stream from.</summary>
    public const string Folder = "art/audio/music";

    /// <summary>Gets a track's file name in <see cref="Folder"/>.</summary>
    public static string File(MusicTrack track) => track switch
    {
        MusicTrack.Menu => "menu.wav",
        MusicTrack.Intermission => "intermission.wav",
        MusicTrack.Final => "final.wav",
        _ => throw new ArgumentOutOfRangeException(nameof(track), track, "no file"),
    };

    /// <summary>Gets a value indicating whether a track starts over when it ends.</summary>
    public static bool Loops(MusicTrack track) => track != MusicTrack.Final;

    /// <summary>Gets the track for the world on show.</summary>
    public static MusicTrack For(World world)
    {
        var screens = world.App.Screens;
        bool menus = screens.Active && !screens.Playing.HasValue;
        return For(menus, menus && screens.Screens.Any(s => s is ResultsScreen), world.Match.Phase);
    }

    /// <summary>Gets the track: on the menus (<paramref name="results"/> when the results screen is among
    /// them), or in a match in the given phase.</summary>
    public static MusicTrack For(bool menus, bool results, MatchPhase phase)
    {
        if (menus) return results ? MusicTrack.Final : MusicTrack.Menu;
        return phase == MatchPhase.Intermission ? MusicTrack.Intermission : MusicTrack.None;
    }
}

/// <summary>Streams the track <see cref="MusicTracks.For(World)"/> picks at the music volume, starting it
/// when the pick changes. <see cref="AudioSystem"/> owns it, so it lives on the same audio device.</summary>
internal sealed class MusicPlayer : IDisposable
{
    readonly Dictionary<MusicTrack, Music> tracks = [];
    MusicTrack playing = MusicTrack.None;

    /// <summary>Loads the tracks that are there; a missing one stays silent.</summary>
    public void Load()
    {
        foreach (var track in Enum.GetValues<MusicTrack>().Where(t => t != MusicTrack.None))
        {
            string path = Path.Combine(MusicTracks.Folder, MusicTracks.File(track));
            if (!System.IO.File.Exists(path)) continue;
            var music = LoadMusicStream(path);
            if (!IsMusicValid(music)) continue;
            music.Looping = MusicTracks.Loops(track);
            tracks[track] = music;
        }
        Console.WriteLine($"SAP: audio: {tracks.Count} music tracks loaded from {MusicTracks.Folder}");
    }

    /// <summary>Starts, stops and feeds the stream for the world on show.</summary>
    public void Update(World world)
    {
        var track = MusicTracks.For(world);
        if (track != playing)
        {
            if (tracks.TryGetValue(playing, out var old)) StopMusicStream(old);
            playing = track;
            if (tracks.TryGetValue(track, out var next)) PlayMusicStream(next);
            Console.WriteLine($"SAP: audio: music {track}");
        }

        if (!tracks.TryGetValue(playing, out var music)) return;
        SetMusicVolume(music, Math.Clamp(world.Settings.MusicVolume, 0, 1));
        UpdateMusicStream(music);
    }

    public void Dispose()
    {
        foreach (var music in tracks.Values) UnloadMusicStream(music);
        tracks.Clear();
    }
}
