using StickAndPuck.App.Core;
using StickAndPuck.App.Rules;

namespace StickAndPuck.Tests.Support;

/// <summary>
/// Builds a match world for a test the way the SAP_SCENE self-tests did: the first two clubs (home
/// and away), default settings, both starting fives on the ice and the match flow begun. Every part
/// can be changed: <c>new WorldBuilder().Rules(RuleSet.Nhl).Human().Build()</c>.
/// </summary>
public sealed class WorldBuilder
{
    static readonly Lazy<IReadOnlyList<ClubData>> AllClubs = new(() => StickAndPuck.App.Core.Clubs.LoadAll());

    MatchSetup setup = Scenes.DefaultSetup(Clubs);
    Settings settings = new();
    int skaters = 5;
    bool starters = true;
    bool begin = true;
    int seed;
    Maybe<PlayingPosition> human;
    readonly List<Action<World>> after = [];
    readonly List<Action<WorldSwitches>> switches = [];

    /// <summary>Gets every club from data/clubs/, loaded once.</summary>
    public static IReadOnlyList<ClubData> Clubs => AllClubs.Value;

    /// <summary>Gets the default exhibition between the first two clubs.</summary>
    public static MatchSetup DefaultSetup => Scenes.DefaultSetup(Clubs);

    /// <summary>Starts from this match setup.</summary>
    /// <param name="value">The setup.</param>
    /// <returns>This builder.</returns>
    public WorldBuilder Setup(MatchSetup value)
    {
        setup = value;
        return this;
    }

    /// <summary>Changes the match setup, for example <c>s => s with { PeriodSeconds = 30 }</c>.</summary>
    /// <param name="change">The change.</param>
    /// <returns>This builder.</returns>
    public WorldBuilder Setup(Func<MatchSetup, MatchSetup> change)
    {
        setup = change(setup);
        return this;
    }

    /// <summary>Plays under a rule set.</summary>
    /// <param name="rules">The rules.</param>
    /// <returns>This builder.</returns>
    public WorldBuilder Rules(RuleSet rules)
    {
        setup = setup.WithRules(rules);
        return this;
    }

    /// <summary>Uses these settings.</summary>
    /// <param name="value">The settings.</param>
    /// <returns>This builder.</returns>
    public WorldBuilder Settings(Settings value)
    {
        settings = value;
        return this;
    }

    /// <summary>Changes the settings, for example <c>s => s with { Lines = LineChanges.Manual }</c>.</summary>
    /// <param name="change">The change.</param>
    /// <returns>This builder.</returns>
    public WorldBuilder Settings(Func<Settings, Settings> change)
    {
        settings = change(settings);
        return this;
    }

    /// <summary>Pins the match seed; 0, the default, starts each random stream from its fixed fallback.</summary>
    /// <param name="value">The seed.</param>
    /// <returns>This builder.</returns>
    public WorldBuilder Seed(int value)
    {
        seed = value;
        return this;
    }

    /// <summary>Puts this many skaters a side on the ice.</summary>
    /// <param name="perSide">Skaters per club.</param>
    /// <returns>This builder.</returns>
    public WorldBuilder Skaters(int perSide)
    {
        skaters = perSide;
        return this;
    }

    /// <summary>Leaves the ice empty: the test adds its own players.</summary>
    /// <returns>This builder.</returns>
    public WorldBuilder NoStarters()
    {
        starters = false;
        return this;
    }

    /// <summary>Does not begin the match flow, for tests of systems that run without it.</summary>
    /// <returns>This builder.</returns>
    public WorldBuilder NotBegun()
    {
        begin = false;
        return this;
    }

    /// <summary>Gives the home player at this position to the keyboard (slot 0).</summary>
    /// <param name="position">The position, the centre by default.</param>
    /// <returns>This builder.</returns>
    public WorldBuilder Human(PlayingPosition position = PlayingPosition.C)
    {
        human = position;
        return this;
    }

    /// <summary>Turns a switch on the new world before anything runs on it, for example <c>s => s.PenaltyStrictness = 0</c>.</summary>
    /// <param name="change">The change.</param>
    /// <returns>This builder.</returns>
    public WorldBuilder Switches(Action<WorldSwitches> change)
    {
        switches.Add(change);
        return this;
    }

    /// <summary>Runs a step on the finished world, after the match flow began.</summary>
    /// <param name="step">The step.</param>
    /// <returns>This builder.</returns>
    public WorldBuilder Then(Action<World> step)
    {
        after.Add(step);
        return this;
    }

    /// <summary>Builds the world.</summary>
    /// <returns>The world.</returns>
    public World Build()
    {
        var world = new World(setup, settings) { Seed = seed };
        foreach (var change in switches) change(world.Switches);
        if (starters) Scenes.AddStarters(world, skaters);
        if (human.TryGet(out var position)) world.OnIce(Side.Home).First(p => p.Data.Position == position).Slot = 0;
        if (begin) MatchFlow.Begin(world, log: false);
        foreach (var step in after) step(world);
        return world;
    }
}
