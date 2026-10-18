using System.Reflection;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Core
{
    /// <summary>Loads bundled assets. Only native APIs that require filenames use the extraction cache.</summary>
    public static class ResourceLoader
    {
        private const string Prefix = "StickAndPuck.App.";
        private static readonly Assembly Assembly = typeof(ResourceLoader).Assembly;

        private static string Name(string path)
        {
            return Prefix + path.Replace('/', '.').Replace('\\', '.');
        }

        /// <summary>Opens an embedded asset; the caller disposes the stream.</summary>
        public static Stream Open(string path)
        {
            return Assembly.GetManifestResourceStream(Name(path))
                ?? throw new FileNotFoundException($"Embedded asset not found: {Name(path)}", path);
        }

        /// <summary>Checks whether an asset is bundled.</summary>
        public static bool Exists(string path)
        {
            return Assembly.GetManifestResourceInfo(Name(path)) is { };
        }

        /// <summary>Lists resource paths within a folder, in ordinal order.</summary>
        public static IEnumerable<string> List(string folder, string extension)
        {
            return Assembly
                .GetManifestResourceNames()
                .Where(name =>
                    name.StartsWith(Name(folder) + ".", StringComparison.Ordinal)
                    && name.EndsWith(extension, StringComparison.Ordinal)
                )
                .Select(name => name[Prefix.Length..])
                .Order(StringComparer.Ordinal);
        }

        /// <summary>Reads an embedded UTF-8 text asset.</summary>
        public static string ReadText(string path)
        {
            using Stream stream = Open(path);
            using StreamReader reader = new(stream);
            return reader.ReadToEnd();
        }

        private static byte[] ReadBytes(string path)
        {
            using Stream stream = Open(path);
            using MemoryStream bytes = new();
            stream.CopyTo(bytes);
            return bytes.ToArray();
        }

        /// <summary>Extracts an asset for filename-only native APIs. Each build has a separate cache;
        /// atomic replacement prevents concurrent game instances from reading partial files.</summary>
        public static string Extract(string path)
        {
            string name = Name(path)[Prefix.Length..];
            int extension = name.LastIndexOf('.', StringComparison.Ordinal);
            int folder = name.LastIndexOf('.', extension - 1, StringComparison.Ordinal);
            string directory = Path.Combine(
                Path.GetTempPath(),
                "StickAndPuck",
                Assembly.ManifestModule.ModuleVersionId.ToString("N"),
                name[..folder]
            );
            _ = Directory.CreateDirectory(directory);
            string target = Path.Combine(directory, name[(folder + 1)..]);
            if (File.Exists(target))
            {
                return target;
            }

            using Stream stream = Open(path);
            string temporary = Path.Combine(directory, Path.GetRandomFileName());
            try
            {
                using (FileStream file = File.Create(temporary))
                {
                    stream.CopyTo(file);
                }

                File.Move(temporary, target, overwrite: true);
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
            Model model = LoadModel(Extract(path));
            // raylib 6.0's IsModelValid also checks GPU-only bone VBOs on CPU-skinned models.
            return model.MeshCount > 0 && model.MaterialCount > 0
                ? model
                : throw new InvalidDataException($"raylib could not load bundled model: {path}");
        }

        /// <summary>Loads animation clips from the same bundled GLB as the model.</summary>
        public static ModelAnimation[] Animations(string path)
        {
            return LoadModelAnimations(Extract(path)).ToArray();
        }

        /// <summary>Decodes a bundled image; the caller releases it with UnloadImage.</summary>
        public static Image Image(string path)
        {
            byte[] bytes = ReadBytes(path);
            return LoadImageFromMemory(Path.GetExtension(path), bytes);
        }

        /// <summary>Loads a bundled texture and releases the intermediate CPU image.</summary>
        public static Texture2D Texture(string path)
        {
            Image image = Image(path);
            try
            {
                return LoadTextureFromImage(image);
            }
            finally
            {
                UnloadImage(image);
            }
        }

        /// <summary>Loads a bundled WAV sound from the extraction cache.</summary>
        public static Sound Sound(string path)
        {
            Sound sound = LoadSound(Extract(path));
            return IsSoundValid(sound)
                ? sound
                : throw new InvalidDataException($"raylib could not load bundled sound: {path}");
        }

        /// <summary>Streams a bundled track from the cache, without retaining pinned managed buffers.</summary>
        public static Music Music(string path)
        {
            Music music = LoadMusicStream(Extract(path));
            return IsMusicValid(music)
                ? music
                : throw new InvalidDataException($"raylib could not load bundled music: {path}");
        }

        /// <summary>Loads a BMFont and its PNG page together, preserving their relative filenames.</summary>
        public static Font Font(string path)
        {
            _ = Extract(Path.ChangeExtension(path, ".png"));
            Font font = LoadFont(Extract(path));
            return IsFontValid(font)
                ? font
                : throw new InvalidDataException($"raylib could not load bundled font: {path}");
        }

        private static string ShaderPath(string path)
        {
            return OperatingSystem.IsBrowser()
                ? path.Replace("shaders/", "shaders/web/", StringComparison.Ordinal)
                : path;
        }

        /// <summary>Loads a shader pair from bundled sources.</summary>
        public static Shader Shader(string vertex, string fragment)
        {
            return LoadShaderFromMemory(
                ReadText(ShaderPath(vertex)),
                ReadText(ShaderPath(fragment))
            );
        }

        /// <summary>Loads a bundled fragment shader; WebGL uses a high-precision passthrough vertex shader.</summary>
        public static Shader FragmentShader(string path)
        {
            return OperatingSystem.IsBrowser()
                ? LoadShaderFromMemory(ReadText("shaders/web/unlit.vs"), ReadText(ShaderPath(path)))
                : LoadShaderFromMemory(vsCode: default, ReadText(ShaderPath(path)));
        }
    }
}
