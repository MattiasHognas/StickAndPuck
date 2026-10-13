using System.Globalization;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Core;

/// <summary>
/// SAP_RECORD=seconds saves every drawn frame as a numbered PNG in SAP_FRAMES (default frames/)
/// for that much game time, then the game quits. The loop runs one step per frame while recording,
/// so a slow renderer still gives smooth 60 Hz movies. tools/record.sh turns the frames into an mp4.
/// </summary>
internal sealed class FrameRecorder
{
    readonly float seconds;
    readonly string folder;

    FrameRecorder(float seconds, string folder)
    {
        this.seconds = seconds;
        this.folder = folder;
        Directory.CreateDirectory(folder);
    }

    /// <summary>Gets a recorder when SAP_RECORD is set to a positive number of seconds.</summary>
    /// <returns>The recorder, or null.</returns>
    public static FrameRecorder? FromEnvironment()
    {
        if (!float.TryParse(Environment.GetEnvironmentVariable("SAP_RECORD"), NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds) || seconds <= 0)
            return null;
        string folder = Environment.GetEnvironmentVariable("SAP_FRAMES") is { Length: > 0 } f ? f : "frames";
        return new FrameRecorder(seconds, AssetRoot.Output(folder));
    }

    /// <summary>Saves the frame just drawn.</summary>
    /// <param name="world">The world, for the step number and game time.</param>
    /// <returns>True once the recording is long enough.</returns>
    public bool Save(World world)
    {
        var image = LoadImageFromScreen();
        ExportImage(image, Path.Combine(folder, $"{world.Tick:D5}.png"));
        UnloadImage(image);
        return world.Time >= seconds;
    }
}
