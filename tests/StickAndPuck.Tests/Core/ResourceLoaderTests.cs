using StickAndPuck.App.Core;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests;

/// <summary>Bundled assets resolve without loose files, including native loaders' extracted companions.</summary>
public sealed class ResourceLoaderTests : CheckedTest
{
    [Test]
    public void RequiredAssetsAreBundled()
    {
        string[] paths = [Assets.Skater, Assets.Goalie, Assets.Referee, Assets.Arena,
            "art/player/staff.glb", "art/zamboni/zamboni.glb", Assets.IceScratches,
            "art/ui/font.fnt", "art/ui/font.png", Assets.UiLayout,
            "shaders/toon.vs", "shaders/toon.fs", "shaders/mask.fs", "shaders/post.fs",
            "shaders/web/toon.vs", "shaders/web/toon.fs", "shaders/web/mask.fs", "shaders/web/post.fs",
            "data/lang/en.json", "data/lang/sv.json",
            "art/audio/music/menu.wav", "art/audio/music/intermission.wav", "art/audio/music/final.wav"];
        foreach (string path in paths)
        {
            Check(ResourceLoader.Exists(path), $"{path} is embedded");
            using var stream = ResourceLoader.Open(path);
            Check(stream.Length > 0, $"{path} has content");
        }
        Check(Clubs.LoadAll().Count == 14, "all fourteen embedded clubs load");
    }

    [Test]
    public void ExtractedModelsMatchEmbeddedBytes()
    {
        string[] paths = new string[8];
        Parallel.For(0, paths.Length, i => paths[i] = ResourceLoader.Extract(Assets.Skater));
        Check(paths.All(p => string.Equals(p, paths[0], StringComparison.Ordinal)), "parallel loads share one complete extracted file");
        using var stream = ResourceLoader.Open(Assets.Skater);
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        Check(File.ReadAllBytes(paths[0]).SequenceEqual(bytes.ToArray()), "extraction preserves the whole GLB");
        Check(paths[0].Contains(typeof(ResourceLoader).Assembly.ManifestModule.ModuleVersionId.ToString("N"), StringComparison.Ordinal),
            "the extraction cache is isolated from other builds");
    }

    [Test]
    public void FontPageRemainsBesideDescriptor()
    {
        string font = ResourceLoader.Extract("art/ui/font.fnt");
        string page = ResourceLoader.Extract("art.ui.font.png");
        Check(string.Equals(Path.Combine(Path.GetDirectoryName(font), "font.png"), page, StringComparison.Ordinal), "the BMFont page resolves beside the descriptor");
        Check(ResourceLoader.ReadText("art/ui/font.fnt").Contains("file=\"font.png\"", StringComparison.Ordinal), "the descriptor uses that page");
    }
}
