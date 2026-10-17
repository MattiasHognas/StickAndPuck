using StickAndPuck.App.Core;
using StickAndPuck.App.Screens;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests;

/// <summary>The generated registrations cover every scene and mode, including derived modes.</summary>
public sealed class DiscoveryTests : CheckedTest
{
    [Test]
    public void RegistryMatchesAllImplementations()
    {
        var types = typeof(Scenes).Assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false } && t.GetConstructor(Type.EmptyTypes) is { }).ToList();
        var scenes = types.Where(t => typeof(IScene).IsAssignableFrom(t)).ToHashSet();
        var modes = types.Where(t => typeof(IGameMode).IsAssignableFrom(t)).ToHashSet();
        Check(scenes.SetEquals(DiscoveryRegistry.Scenes().Select(scene => scene.GetType())), "every scene is registered without runtime reflection");
        Check(modes.SetEquals(DiscoveryRegistry.Modes().Select(mode => mode.GetType())), "every mode, including inherited arcade modes, is registered");
        foreach (var mode in DiscoveryRegistry.Modes())
            Check(GameModes.Named(mode.GetType().Name).Is(named => named.GetType() == mode.GetType() && !ReferenceEquals(named, mode)), $"{mode.GetType().Name} replays create a fresh mode");
        Check(!GameModes.Named("missing mode").HasValue, "an unknown replay mode is rejected");
    }
}
