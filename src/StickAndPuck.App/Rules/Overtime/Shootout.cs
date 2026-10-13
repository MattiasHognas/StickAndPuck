using StickAndPuck.App.Core;

namespace StickAndPuck.App.Rules;

/// <summary>One shootout attempt.</summary>
/// <param name="Side">The shooter's club.</param>
/// <param name="PlayerId">The shooter.</param>
/// <param name="Scored">Whether it went in.</param>
public readonly record struct ShootoutShot(Side Side, int PlayerId, bool Scored);

/// <summary>The shootout so far, for the HUD and the season: every attempt, who is shooting now and the winner once decided.</summary>
public sealed class ShootoutState
{
    readonly List<ShootoutShot> shots = [];

    /// <summary>Gets every attempt so far, in order.</summary>
    public IReadOnlyList<ShootoutShot> Shots => shots;

    /// <summary>Gets the player shooting now, or -1 between attempts.</summary>
    public int ShooterId { get; internal set; } = -1;

    /// <summary>Gets the club shooting now (or last).</summary>
    public Side Shooting { get; internal set; }

    /// <summary>Gets the shooters per club before sudden death, from the match's rules (W45).</summary>
    public int Rounds { get; internal set; } = 3;

    /// <summary>Gets the winner once the shootout is decided.</summary>
    public SideOrNone Winner { get; internal set; }

    /// <summary>Gets the goals a club has scored in the shootout.</summary>
    /// <param name="side">The club.</param>
    /// <returns>Goals.</returns>
    public int Goals(Side side)
    {
        return shots.Count(s => s.Side == side && s.Scored);
    }

    /// <summary>Gets the attempts a club has taken.</summary>
    /// <param name="side">The club.</param>
    /// <returns>Attempts.</returns>
    public int Taken(Side side)
    {
        return shots.Count(s => s.Side == side);
    }

    /// <summary>Gets the winner if the attempts so far decide it: one club out of reach within the rounds, or ahead after an even sudden death round.</summary>
    /// <param name="rounds">Shooters per club before sudden death.</param>
    /// <returns>The winner, or None while it goes on.</returns>
    public SideOrNone Decided(int rounds)
    {
        int hg = Goals(Side.Home), ag = Goals(Side.Away), ht = Taken(Side.Home), at = Taken(Side.Away);
        if (ht <= rounds && at <= rounds)
        {
            if (hg > ag + (rounds - at)) return SideOrNone.Home;
            if (ag > hg + (rounds - ht)) return SideOrNone.Away;
            return SideOrNone.None;
        }

        if (ht != at || hg == ag) return SideOrNone.None;
        return hg > ag ? SideOrNone.Home : SideOrNone.Away;
    }

    internal void Add(ShootoutShot shot)
    {
        shots.Add(shot);
    }
}
