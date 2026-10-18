namespace StickAndPuck.App.Render
{
    /// <summary>How coarse the scene is drawn. Far from the players (the gameplay camera) a kit texel is
    /// smaller than a pixel of the 480x270 frame, so the full frame is right. Up close (the goal replay,
    /// the fight close-up) one texel covers two or three pixels and the kits turn into soft blocks of
    /// uneven size; drawing the scene at 240x135 or 160x90 instead keeps a texel about one pixel, and
    /// the outline, drawn on that smaller frame, stays one of its pixels wide. The camera asks for a
    /// scale each step; the pixel frame picks it up when the next frame starts, and the HUD always stays
    /// at the full 480x270.</summary>
    public sealed class RenderScale
    {
        /// <summary>The coarsest scale: 160x90.</summary>
        public const int MaxDivisor = 3;

        /// <summary>How far one kit texel stretches on a player, in metres (a 64x64 atlas on a model drawn 1.15 times its size).</summary>
        public const float TexelMetres = 0.045f;

        /// <summary>How far the texel-per-pixel ratio must pass the halfway point before the scale changes, so a camera easing in does not flicker between two scales.</summary>
        private const float Hold = 0.15f;

        /// <summary>Gets the scale the camera asked for: 1 draws 480x270, 2 draws 240x135, 3 draws 160x90.</summary>
        public int Divisor { get; private set; } = 1;

        /// <summary>Asks for a scale from the next frame on.</summary>
        public void Request(int divisor)
        {
            Divisor = Math.Clamp(divisor, 1, MaxDivisor);
        }

        /// <summary>The scale that keeps a kit texel about one pixel, for a camera whose full-frame pixel is this big at the players.</summary>
        /// <param name="pixelMetres">One pixel of the 480x270 frame at the players' distance, in metres.</param>
        /// <param name="current">The scale used now; it is kept until the ratio is clearly nearer another one.</param>
        public static int ForPixel(float pixelMetres, int current)
        {
            if (pixelMetres <= 0)
            {
                return MaxDivisor;
            }

            float texels = TexelMetres / pixelMetres;
            return MathF.Abs(texels - current) < 0.5f + Hold
                ? Math.Clamp(current, 1, MaxDivisor)
                : Math.Clamp((int)MathF.Round(texels, MidpointRounding.ToEven), 1, MaxDivisor);
        }
    }
}
