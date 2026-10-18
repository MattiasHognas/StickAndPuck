#if BROWSER
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace StickAndPuck.App.Core
{
    /// <summary>Synchronous persistence for the three small JSON saves, independent of the WASM filesystem.</summary>
    [SupportedOSPlatform("browser")]
    internal static partial class BrowserStorage
    {
        [JSImport("saves.exists", "browser")]
        internal static partial bool Exists(string path);

        [JSImport("saves.read", "browser")]
        internal static partial string Read(string path);

        [JSImport("saves.write", "browser")]
        private static partial void WriteStored(string path, string text);

        internal static void Write(string path, string text)
        {
            try
            {
                WriteStored(path, text);
            }
            catch (JSException error)
            {
                throw new IOException($"Browser save failed: {path}", error);
            }
        }

        [JSImport("saves.delete", "browser")]
        internal static partial void Delete(string path);
    }
}
#endif
