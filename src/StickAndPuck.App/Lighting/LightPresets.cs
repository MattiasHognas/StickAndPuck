using System.Numerics;
using Raylib_cs;

namespace StickAndPuck.App.Lighting
{
    /// <summary>The three presets, one per start time.</summary>
    public static class LightPresets
    {
        /// <summary>The ice in daylight and at dusk: the palette's pale cold grey (#ccc9d2), what the dusk render's ice snaps to.</summary>
        public static readonly Color BrightIce = new(0xcc, 0xc9, 0xd2, 0xff);

        /// <summary>The ice under the evening lights: one palette step darker (#bdb9c4), still cold.</summary>
        public static readonly Color DimIce = new(0xbd, 0xb9, 0xc4, 0xff);

        /// <summary>Early: cool, high and bright, pale bands, a light glass tint.</summary>
        public static readonly LightPreset Early = new(
            TimeOfDay.Early,
            ["13:00", "14:00", "15:15"],
            new Vector3(-0.2f, 0.95f, 0.3f),
            Shadow: new Vector3(0.62f, 0.66f, 0.76f),
            Mid: new Vector3(0.92f, 0.95f, 1.02f),
            Full: new Vector3(1.12f, 1.12f, 1.16f),
            IceBand: new Color(0xef, 0xe0, 0xc2, 0xff), // #efe0c2 cream
            Glass: new Color(0x9e, 0x65, 0x5c, 70), // #9e655c, thin
            Ice: BrightIce,
            Scratch: new Color(0xe3, 0xd8, 0xc6, 150)
        ); // #e3d8c6, faint

        /// <summary>Afternoon: the dusk look the art was made in; the shader's own defaults.</summary>
        public static readonly LightPreset Afternoon = new(
            TimeOfDay.Afternoon,
            ["16:30", "17:00"],
            new Vector3(-0.45f, 0.75f, 0.5f),
            Shadow: new Vector3(0.42f, 0.40f, 0.48f),
            Mid: new Vector3(0.70f, 0.68f, 0.72f),
            Full: Vector3.One,
            IceBand: new Color(0xe3, 0xd8, 0xc6, 0xff), // #e3d8c6, ArenaLook.DuskIceBand
            Glass: new Color(0x7a, 0x3e, 0x35, 110), // ArenaLook.DuskGlass
            Ice: BrightIce,
            Scratch: new Color(0xe3, 0xd8, 0xc6, 150)
        ); // #e3d8c6, faint

        /// <summary>Late: low warm light from the side, dark plum shadows, gold bands, heavy glass.</summary>
        public static readonly LightPreset Late = new(
            TimeOfDay.Late,
            ["19:00", "19:30"],
            new Vector3(-0.75f, 0.45f, 0.5f),
            Shadow: new Vector3(0.30f, 0.24f, 0.36f),
            Mid: new Vector3(0.60f, 0.50f, 0.58f),
            Full: new Vector3(1.0f, 0.90f, 0.86f),
            IceBand: new Color(0xf0, 0xb4, 0x8a, 0xff), // #f0b48a peach
            Glass: new Color(0x61, 0x3b, 0x3a, 160), // #613b3a, thick
            Ice: DimIce,
            Scratch: new Color(0xcc, 0xc9, 0xd2, 150)
        ); // #ccc9d2, the bright ice grey, faint

        /// <summary>Gets the preset for a start time.</summary>
        public static LightPreset For(TimeOfDay time)
        {
            return time switch
            {
                TimeOfDay.Early => Early,
                TimeOfDay.Late => Late,
                TimeOfDay.Afternoon => Afternoon,
                _ => Afternoon,
            };
        }
    }
}
