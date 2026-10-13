using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Core;

/// <summary>
/// Where the asset paths in <see cref="Assets"/> are read from. dotnet run from the repo root reads
/// them in place; a published build reads them from beside the executable, or from the folder a
/// single-file build unpacks itself into. Screenshots and recordings still land where the game was started.
/// </summary>
public static class AssetRoot
{
    /// <summary>Gets the folder the game was started from, where screenshots and recordings go.</summary>
    public static string StartFolder { get; private set; } = Directory.GetCurrentDirectory();

    /// <summary>Moves into the asset folder unless the game was started next to its assets.</summary>
    public static void Enter()
    {
        StartFolder = Directory.GetCurrentDirectory();
        if (!Directory.Exists(Assets.Shaders))
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);
    }

    /// <summary>Resolves an output path against the folder the game was started from.</summary>
    /// <param name="path">A relative or absolute path.</param>
    /// <returns>The absolute path.</returns>
    public static string Output(string path) => Path.GetFullPath(path, StartFolder);

    /// <summary>Saves the window as a PNG in the folder the game was started from.</summary>
    /// <param name="path">A relative or absolute path.</param>
    public static void SaveScreen(string path)
    {
        var image = LoadImageFromScreen();
        ExportImage(image, Output(path));
        UnloadImage(image);
    }
}
