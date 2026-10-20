using StickAndPuck.App.Core;

namespace StickAndPuck.App.Arena
{
    /// <summary>The crowd during the title intro: on its feet as the camera comes down from the rafters, the
    /// stands facing the camera cheering, the visitors' corner waving its own colours, and back in its seats
    /// once the start menu is up. Presentation only, on the menu backdrop's <see cref="World.Crowd"/>.
    /// The home tifo (W120) goes up a beat before, with <see cref="RaiseTifo"/>, so it is all the way up the
    /// far stand when the arena fades in and the camera's first shot is full of it (W142); it comes down with
    /// <see cref="Settle"/>.</summary>
    internal static class IntroCrowd
    {
        /// <summary>The intro's moment: the camera starts into the arena. Every home section stands, the far
        /// stands the camera arrives at cheer and the visitors' corner waves.</summary>
        public static void Rise(World world)
        {
            float now = world.Time;
            world.Crowd.SetAll(CrowdPose.Stand, now);
            world.Crowd.Set(CrowdSection.FarLeft, CrowdPose.Cheer, now);
            world.Crowd.Set(CrowdSection.FarCentre, CrowdPose.Cheer, now);
            world.Crowd.Set(CrowdSection.LeftEnd, CrowdPose.Wave, now);
            world.Crowd.Set(CrowdSection.RightEnd, CrowdPose.Wave, now);
            world.Crowd.Set(CrowdFans.AwaySection, CrowdPose.Wave, now);
            RaiseTifo(world); // already up unless the intro skipped ahead
        }

        /// <summary>The home end passes its tifo up the far centre stand, the one the camera starts on and comes
        /// down from; nothing when Flags and tifo is off.</summary>
        public static void RaiseTifo(World world)
        {
            if (world.Settings.Supporters && !world.Crowd.Tifo.Up)
            {
                world.Crowd.Tifo.Raise(TifoMoments.StartSection, world.Time);
            }
        }

        /// <summary>The menu is up: the crowd sits back down.</summary>
        public static void Settle(World world)
        {
            world.Crowd.SetAll(CrowdPose.Sit, world.Time);
            world.Crowd.Tifo.Lower(world.Time);
        }
    }
}
