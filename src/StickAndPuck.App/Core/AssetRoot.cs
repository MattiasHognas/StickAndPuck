using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Core;

/// <summary>Preserves the launch folder for screenshots and recordings; assets load from the assembly.</summary>
public static class AssetRoot
{
    /// <summary>Gets the folder the game was started from, where screenshots and recordings go.</summary>
    public static string StartFolder { get; private set; } = Directory.GetCurrentDirectory();

    /// <summary>Remembers the folder the game was started from.</summary>
    public static void Enter()
    {
        StartFolder = Directory.GetCurrentDirectory();
    }

    /// <summary>Resolves an output path against the folder the game was started from.</summary>
    public static string Output(string path) => Path.GetFullPath(path, StartFolder);

    /// <summary>Saves the window as a PNG in the folder the game was started from.</summary>
    public static void SaveScreen(string path)
    {
        var image = LoadImageFromScreen();
        ExportImage(image, Output(path));
        UnloadImage(image);
    }
}
