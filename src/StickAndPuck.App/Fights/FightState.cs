using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match;

namespace StickAndPuck.App.Fights
{
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
        public Random Rng { get; set; } = Seeds.Dice(15);

        /// <summary>Gets or sets when the fight scene offers its scripted fight (seconds of play), or NaN.</summary>
        public float ScriptAfterDrop { get; set; } = float.NaN;

        public float FirstDrop { get; set; } = float.NaN;

        public static void Log(World world, string what)
        {
            if (!MatchFlow.State(world).Log)
            {
                return;
            }

            MatchState m = world.Match;
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"FIGHT: {what}; P{m.Period} {MatchFlow.ClockText(m.Clock)}"
                )
            );
        }
    }
}
