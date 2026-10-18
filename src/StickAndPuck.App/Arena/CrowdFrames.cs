namespace StickAndPuck.App.Arena
{
    /// <summary>The crowd atlas layout (art/crowd/crowd_256.png from art/scripts/crowd_atlas.py) and
    /// which frame a spectator shows. Pure maths: the renderer and the tests share it.</summary>
    internal static class CrowdFrames
    {
        /// <summary>The atlas is square, so it gets the palette-safe mipmap chain of <see cref="Camera.FarTexture"/>.</summary>
        public const int AtlasSize = 256;

        /// <summary>A cell's width in texels; 16 texels a metre.</summary>
        public const int CellWidth = 16;

        /// <summary>A cell's height in texels, the feet on its bottom row.</summary>
        public const int CellHeight = 32;

        /// <summary>A cell's width in metres in the stands.</summary>
        public const float Width = 1f;

        /// <summary>A cell's height in metres in the stands.</summary>
        public const float Height = 2f;

        /// <summary>The spectator looks: rows 0 to 3, in their own clothes.</summary>
        public const int Looks = 4;

        /// <summary>The first shirt-mask row: row ShirtRow + look is that look's shirt, white in the
        /// light and grey in shade, drawn over it in a club colour.</summary>
        public const int ShirtRow = 4;

        /// <summary>The atlas column a spectator shows: his pose's first or second frame.</summary>
        /// <param name="pose">His section's pose.</param>
        /// <param name="time">Seconds since the pose began.</param>
        /// <param name="phase">His own offset, 0 to 1, so a section does not move as one.</param>
        /// <returns>The column, 0 to 9.</returns>
        public static int Column(CrowdPose pose, float time, float phase)
        {
            bool second = pose switch
            {
                CrowdPose.Sit => Cycle(time, 7f, phase) < 0.18f, // now and then a hand to the chin
                CrowdPose.Stand => Cycle(time, 4f, phase) < 0.35f && Beat(time, 4f, phase), // a burst of claps
                CrowdPose.Cheer => Beat(time, 3f, phase),
                CrowdPose.Wave => Beat(time, 1.6f, phase),
                CrowdPose.Boo => Beat(time, 1.4f, phase),
                _ => false,
            };
            return ((int)pose * 2) + (second ? 1 : 0);
        }

        /// <summary>A stable mix of a seat's index, so every seat keeps its look, shirt and phase.</summary>
        /// <param name="seat">The seat's index.</param>
        /// <param name="salt">Which property.</param>
        /// <returns>A well mixed number.</returns>
        public static uint Hash(int seat, uint salt)
        {
            uint h = unchecked(((uint)seat * 0x9E3779B1u) ^ (salt * 0x85EBCA77u));
            h ^= h >> 15;
            h = unchecked(h * 0x2C1B3C6Du);
            h ^= h >> 12;
            h = unchecked(h * 0x297A2D39u);
            h ^= h >> 15;
            return h;
        }

        /// <summary>A seat's hash as 0 to 1.</summary>
        /// <param name="seat">The seat's index.</param>
        /// <param name="salt">Which property.</param>
        /// <returns>0 (inclusive) to 1 (exclusive).</returns>
        public static float Unit(int seat, uint salt)
        {
            return (Hash(seat, salt) >> 8) / 16777216f;
        }

        private static float Cycle(float time, float period, float phase)
        {
            float c = (time / period) + phase;
            return c - MathF.Floor(c);
        }

        private static bool Beat(float time, float hz, float phase)
        {
            return ((int)MathF.Floor((time * hz) + (phase * 2f)) & 1) == 1;
        }
    }
}
