namespace StickAndPuck.App.Core;

/// <summary>Settings and progress files: atomic desktop files, origin-local browser storage.</summary>
public static class SavedFiles
{
    /// <summary>Gets a stable save path on the current platform.</summary>
    public static string DefaultPath(string name) => Path.Combine(
        OperatingSystem.IsBrowser() ? "/saves" : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create), "StickAndPuck"), name);

    /// <summary>Checks whether a save exists.</summary>
    public static bool Exists(string path)
    {
#if BROWSER
        if (OperatingSystem.IsBrowser()) return BrowserStorage.Exists(path);
#endif
        return File.Exists(path);
    }

    /// <summary>Reads the saved UTF-8 JSON text.</summary>
    public static string ReadText(string path)
    {
#if BROWSER
        if (OperatingSystem.IsBrowser()) return BrowserStorage.Read(path);
#endif
        return File.ReadAllText(path);
    }

    /// <summary>Replaces a save atomically. Browser storage commits synchronously before returning.</summary>
    public static void WriteText(string path, string text)
    {
#if BROWSER
        if (OperatingSystem.IsBrowser())
        {
            BrowserStorage.Write(path, text);
            return;
        }
#endif
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, text);
        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>Removes a save if it exists.</summary>
    public static void Delete(string path)
    {
#if BROWSER
        if (OperatingSystem.IsBrowser())
        {
            BrowserStorage.Delete(path);
            return;
        }
#endif
        if (File.Exists(path)) File.Delete(path);
    }
}
