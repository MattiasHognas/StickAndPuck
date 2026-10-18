using StickAndPuck.App.Core;

namespace StickAndPuck.App.Lines.Dev
{
    /// <summary>Shared setup for the lines scenes.</summary>
    internal static class LinesScenes
    {
        /// <summary>Fast fatigue (unless SAP_FATIGUE says otherwise) and the panel on.</summary>
        public static void Demo(World world)
        {
            if (Env.Get("SAP_FATIGUE") is not { Length: > 0 })
            {
                world.Switches.FatiguePace = 6f;
            }

            world.Switches.LinesPanel = true;
        }
    }
}
