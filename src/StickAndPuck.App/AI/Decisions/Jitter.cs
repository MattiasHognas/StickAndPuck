using StickAndPuck.App.Core;

namespace StickAndPuck.App.AI;

/// <summary>How one roll of a read or an execution came out.</summary>
public enum ReadOutcome
{
    /// <summary>The skater plays to their hockey IQ.</summary>
    Clean,

    /// <summary>The skater fumbles: this once they read and play like a much weaker player.</summary>
    Fumble,

    /// <summary>The skater gets lucky: this once they read and play like a star.</summary>
    Lucky,
}

/// <summary>The skill-scaled randomness every AI read and execution rolls. Hockey IQ only sets the
/// odds (D2's rule); each roll decides what happens this time, so the same situation is not always
/// played the same way. A star fumbles now and then and plays that look like a much weaker player;
/// a weak player now and then gets lucky and plays it like a star.</summary>
public static class Jitter
{
    /// <summary>Gets the odds a skater fumbles a read or an execution: 18 % at hockey IQ 0, still 4 % at 100.</summary>
    public static float FumbleOdds(int hockeyIq)
    {
        return Traits.Scale(hockeyIq, 0.18f, 0.04f);
    }

    /// <summary>Gets the odds a skater gets lucky: 15 % at hockey IQ 0, none at 100.</summary>
    public static float LuckOdds(int hockeyIq)
    {
        return Traits.Scale(hockeyIq, 0.15f, 0f);
    }

    /// <summary>Rolls one read or execution against the skater's odds.</summary>
    public static (ReadOutcome Outcome, int Iq) Roll(Random rng, int hockeyIq)
    {
        float u = rng.NextSingle();
        if (u < FumbleOdds(hockeyIq)) return (ReadOutcome.Fumble, Fumbled(hockeyIq));
        if (u > 1f - LuckOdds(hockeyIq)) return (ReadOutcome.Lucky, 100);
        return (ReadOutcome.Clean, hockeyIq);
    }

    /// <summary>Rolls and counts the roll in the side's tally.</summary>
    public static (ReadOutcome Outcome, int Iq) Roll(World world, Player p, Random rng)
    {
        var roll = Roll(rng, p.Data.Traits.HockeyIq);
        var tally = Decisions.Tally(world, p.Side);
        tally.Rolls++;
        switch (roll.Outcome)
        {
            case ReadOutcome.Fumble: tally.Fumbles++; break;
            case ReadOutcome.Lucky: tally.Lucky++; break;
            default: break;
        }
        if (p.Data.Traits.HockeyIq >= 95)
        {
            tally.StarRolls++;
            if (roll.Outcome == ReadOutcome.Fumble) tally.StarFumbles++;
        }
        return roll;
    }

    /// <summary>Gets the hockey IQ a fumbled read is played with: 60 % of the skater's own, so a 100 plays it like a 60.</summary>
    public static int Fumbled(int hockeyIq)
    {
        return Math.Clamp(hockeyIq * 3 / 5, 0, 100);
    }
}
