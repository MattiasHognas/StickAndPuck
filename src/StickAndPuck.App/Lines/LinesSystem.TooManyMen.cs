using System.Globalization;
using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Penalties;

namespace StickAndPuck.App.Lines
{
    internal sealed partial class LinesSystem
    {
        /// <summary>Too many men on the ice (<see cref="RuleSet.TooManyMen"/>): in a rushed change, the skater who stepped
        /// out early or the one he replaces plays the puck while the one going off is still further from the
        /// bench door than the rule allows (<see cref="LinesTuning.TooManyMenLenient"/>, or
        /// <see cref="LinesTuning.TooManyMenStrict"/>). The referee signals a bench minor on the early skater
        /// (<see cref="BenchMinor"/>), called once per change. Only a human rushes a change, so the AI never draws it.</summary>
        private static void TooManyMen(World world, ShiftChange change)
        {
            TooManyMenCall rule = world.Setup.Rules.TooManyMen;
            int carrier = world.Puck.Carrier;
            if (!change.Rushed || change.Called || rule == TooManyMenCall.Off || carrier < 0)
            {
                return;
            }

            float reach =
                rule == TooManyMenCall.Strict
                    ? LinesTuning.TooManyMenStrict
                    : LinesTuning.TooManyMenLenient;
            foreach (Swap s in change.Swaps.Where(s => s.Early && !s.Done && s.Out.OnIce))
            {
                if (
                    (carrier != s.In.Id && carrier != s.Out.Id)
                    || Vector2.Distance(Flat(s.Out), s.Door) <= reach
                )
                {
                    continue;
                }

                change.Called = BenchMinor.Signal(world, s.In.Id, Infraction.TooManyMen);
                if (change.Called)
                {
                    Log(
                        world,
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"{world.Setup.Club(change.Side).ShortName} too many men: #{s.In.Data.Number} {s.In.Data.LastName} on, #{s.Out.Data.Number} {s.Out.Data.LastName} still {Vector2.Distance(Flat(s.Out), s.Door):0.0} m out"
                        )
                    );
                }

                return;
            }
        }
    }
}
