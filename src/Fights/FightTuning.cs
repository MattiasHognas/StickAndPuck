namespace StickAndPuck.Fights;

/// <summary>How often fights start and how a fight plays out. Times are real seconds.</summary>
public static class FightTuning
{
    /// <summary>The chance a hard check (<see cref="Players.CheckingModel.HardHit"/> and up) leads to gloves dropped and a fight offered.</summary>
    public const float OfferChance = 0.4f;

    /// <summary>How long a human has to accept an offered fight.</summary>
    public const float OfferSeconds = 2.5f;

    /// <summary>No new fight within this many seconds of the last one.</summary>
    public const float Cooldown = 30f;

    /// <summary>The two fighters stand this far from the spot between them.</summary>
    public const float HalfGap = 0.55f;

    /// <summary>Grabbing sweaters before the first exchange.</summary>
    public const float GrabSeconds = 0.8f;

    /// <summary>One exchange: the timing marker sweeps across the bar once.</summary>
    public const float ExchangeSeconds = 1.2f;

    /// <summary>The fight is broken up after this many exchanges without a knockdown; the fighter with less balance loses.</summary>
    public const int MaxExchanges = 6;

    /// <summary>Half the width of the sweet spot in the middle of the bar, as a fraction of the sweep: a punch thrown outside it misses.</summary>
    public const float SweetHalf = 0.16f;

    /// <summary>From throwing a punch to it landing: the wind-up the other fighter can see and block.</summary>
    public const float Windup = 0.22f;

    /// <summary>A block counts only if it went up at most this long before the punch landed; a guard held longer has dropped.</summary>
    public const float BlockFresh = 0.4f;

    /// <summary>Balance a clean, perfectly timed punch between average fighters takes; a fighter starts at 1 and is down at 0.</summary>
    public const float Damage = 0.36f;

    /// <summary>The winner celebrates and the loser lies (or stands) this long before the loser goes to the box.</summary>
    public const float EndSeconds = 1.8f;

    /// <summary>The infraction the box shows for the loser.</summary>
    public const string Infraction = "fighting";
}
