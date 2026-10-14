using StickAndPuck.App.Core;
using StickAndPuck.App.Season;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens;

/// <summary>The arcade entries on the "new game" list, over <see cref="ArcadeSession"/>: a new run
/// with three lives or sudden death (pick a club), or continue the saved run. Each game's result is
/// recorded and saved by the ArcadeSystem; the results screen offers the next game while the
/// run lasts.</summary>
internal abstract class ArcadeMode : IGameMode
{
    bool begun;

    /// <inheritdoc/>
    public abstract string Title { get; }

    /// <inheritdoc/>
    public virtual int Order => 20;

    /// <inheritdoc/>
    public bool HasNextGame(AppState app) => app.Arcade.Current.Is(r => !r.IsOver);

    /// <inheritdoc/>
    public virtual int ClubPicks => 1;

    /// <inheritdoc/>
    public virtual bool Available(AppState app) => true;

    /// <inheritdoc/>
    public Maybe<string> Status(AppState app)
    {
        if (!app.Arcade.Current.TryGet(out var run)) return Maybe<string>.None;
        string wins = Text.Format(K.ArcadeStatusWins, run.Stage, run.Opponents.Count);
        if (run.Champion) return Text.Format(K.ArcadeStatusChampions, wins);
        if (run.Lost) return Text.Format(K.ArcadeStatusOver, wins);
        string opponent = run.NextOpponent.Or("");
        string next = app.Screens.Clubs.FirstOrDefault(c => string.Equals(c.Id, opponent, StringComparison.Ordinal)) is { } club ? club.Name : opponent;
        return Text.Format(K.ArcadeStatusNext, wins, run.LivesLeft, next);
    }

    /// <summary>Gets the rule of a new run, or None to continue the saved one.</summary>
    protected abstract Maybe<ArcadeRule> Rule { get; }

    /// <inheritdoc/>
    public Maybe<MatchSetup> Setup(GameModeStart start)
    {
        // the first game of a new run starts it; every other game continues the save the last result went into
        if (Rule.TryGet(out var rule) && !begun) start.App.Arcade.StartNew(start.Clubs, start.Picks[0].Id, rule);
        else start.App.Arcade.Continue(start.Clubs);
        begun = true;
        return start.App.Arcade.NextSetup(start.Device) is NextMatch.Play play ? play.Setup : Maybe<MatchSetup>.None;
    }
}

/// <summary>A new arcade run with three lives.</summary>
internal sealed class ArcadeLivesMode : ArcadeMode
{
    /// <inheritdoc/>
    public override string Title => Text.Get(K.ArcadeThreeLives);

    /// <inheritdoc/>
    protected override Maybe<ArcadeRule> Rule => ArcadeRule.ThreeLives;
}

/// <summary>A new arcade run where the first loss ends it.</summary>
internal sealed class ArcadeSuddenDeathMode : ArcadeMode
{
    /// <inheritdoc/>
    public override string Title => Text.Get(K.ArcadeSuddenDeath);

    /// <inheritdoc/>
    public override int Order => 21;

    /// <inheritdoc/>
    protected override Maybe<ArcadeRule> Rule => ArcadeRule.SuddenDeath;
}

/// <summary>Continues the saved arcade run; shown only when there is one still going.</summary>
internal sealed class ArcadeContinueMode : ArcadeMode
{
    /// <inheritdoc/>
    public override string Title => Text.Get(K.ArcadeContinue);

    /// <inheritdoc/>
    public override int Order => 19;

    /// <inheritdoc/>
    public override int ClubPicks => 0;

    /// <inheritdoc/>
    public override bool Available(AppState app) => app.Arcade.CanContinue();

    /// <inheritdoc/>
    protected override Maybe<ArcadeRule> Rule => Maybe<ArcadeRule>.None;
}
