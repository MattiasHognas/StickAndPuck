using System.Reflection;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Core;

/// <summary>Loads bundled assets. Only native APIs that require filenames use the extraction cache.</summary>
public static class ResourceLoader
{
    const string Prefix = "StickAndPuck.App.";
    static readonly Assembly Assembly = typeof(ResourceLoader).Assembly;

    static string Name(string path) => Prefix + path.Replace('/', '.').Replace('\\', '.');

    /// <summary>Opens an embedded asset; the caller disposes the stream.</summary>
    public static Stream Open(string path) => Assembly.GetManifestResourceStream(Name(path))
        ?? throw new FileNotFoundException($"Embedded asset not found: {Name(path)}", path);

    /// <summary>Checks whether an asset is bundled.</summary>
    public static bool Exists(string path) => Assembly.GetManifestResourceInfo(Name(path)) is { };

    /// <summary>Lists resource paths within a folder, in ordinal order.</summary>
    public static IEnumerable<string> List(string folder, string extension) => Assembly.GetManifestResourceNames()
        .Where(name => name.StartsWith(Name(folder) + ".", StringComparison.Ordinal) && name.EndsWith(extension, StringComparison.Ordinal))
        .Select(name => name[Prefix.Length..]).Order(StringComparer.Ordinal);

    /// <summary>Reads an embedded UTF-8 text asset.</summary>
    public static string ReadText(string path)
    {
        using var stream = Open(path);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    static byte[] ReadBytes(string path)
    {
        using var stream = Open(path);
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }

    /// <summary>Extracts an asset for filename-only native APIs. Each build has a separate cache;
    /// atomic replacement prevents concurrent game instances from reading partial files.</summary>
    public static string Extract(string path)
    {
        string name = Name(path)[Prefix.Length..];
        int extension = name.LastIndexOf('.');
        int folder = name.LastIndexOf('.', extension - 1);
        string directory = Path.Combine(Path.GetTempPath(), "StickAndPuck", Assembly.ManifestModule.ModuleVersionId.ToString("N"), name[..folder]);
        Directory.CreateDirectory(directory);
        string target = Path.Combine(directory, name[(folder + 1)..]);
        if (File.Exists(target)) return target;
        using var stream = Open(path);
        string temporary = Path.Combine(directory, Path.GetRandomFileName());
        try
        {
            using (var file = File.Create(temporary)) stream.CopyTo(file);
            File.Move(temporary, target, true);
        }
        finally
        {
            File.Delete(temporary);
        }
        return target;
    }

    /// <summary>Loads a model from its bundled GLB.</summary>
    public static Model Model(string path)
    {
        var model = LoadModel(Extract(path));
        // raylib 6.0's IsModelValid also checks GPU-only bone VBOs on CPU-skinned models.
        return model.MeshCount > 0 && model.MaterialCount > 0 ? model : throw new InvalidDataException($"raylib could not load bundled model: {path}");
    }

    /// <summary>Loads animation clips from the same bundled GLB as the model.</summary>
    public static ModelAnimation[] Animations(string path) => LoadModelAnimations(Extract(path)).ToArray();

    /// <summary>Decodes a bundled image; the caller releases it with UnloadImage.</summary>
    public static unsafe Image Image(string path)
    {
        byte[] bytes = ReadBytes(path);
        byte[] extension = System.Text.Encoding.UTF8.GetBytes(Path.GetExtension(path) + "\0");
        fixed (byte* data = bytes)
        fixed (byte* ext = extension)
            return LoadImageFromMemory((sbyte*)ext, data, bytes.Length);
    }

    /// <summary>Loads a bundled texture and releases the intermediate CPU image.</summary>
    public static Texture2D Texture(string path)
    {
        var image = Image(path);
        try { return LoadTextureFromImage(image); }
        finally { UnloadImage(image); }
    }

    /// <summary>Loads a bundled WAV sound and releases the decoded wave.</summary>
    public static unsafe Sound Sound(string path)
    {
        byte[] bytes = ReadBytes(path);
        byte[] extension = System.Text.Encoding.UTF8.GetBytes(".wav\0");
        fixed (byte* data = bytes)
        fixed (byte* ext = extension)
        {
            var wave = LoadWaveFromMemory((sbyte*)ext, data, bytes.Length);
            try { return LoadSoundFromWave(wave); }
            finally { UnloadWave(wave); }
        }
    }

    /// <summary>Streams a bundled track from the cache, without retaining pinned managed buffers.</summary>
    public static Music Music(string path)
    {
        var music = LoadMusicStream(Extract(path));
        return IsMusicValid(music) ? music : throw new InvalidDataException($"raylib could not load bundled music: {path}");
    }

    /// <summary>Loads a BMFont and its PNG page together, preserving their relative filenames.</summary>
    public static Font Font(string path)
    {
        Extract(Path.ChangeExtension(path, ".png"));
        var font = LoadFont(Extract(path));
        return IsFontValid(font) ? font : throw new InvalidDataException($"raylib could not load bundled font: {path}");
    }

    static string ShaderPath(string path) => OperatingSystem.IsBrowser() ? path.Replace("shaders/", "shaders/web/", StringComparison.Ordinal) : path;

    /// <summary>Loads a shader pair from bundled sources.</summary>
    public static Shader Shader(string vertex, string fragment) => LoadShaderFromMemory(ReadText(ShaderPath(vertex)), ReadText(ShaderPath(fragment)));

    /// <summary>Loads a bundled fragment shader; WebGL uses a high-precision passthrough vertex shader.</summary>
    public static unsafe Shader FragmentShader(string path)
    {
        if (OperatingSystem.IsBrowser()) return LoadShaderFromMemory(ReadText("shaders/web/unlit.vs"), ReadText(ShaderPath(path)));
        byte[] source = System.Text.Encoding.UTF8.GetBytes(ReadText(ShaderPath(path)) + "\0");
        fixed (byte* fs = source) return LoadShaderFromMemory((sbyte*)0, (sbyte*)fs);
    }
}
