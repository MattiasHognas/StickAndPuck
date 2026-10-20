using System.Numerics;

namespace StickAndPuck.App.Season.Endings
{
    /// <summary>The toon shader's three tones and light direction for a moment of an ending.</summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    internal readonly record struct Tones(Vector3 Shadow, Vector3 Mid, Vector3 Full, Vector3 Light)
    {
        /// <summary>Gets the arena's afternoon light, the look the art was made in.</summary>
        public static Tones Arena =>
            new(
                Lighting.LightPresets.Afternoon.Shadow,
                Lighting.LightPresets.Afternoon.Mid,
                Vector3.One,
                Lighting.LightPresets.Afternoon.LightDir
            );

        /// <summary>Gets the evening light: low and warm from the side, plum shadows.</summary>
        public static Tones Late =>
            new(
                Lighting.LightPresets.Late.Shadow,
                Lighting.LightPresets.Late.Mid,
                Lighting.LightPresets.Late.Full,
                Lighting.LightPresets.Late.LightDir
            );

        /// <summary>The same tones turned down (or up) by a factor.</summary>
        public Tones Times(float k)
        {
            return this with { Shadow = Shadow * k, Mid = Mid * k, Full = Full * k };
        }
    }
}
