using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season;

/// <summary>The arcade run being played, and the entry point for the start screen: show Continue when
/// <see cref="CanContinue()"/>, call <see cref="Continue(IReadOnlyList{ClubData})"/> or <see cref="StartNew(IReadOnlyList{ClubData}, string, ArcadeRule)"/>, then build the
/// world from <see cref="NextSetup"/>. <see cref="ArcadeSystem"/> records the result when that match
/// ends and saves the run at once; when <see cref="ArcadeRun.IsOver"/> the run's summary shows.</summary>
public sealed class ArcadeSession
{
    /// <summary>Gets the run being played, or None outside arcade mode.</summary>
    public Maybe<ArcadeRun> Current { get; private set; }

    /// <summary>Gets the setup of the arcade game being played, or None when the current match is not one.</summary>
    public Maybe<MatchSetup> Playing { get; private set; }

    /// <summary>Gets the clubs the run was loaded with.</summary>
    public IReadOnlyList<ClubData> Clubs { get; private set; } = [];

    /// <summary>Gets the save file this session writes.</summary>
    public string SavePath { get; private set; } = "";

    /// <summary>Gets a value indicating whether there is an unfinished run to continue at <see cref="ArcadeSave.DefaultPath"/>, for the start screen.</summary>
    public bool CanContinue() => CanContinue(ArcadeSave.DefaultPath);

    /// <summary>Gets a value indicating whether there is an unfinished run to continue, for the start screen.</summary>
    public bool CanContinue(string path)
    {
        return ArcadeSave.Read(path).Is(run => !run.IsOver);
    }

    /// <summary>Continues the run saved at <see cref="ArcadeSave.DefaultPath"/>; a finished run is loaded too, so its summary can show.</summary>
    public Maybe<ArcadeRun> Continue(IReadOnlyList<ClubData> clubs) => Continue(clubs, ArcadeSave.DefaultPath);

    /// <summary>Continues the saved run; a finished run is loaded too, so its summary can show.</summary>
    public Maybe<ArcadeRun> Continue(IReadOnlyList<ClubData> clubs, string path)
    {
        if (!ArcadeSave.Read(path).TryGet(out var run)) return Maybe<ArcadeRun>.None;
        Use(run, clubs, path);
        return run;
    }

    /// <summary>Starts a new run with a random opponent order at <see cref="ArcadeSave.DefaultPath"/>, replacing any arcade save, and saves it at once.</summary>
    public ArcadeRun StartNew(IReadOnlyList<ClubData> clubs, string userClub, ArcadeRule rule) => StartNew(clubs, userClub, rule, Maybe<int>.None, ArcadeSave.DefaultPath);

    /// <summary>Starts a new run at <see cref="ArcadeSave.DefaultPath"/>, replacing any arcade save, and saves it at once.</summary>
    public ArcadeRun StartNew(IReadOnlyList<ClubData> clubs, string userClub, ArcadeRule rule, Maybe<int> seed) => StartNew(clubs, userClub, rule, seed, ArcadeSave.DefaultPath);

    /// <summary>Starts a new run, replacing any arcade save, and saves it at once.</summary>
    public ArcadeRun StartNew(IReadOnlyList<ClubData> clubs, string userClub, ArcadeRule rule, Maybe<int> seed, string path)
    {
        var run = ArcadeRun.New(clubs, userClub, rule, seed.TryGet(out int s) ? s : Random.Shared.Next());
        // the best-runs board carries over; a run given up with games played goes on it as it stands
        if (ArcadeSave.Read(path).TryGet(out var old))
        {
            run.Records = old.Records;
            if (!old.IsOver && old.Games.Count > 0) run.Records = ArcadeRecord.Add(old.Records, ArcadeRecord.Of(old));
        }
        Use(run, clubs, path);
        ArcadeSave.Write(run, path);
        return run;
    }

    /// <summary>The setup for the human's next game, marked as the game being played. Over when the run is over.</summary>
    public NextMatch NextSetup(InputDevice device = InputDevice.KeyboardMouse)
    {
        var next = Current.TryGet(out var run) ? run.NextSetup(Clubs, device) : NextMatch.Over;
        Playing = next is NextMatch.Play play ? play.Setup : Maybe<MatchSetup>.None;
        return next;
    }

    /// <summary>Records the result of the game being played and saves the run. Ignored outside arcade mode.</summary>
    public Maybe<ArcadeGame> Finish(FixtureResult result)
    {
        if (!Current.TryGet(out var run) || !Playing.HasValue) return Maybe<ArcadeGame>.None;
        var game = run.Record(result);
        Playing = Maybe<MatchSetup>.None;
        if (run.IsOver)
        {
            var record = ArcadeRecord.Of(run);
            run.Records = ArcadeRecord.Add(run.Records, record);
            run.Place = ArcadeRecord.Place(run.Records, record);
        }
        ArcadeSave.Write(run, SavePath);
        return game;
    }

    /// <summary>Leaves arcade mode: nothing is recorded or saved after this.</summary>
    public void End()
    {
        Current = Maybe<ArcadeRun>.None;
        Playing = Maybe<MatchSetup>.None;
    }

    void Use(ArcadeRun run, IReadOnlyList<ClubData> clubs, string path)
    {
        Current = run;
        Clubs = clubs;
        SavePath = path;
        Playing = Maybe<MatchSetup>.None;
    }
}
