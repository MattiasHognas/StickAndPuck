#if BROWSER
using System.Runtime.InteropServices;
#else
using Raylib_cs;
#endif

namespace StickAndPuck.App.Render;

/// <summary>Enables colour writes around depth-only passes, with scalar WebAssembly arguments.</summary>
internal static partial class ColorWrite
{
    public static void Set(bool enabled)
    {
#if BROWSER
        // raylib-cs CBool is a one-byte struct. Use scalar bytes across the WASM
        // boundary: CBool arguments produced mixed masks for four false values.
        byte channel = enabled ? (byte)1 : (byte)0;
        SetNative(channel, channel, channel, channel);
#else
        Rlgl.ColorMask(enabled, enabled, enabled, enabled);
#endif
    }

#if BROWSER
    [LibraryImport("raylib", EntryPoint = "rlColorMask")]
    private static partial void SetNative(byte red, byte green, byte blue, byte alpha);
#endif
}
