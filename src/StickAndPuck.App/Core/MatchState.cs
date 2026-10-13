namespace StickAndPuck.App.Core;

/// <summary>Where the match is in its flow.</summary>
public enum MatchPhase
{
    /// <summary>Intro, before the first faceoff.</summary>
    Intro,

    /// <summary>Waiting for the puck drop.</summary>
    Faceoff,

    /// <summary>Puck in play, clock running.</summary>
    Play,

    /// <summary>Whistle: play stopped, clock stopped.</summary>
    Stopped,

    /// <summary>Goal scored: celebration and replay.</summary>
    Goal,

    /// <summary>Between periods.</summary>
    Intermission,

    /// <summary>3 on 3 overtime.</summary>
    Overtime,

    /// <summary>Shootout.</summary>
    Shootout,

    /// <summary>Match over.</summary>
    Final,
}

/// <summary>A penalty being served.</summary>
public sealed record PenaltyState(int PlayerId, Side Side, string Infraction, float SecondsLeft);

/// <summary>The score, clock and flow of the match. Rules own it; everything else reads it.</summary>
public sealed class MatchState
{
    /// <summary>Gets or sets the phase.</summary>
    public MatchPhase Phase { get; set; } = MatchPhase.Play;

    /// <summary>Gets or sets the period, from 1; overtime is period 4.</summary>
    public int Period { get; set; } = 1;

    /// <summary>Gets or sets the game seconds left in the period.</summary>
    public float Clock { get; set; }

    /// <summary>Gets the goals per side, indexed by <see cref="Side"/>.</summary>
    public int[] Score { get; } = new int[2];

    /// <summary>Gets the shots on goal per side, indexed by <see cref="Side"/>.</summary>
    public int[] Shots { get; } = new int[2];

    /// <summary>Gets the penalties being served.</summary>
    public List<PenaltyState> Penalties { get; } = [];

    /// <summary>Gets or sets a value indicating whether the game is paused (screens own this).</summary>
    public bool Paused { get; set; }

    /// <summary>Gets the side defending the -x end this period; they swap every period.</summary>
    public Side DefendsLeft => Period % 2 == 1 ? Side.Home : Side.Away;
}
