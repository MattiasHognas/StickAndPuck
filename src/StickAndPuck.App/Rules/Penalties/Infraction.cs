using StickAndPuck.App.Core;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Rules.Penalties
{
    /// <summary>The infractions the referee calls: tripping, hooking and slashing always, six more with a switch each, and checking from behind.</summary>
    public static class Infraction
    {
        /// <summary>Taking a skater's legs out.</summary>
        public const string Tripping = "tripping";

        /// <summary>Holding a carrier back with the stick.</summary>
        public const string Hooking = "hooking";

        /// <summary>Swinging the stick at a skater.</summary>
        public const string Slashing = "slashing";

        /// <summary>Hitting a skater who is nowhere near the puck.</summary>
        public const string Interference = "interference";

        /// <summary>Hanging on to a slowed carrier from behind.</summary>
        public const string Holding = "holding";

        /// <summary>The stick up at the face on a check; a double minor when it draws blood.</summary>
        public const string HighSticking = "high-sticking";

        /// <summary>A hard hit with both hands on the stick from the front; a major when very hard.</summary>
        public const string CrossChecking = "cross-checking";

        /// <summary>A hard hit from behind into the boards; a major when very hard.</summary>
        public const string Boarding = "boarding";

        /// <summary>A hit after the whistle, or the same skater hit again at once.</summary>
        public const string Roughing = "roughing";

        /// <summary>A hard hit from behind in open ice, a minor and misconduct or a major; called only with <see cref="PenaltySeverity.Full"/>.</summary>
        public const string CheckingFromBehind = "checking from behind";

        /// <summary>Gets an infraction's name as the penalty banner shows it, in the language shown.</summary>
        public static string Label(string infraction)
        {
            return Text.Get(
                infraction switch
                {
                    Tripping => K.InfTripping,
                    Hooking => K.InfHooking,
                    Slashing => K.InfSlashing,
                    Interference => K.InfInterference,
                    Holding => K.InfHolding,
                    HighSticking => K.InfHighSticking,
                    CrossChecking => K.InfCrossChecking,
                    Boarding => K.InfBoarding,
                    Roughing => K.InfRoughing,
                    CheckingFromBehind => K.InfCheckingFromBehind,
                    Fights.FightTuning.Infraction => K.InfFighting,
                    _ => K.InfPenalty,
                }
            );
        }

        /// <summary>Gets whether the rules let the referee call an infraction: six each have a switch, the rest are always called.</summary>
        public static bool Called(RuleSet rules, string infraction)
        {
            return infraction switch
            {
                Interference => rules.Interference,
                Holding => rules.Holding,
                HighSticking => rules.HighSticking,
                CrossChecking => rules.CrossChecking,
                Boarding => rules.Boarding,
                Roughing => rules.Roughing,
                CheckingFromBehind => rules.PenaltySeverity == PenaltySeverity.Full,
                _ => true,
            };
        }
    }
}
