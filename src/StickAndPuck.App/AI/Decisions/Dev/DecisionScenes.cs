using System.Globalization;
using Raylib_cs;
using StickAndPuck.App.Core;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.AI.Decisions.Dev
{
    /// <summary>Helpers shared by the decision scenes and the self-test.</summary>
    internal static class DecisionScenes
    {
        /// <summary>Gives every skater of a club the same hockey IQ.</summary>
        public static void SetHockeyIq(World world, Side side, Maybe<int> smart)
        {
            if (!smart.TryGet(out int value))
            {
                return;
            }

            foreach (Player p in world.Players.ToList())
            {
                if (p.Side != side || p.IsGoalie)
                {
                    continue;
                }

                PlayerData data = p.Data with { Traits = p.Data.Traits with { HockeyIq = value } };
                world.Players[p.Id] = new Player
                {
                    Id = p.Id,
                    Side = p.Side,
                    Data = data,
                    Position = p.Position,
                    Heading = p.Heading,
                    OnIce = p.OnIce,
                    Slot = p.Slot,
                };
            }
        }

        /// <summary>Draws each club's passes, shots and checks.</summary>
        public static void DrawLine(World world)
        {
            if (!world.Switches.DecisionLine)
            {
                return;
            }

            DecisionTally h = DecisionState.Tally(world, Side.Home);
            DecisionTally a = DecisionState.Tally(world, Side.Away);
            string text =
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{world.Setup.Home.ShortName} PASS {h.Completed}/{h.Passes} SH {h.Shots} HIT {h.Checks}   "
                )
                + string.Create(
                    CultureInfo.InvariantCulture,
                    $"{world.Setup.Away.ShortName} PASS {a.Completed}/{a.Passes} SH {a.Shots} HIT {a.Checks}"
                );
            DrawText(text, 6, 258, 10, new Color(0xef, 0xe0, 0xc2, 255));
        }
    }
}
