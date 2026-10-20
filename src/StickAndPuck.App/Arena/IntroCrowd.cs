using StickAndPuck.App.Core;

namespace StickAndPuck.App.Arena
{
    /// <summary>The crowd during the title intro: on its feet as the camera comes down from the rafters, the
    /// stands facing the camera cheering, the visitors' corner waving its own colours, and back in its seats
    /// once the start menu is up. Presentation only, on the menu backdrop's <see cref="World.Crowd"/>.
    /// <see cref="Rise"/> is the intro's big moment: the home tifo (W120) goes up with it, and comes down
    /// with <see cref="Settle"/>; the title intro's own staging of it is W142's.</summary>
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
            if (world.Settings.Supporters)
            {
                world.Crowd.Tifo.Raise(TifoMoments.StartSection, now); // the home tifo goes up as the camera comes down
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
