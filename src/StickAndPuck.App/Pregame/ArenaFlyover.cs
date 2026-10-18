using StickAndPuck.App.Core;

namespace StickAndPuck.App.Pregame
{
    /// <summary>The camera flyover of the arena at the start of the warm-up. Its timeline comes from the
    /// warm-up's own clock, so it is the same in every run and on replay; the camera only reads it,
    /// and nothing in the simulation depends on it. The first start press while it plays skips it
    /// (<see cref="Skip"/>); the next one starts the game. Anything drawn over the flyover (such as a
    /// stats card) can follow <see cref="Phase"/> and <see cref="Progress"/>.</summary>
    public static class ArenaFlyover
    {
        /// <summary>How long the high sweep over the arena lasts, in seconds.</summary>
        public const float AerialSeconds = 4f;

        /// <summary>How long the close-up on the home squad lasts, in seconds.</summary>
        public const float CloseUpSeconds = 3.5f;

        /// <summary>How long the camera takes to settle to the broadcast view, in seconds.</summary>
        public const float SettleSeconds = 1.5f;

        /// <summary>The whole flyover, in seconds.</summary>
        public const float Seconds = AerialSeconds + CloseUpSeconds + SettleSeconds;

        /// <summary>Gets a value indicating whether SAP_FLYOVER=0 turned the flyover off (the warm-up keeps its plain close-up).</summary>
        public static bool Disabled => Env.Is("SAP_FLYOVER", "0");

        /// <summary>Gets where the flyover is in a world right now.</summary>
        public static FlyoverPhase Phase(World world)
        {
            if (Disabled || !PregameFlow.IsWarmingUp(world))
            {
                return FlyoverPhase.Off;
            }

            float t = PregameFlow.Elapsed(world);
            return PregameFlow.State(world).FlyoverSkipped || t >= Seconds ? FlyoverPhase.Done
                : t < AerialSeconds ? FlyoverPhase.Aerial
                : t < AerialSeconds + CloseUpSeconds ? FlyoverPhase.CloseUp
                : FlyoverPhase.Settle;
        }

        /// <summary>Gets the seconds the flyover has played: the warm-up's elapsed time, up to <see cref="Seconds"/>.</summary>
        public static float Elapsed(World world)
        {
            return Math.Min(PregameFlow.Elapsed(world), Seconds);
        }

        /// <summary>Gets how far the flyover has come, 0 to 1; 1 once it is done or skipped, 0 when it is off.</summary>
        public static float Progress(World world)
        {
            return Phase(world) switch
            {
                FlyoverPhase.Off => 0,
                FlyoverPhase.Done => 1,
                FlyoverPhase.Aerial or FlyoverPhase.CloseUp or FlyoverPhase.Settle => Elapsed(world)
                    / Seconds,
                _ => throw new NotSupportedException(
                    $"No progress for flyover phase {Phase(world)}"
                ),
            };
        }

        /// <summary>Gets a value indicating whether the flyover is still playing (not off, not done).</summary>
        public static bool IsPlaying(World world)
        {
            return Phase(world) is not (FlyoverPhase.Off or FlyoverPhase.Done);
        }

        /// <summary>Skips the flyover: the camera goes to the broadcast view and the warm-up goes on. Returns false when no flyover was playing.</summary>
        public static bool Skip(World world)
        {
            if (!IsPlaying(world))
            {
                return false;
            }

            PregameFlow.State(world).FlyoverSkipped = true;
            Console.WriteLine("SAP: arena flyover skipped");
            return true;
        }
    }
}
