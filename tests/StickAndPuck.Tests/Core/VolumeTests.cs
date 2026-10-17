using System.Globalization;
using StickAndPuck.App.Audio;
using StickAndPuck.App.Core;
using StickAndPuck.App.Screens;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests;

/// <summary>
/// The overall, effects and music volumes: an old settings.json's single volume carries over to the
/// effects, the three survive a restart, the rows step by tenths between 0 and 10, and the music
/// track each screen and match phase picks.
/// </summary>
public sealed class VolumeTests : CheckedTest
{
    readonly string dir = Path.Combine(Path.GetTempPath(), string.Create(CultureInfo.InvariantCulture, $"sap_volumetest_{Environment.ProcessId}_{Guid.NewGuid():N}"));

    [Before(Test)]
    public void MakeFolder() => Directory.CreateDirectory(dir);

    [After(Test)]
    public void CleanUp() => Directory.Delete(dir, recursive: true);

    static bool Near(float a, float b) => MathF.Abs(a - b) < 1e-4f;

    [Test]
    public void OldVolumeCarriesOver()
    {
        string path = Path.Combine(dir, "settings.json");
        File.WriteAllText(path, "{ \"settings\": { \"volume\": 0.3, \"fights\": false }, \"periodMinutes\": 5 }");
        var read = ScreenPrefsFile.Read(path).Settings;
        Check(Near(read.EffectsVolume, 0.3f), string.Create(CultureInfo.InvariantCulture, $"an old settings.json's volume is the effects volume ({read.EffectsVolume})"));
        Check(Near(read.OverallVolume, 1f) && Near(read.MusicVolume, new Settings().MusicVolume), "the overall and music volumes start at their defaults");
        Check(!read.Fights, "the rest of the old settings still load");

        ScreenPrefsFile.Write(new ScreenPrefs { Settings = read }, path);
        string json = File.ReadAllText(path);
        Check(!json.Contains("\"volume\"", StringComparison.Ordinal), "the old key is not written again");
        Check(Near(ScreenPrefsFile.Read(path).Settings.EffectsVolume, 0.3f), "the carried-over volume survives the next save");

        File.WriteAllText(path, "{ \"settings\": { \"volume\": 0.3, \"effectsVolume\": 0.9 } }");
        Check(Near(ScreenPrefsFile.Read(path).Settings.EffectsVolume, 0.9f), "a saved effects volume wins over a stray old one");
    }

    [Test]
    public void VolumesSurviveRestart()
    {
        string path = Path.Combine(dir, "settings.json");
        var saved = new Settings { OverallVolume = 0.5f, EffectsVolume = 0.2f, MusicVolume = 0f };
        ScreenPrefsFile.Write(new ScreenPrefs { Settings = saved }, path);
        var read = ScreenPrefsFile.Read(path).Settings;
        Check(Near(read.OverallVolume, 0.5f) && Near(read.EffectsVolume, 0.2f) && Near(read.MusicVolume, 0f), "all three volumes are saved and read back");
        Check(Near(ScreenPrefsFile.FromJson(ScreenPrefsFile.ToJson(new ScreenPrefs { Settings = saved })).Settings.MusicVolume, 0f), "a replay keeps them too");
    }

    [Test]
    public void RowsStepByTenths()
    {
        Check(Near(SettingsScreen.Step(0.8f, 1), 0.9f) && Near(SettingsScreen.Step(0.8f, -1), 0.7f), "left and right step a tenth");
        Check(Near(SettingsScreen.Step(1f, 1), 1f) && Near(SettingsScreen.Step(0f, -1), 0f), "the rows stop at 0 and 10");
    }

    [Test]
    public void TrackPerScreen()
    {
        Check(MusicTracks.For(menus: true, results: false, MatchPhase.Intro) == MusicTrack.Menu, "the menus play the menu track");
        Check(MusicTracks.For(menus: true, results: true, MatchPhase.Intro) == MusicTrack.Final, "the results screen plays the final track");
        Check(MusicTracks.For(menus: false, results: false, MatchPhase.Intermission) == MusicTrack.Intermission, "the Zamboni break plays the intermission track");
        foreach (var phase in new[] { MatchPhase.Faceoff, MatchPhase.Play, MatchPhase.Stopped, MatchPhase.Goal, MatchPhase.Overtime, MatchPhase.Shootout, MatchPhase.Final })
            Check(MusicTracks.For(menus: false, results: false, phase) == MusicTrack.None, $"no music in a match's {phase}");
        Check(MusicTracks.Loops(MusicTrack.Menu) && MusicTracks.Loops(MusicTrack.Intermission) && !MusicTracks.Loops(MusicTrack.Final), "the menu and intermission tracks loop, the final plays once");

        var world = new WorldBuilder().NoStarters().NotBegun().Build();
        Check(MusicTracks.For(world) == MusicTrack.None, "a test scene with no screens is silent");
        foreach (var track in new[] { MusicTrack.Menu, MusicTrack.Intermission, MusicTrack.Final })
        {
            string file = $"{MusicTracks.ResourcePath}.{MusicTracks.File(track)}";
            Check(ResourceLoader.Exists(file), $"{MusicTracks.File(track)} is embedded in the game");
        }
    }
}
