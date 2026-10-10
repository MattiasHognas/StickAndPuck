using System.Globalization;
using StickAndPuck.Core;
using StickAndPuck.Season;

namespace StickAndPuck.Screens;

/// <summary>
/// The arcade entries on the "new game" list, over W16c's <see cref="ArcadeSession"/>: a new run
/// with three lives or sudden death (pick a club), or continue the saved run. Each game's result is
/// recorded and saved by W16c's ArcadeSystem; the results screen offers the next game while the
/// run lasts.
/// </summary>
internal abstract class ArcadeMode : IGameMode
{
    bool begun;

    /// <inheritdoc/>
    public abstract string Title { get; }

    /// <inheritdoc/>
    public virtual int Order => 20;

    /// <inheritdoc/>
    public bool HasNextGame => ArcadeSession.Current is { IsOver: false };

    /// <inheritdoc/>
    public virtual int ClubPicks => 1;

    /// <inheritdoc/>
    public virtual bool Available => true;

    /// <inheritdoc/>
    public string? Status
    {
        get
        {
            if (ArcadeSession.Current is not { } run) return null;
            string wins = $"{N(run.Stage)} OF {N(run.Opponents.Count)} BEATEN";
            if (run.Champion) return $"ARCADE CHAMPIONS - {wins}";
            if (run.Lost) return $"RUN OVER - {wins}";
            string next = ScreenFlow.Clubs.FirstOrDefault(c => string.Equals(c.Id, run.NextOpponent, StringComparison.Ordinal))?.Name ?? run.NextOpponent ?? "";
            return $"{wins} - LIVES {N(run.LivesLeft)} - NEXT {next}";
        }
    }

    /// <summary>Gets the rule of a new run, or null to continue the saved one.</summary>
    protected abstract ArcadeRule? Rule { get; }

    /// <inheritdoc/>
    public MatchSetup? Setup(GameModeStart start)
    {
        // the first game of a new run starts it; every other game continues the save the last result went into
        if (Rule is { } rule && !begun) ArcadeSession.StartNew(start.Clubs, start.Picks[0].Id, rule);
        else ArcadeSession.Continue(start.Clubs);
        begun = true;
        return ArcadeSession.NextSetup(start.Device);
    }

    static string N(int n) => n.ToString(CultureInfo.InvariantCulture);
}

/// <summary>A new arcade run with three lives.</summary>
internal sealed class ArcadeLivesMode : ArcadeMode
{
    /// <inheritdoc/>
    public override string Title => "ARCADE - THREE LIVES";

    /// <inheritdoc/>
    protected override ArcadeRule? Rule => ArcadeRule.ThreeLives;
}

/// <summary>A new arcade run where the first loss ends it.</summary>
internal sealed class ArcadeSuddenDeathMode : ArcadeMode
{
    /// <inheritdoc/>
    public override string Title => "ARCADE - SUDDEN DEATH";

    /// <inheritdoc/>
    public override int Order => 21;

    /// <inheritdoc/>
    protected override ArcadeRule? Rule => ArcadeRule.SuddenDeath;
}

/// <summary>Continues the saved arcade run; shown only when there is one still going.</summary>
internal sealed class ArcadeContinueMode : ArcadeMode
{
    /// <inheritdoc/>
    public override string Title => "ARCADE - CONTINUE RUN";

    /// <inheritdoc/>
    public override int Order => 19;

    /// <inheritdoc/>
    public override int ClubPicks => 0;

    /// <inheritdoc/>
    public override bool Available => ArcadeSession.CanContinue();

    /// <inheritdoc/>
    protected override ArcadeRule? Rule => null;
}
