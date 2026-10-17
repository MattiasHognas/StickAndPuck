using System.Globalization;
using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules;

namespace StickAndPuck.App.Fights;

/// <summary>Where a fight is.</summary>
public enum FightStage
{
    /// <summary>Gloves off, sweaters grabbed, before the first exchange.</summary>
    Grab,

    /// <summary>The timing marker sweeps; both fighters punch and block.</summary>
    Exchange,

    /// <summary>Decided: the winner celebrates before the loser goes to the box.</summary>
    Over,
}

/// <summary>A fight offered after a hard check, waiting for both sides to accept.</summary>
public sealed record FightOffer(int CheckerId, int VictimId, float Time)
{
    /// <summary>Gets the human slot deciding for each fighter (checker, victim), or -1 for the AI.</summary>
    public int[] Slots { get; } = [-1, -1];

    /// <summary>Gets whether each fighter has said yes (an AI decides at once).</summary>
    public bool[] Accepted { get; } = new bool[2];
}

/// <summary>One fighter's side of a fight.</summary>
public sealed class Fighter
{
    /// <summary>Initializes a new instance of the <see cref="Fighter"/> class.</summary>
    public Fighter(int playerId, int slot)
    {
        PlayerId = playerId;
        Slot = slot;
    }

    /// <summary>Gets the skater.</summary>
    public int PlayerId { get; }

    /// <summary>Gets the human slot playing the fight for him, or -1 for the AI.</summary>
    public int Slot { get; }

    /// <summary>Gets or sets the balance left: 1 at the start, down at 0.</summary>
    public float Balance { get; set; } = 1f;

    /// <summary>Gets or sets a value indicating whether he has thrown this exchange's punch.</summary>
    public bool Punched { get; set; }

    /// <summary>Gets or sets where the marker was when he threw it, 0 to 1.</summary>
    public float PunchAt { get; set; } = float.NaN;

    /// <summary>Gets or sets when his punch lands (<see cref="World.Time"/>), NaN when none is on its way.</summary>
    public float LandsAt { get; set; } = float.NaN;

    /// <summary>Gets or sets a value indicating whether his guard is up.</summary>
    public bool Blocking { get; set; }

    /// <summary>Gets or sets when his guard went up.</summary>
    public float BlockSince { get; set; } = float.NaN;

    /// <summary>Gets or sets where on the bar the AI throws this exchange's punch.</summary>
    public float AiPunchAt { get; set; } = float.NaN;

    /// <summary>Gets or sets when the AI raises its guard against a wind-up it saw, or NaN.</summary>
    public float AiBlockAt { get; set; } = float.NaN;

    /// <summary>Gets or sets punches that landed.</summary>
    public int Landed { get; set; }

    /// <summary>Gets or sets punches the other fighter blocked.</summary>
    public int Blocked { get; set; }

    /// <summary>Gets or sets punches thrown outside the sweet spot.</summary>
    public int Missed { get; set; }

    /// <summary>Gets or sets the clip the renderer shows.</summary>
    public string Clip { get; set; } = "grab";

    /// <summary>Gets or sets when that clip started.</summary>
    public float ClipSince { get; set; }
}

/// <summary>A fight on the ice.</summary>
public sealed class Fight
{
    /// <summary>Initializes a new instance of the <see cref="Fight"/> class.</summary>
    public Fight(Fighter checker, Fighter victim, Vector3 spot)
    {
        Checker = checker;
        Victim = victim;
        Spot = spot;
    }

    /// <summary>Gets the skater who threw the hit.</summary>
    public Fighter Checker { get; }

    /// <summary>Gets the skater who took it.</summary>
    public Fighter Victim { get; }

    /// <summary>Gets both fighters, checker first.</summary>
    public IEnumerable<Fighter> Both => [Checker, Victim];

    /// <summary>Gets the point between them.</summary>
    public Vector3 Spot { get; }

    /// <summary>Gets or sets the stage.</summary>
    public FightStage Stage { get; set; } = FightStage.Grab;

    /// <summary>Gets or sets the seconds since the stage (or exchange) started.</summary>
    public float StageTime { get; set; }

    /// <summary>Gets or sets when a punch last landed (<see cref="World.Time"/>), NaN before the first; the close-up camera jolts on it.</summary>
    public float LandedAt { get; set; } = float.NaN;

    /// <summary>Gets or sets the exchanges finished.</summary>
    public int Exchange { get; set; }

    /// <summary>Gets where the timing marker is in this exchange, 0 to 1.</summary>
    public float Sweep => Stage == FightStage.Exchange ? Math.Clamp(StageTime / FightTuning.ExchangeSeconds, 0f, 1f) : 0f;

    /// <summary>Gets or sets the loser once the fight is decided, or -1.</summary>
    public int LoserId { get; set; } = -1;

    /// <summary>Gets or sets a value indicating whether the loser went down (otherwise the linesmen broke it up).</summary>
    public bool Knockdown { get; set; }

    /// <summary>Gets the other fighter.</summary>
    public Fighter Other(Fighter f)
    {
        return f == Checker ? Victim : Checker;
    }

    /// <summary>Gets a skater's side of the fight, or None if he is not in it.</summary>
    public Maybe<Fighter> Of(int playerId)
    {
        return Checker.PlayerId == playerId ? Checker : Victim.PlayerId == playerId ? Victim : Maybe<Fighter>.None;
    }
}

/// <summary>Fights. A hard check (<see cref="Players.CheckingModel.HardHit"/> and up) may drop the gloves:
/// the checker and the skater he hit are offered a fight. An AI skater accepts or declines from his
/// checking trait and the score (<see cref="FightModel.AcceptChance"/>); a human accepts for his club's
/// skater with the accept button (F, or Y on a pad) inside <see cref="FightTuning.OfferSeconds"/>, or
/// lets it pass. When both say yes the referee whistles, the clock stops and the two square off.
/// The fight is a run of exchanges: a marker sweeps a bar, and a punch thrown inside the sweet spot in
/// the middle lands (harder the closer to the middle) unless the other fighter gets his guard up just
/// in time (inside <see cref="FightTuning.BlockFresh"/> of the punch landing). A fighter whose balance
/// runs out goes down; after <see cref="FightTuning.MaxExchanges"/> the linesmen step in and the one
/// with less balance loses. The penalty follows the rule set: the loser's two minutes (Arcade), or
/// coincidental 5 minute majors for both, with a game misconduct under SHL. Fights happen in regulation
/// only, never involve goalies, and never put a club three short.</summary>
public static class Fighting
{

    /// <summary>Gets the fight on the ice, or None.</summary>
    public static Maybe<Fight> Current(World world)
    {
        return world.Fight.TryGet(out var s) ? s.Fight : Maybe<Fight>.None;
    }

    /// <summary>Gets the fight waiting to be accepted, or None.</summary>
    public static Maybe<FightOffer> Pending(World world)
    {
        return world.Fight.TryGet(out var s) ? s.Offer : Maybe<FightOffer>.None;
    }

    /// <summary>Gets the fights started this match.</summary>
    public static int Started(World world)
    {
        return world.Fight.TryGet(out var s) ? s.Started : 0;
    }

    /// <summary>Gets whether a skater is in a fight right now.</summary>
    public static bool InFight(World world, int playerId)
    {
        return Current(world).Is(f => f.Of(playerId).HasValue);
    }

    /// <summary>Gets the clip a fighter shows: "grab", "punch", "block", "fall", "celebrate" or "idle",
    /// the seconds since it started, and whether it loops. None for a skater not in a fight.</summary>
    public static Maybe<(string Clip, float Seconds, bool Loop)> Pose(World world, Player p)
    {
        if (!Current(world).TryGet(out var fight) || !fight.Of(p.Id).TryGet(out var f)) return Maybe<(string Clip, float Seconds, bool Loop)>.None;
        bool loop = f.Clip is "grab" or "celebrate" or "idle";
        return (f.Clip, Math.Max(0, world.Time - f.ClipSince), loop);
    }

    /// <summary>Offers a fight between a checker and the skater he hit. Nothing happens if fights are off, play is
    /// not running in regulation, either is a goalie, off the ice, in the box or about to be, the loser's
    /// minor would put a club three short, or another fight or offer is on.</summary>
    public static bool Offer(World world, int checkerId, int victimId)
    {
        var s = State(world);
        if (s.Fight.HasValue || s.Offer.HasValue || !CanFight(world, checkerId, victimId)) return false;
        if (world.Time - s.LastFight < FightTuning.Cooldown) return false;
        var offer = new FightOffer(checkerId, victimId, world.Time);
        int[] ids = [checkerId, victimId];
        for (int i = 0; i < 2; i++)
        {
            var p = world.Players[ids[i]];
            offer.Slots[i] = HumanSlot(world, p.Side);
            if (offer.Slots[i] < 0) offer.Accepted[i] = s.ForceAccept.TryGet(out bool forced) ? forced : s.Rng.NextSingle() < FightModel.AcceptChance(world, p);
        }
        s.Offer = offer;
        s.Log(world, $"gloves off: {Penalties.Name(world, world.Players[checkerId])} and {Penalties.Name(world, world.Players[victimId])}");
        return true;
    }

    /// <summary>Gets whether two skaters may start a fight now.</summary>
    internal static bool CanFight(World world, int a, int b)
    {
        if (!world.Settings.Fights || !MatchFlow.IsRunning(world)) return false;
        var m = world.Match;
        if (m.Phase != MatchPhase.Play || m.Period > world.Setup.Periods) return false;
        if (a < 0 || b < 0 || a >= world.Players.Count || b >= world.Players.Count || a == b) return false;
        var pa = world.Players[a];
        var pb = world.Players[b];
        if (pa.Side == pb.Side) return false;
        foreach (var p in new[] { pa, pb })
        {
            if (p.IsGoalie || !p.OnIce || Penalties.InBox(world, p.Id) || Penalties.Delayed(world).Any(d => d.PlayerId == p.Id)) return false;
            if (ShortsClub(world) && Penalties.Serving(world, p.Side) + Penalties.Delayed(world).Count(d => d.Side == p.Side) >= PenaltyTuning.MaxServing) return false;
        }
        return true;
    }

    /// <summary>Gets whether the fight penalty can put a club three short: only the loser's minor, and only
    /// when a third penalty does not wait its turn. Coincidental majors are substituted and never do.</summary>
    static bool ShortsClub(World world)
    {
        var rules = world.Setup.Rules;
        return rules.FightPenalty == FightPenalty.LoserMinor && !rules.StackPenalties;
    }

    /// <summary>Gets the human slot playing for a club, or -1 when the AI plays it.</summary>
    internal static int HumanSlot(World world, Side side)
    {
        return world.Players.Find(p => p.Side == side && p.Slot >= 0)?.Slot ?? -1;
    }

    internal static FightState State(World world)
    {
        return world.Fight.Get(() => new FightState());
    }
}

/// <summary>The fight state of one match.</summary>
internal sealed class FightState
{
    public Maybe<Fight> Fight { get; set; }

    public Maybe<FightOffer> Offer { get; set; }

    /// <summary>Gets or sets the last fight that ended, kept for the result banner.</summary>
    public Maybe<Fight> Last { get; set; }

    public float LastEnded { get; set; } = float.NegativeInfinity;

    public float LastFight { get; set; } = float.NegativeInfinity;

    public int Started { get; set; }

    public int Declined { get; set; }

    /// <summary>Gets or sets the chance a hard check drops the gloves; scenes raise it.</summary>
    public float OfferChance { get; set; } = FightTuning.OfferChance;

    /// <summary>Gets or sets a fixed answer for AI fighters (scenes and tests), or None to roll.</summary>
    public Maybe<bool> ForceAccept { get; set; }

    /// <summary>Gets or sets whether the AI ever blocks (tests turn it off).</summary>
    public bool AiBlocks { get; set; } = true;

    /// <summary>Gets or sets whether the AI ever punches (tests turn it off).</summary>
    public bool AiPunches { get; set; } = true;

    /// <summary>Gets or sets the dice; scenes and tests seed them.</summary>
#pragma warning disable S2245 // gameplay dice, not security
    public Random Rng { get; set; } = new(15);
#pragma warning restore S2245

    /// <summary>Gets or sets when the fight scene offers its scripted fight (seconds of play), or NaN.</summary>
    public float ScriptAfterDrop { get; set; } = float.NaN;

    public float FirstDrop { get; set; } = float.NaN;

    public void Log(World world, string what)
    {
        if (!MatchFlow.State(world).Log) return;
        var m = world.Match;
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"FIGHT: {what}; P{m.Period} {MatchFlow.ClockText(m.Clock)}"));
    }
}
