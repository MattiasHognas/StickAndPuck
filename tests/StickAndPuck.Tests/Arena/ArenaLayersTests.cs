using System.Text.Json;
using Raylib_cs;
using StickAndPuck.App.Arena;
using StickAndPuck.App.Core;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Arena
{
    /// <summary>
    /// The arena's league layers (A17): an SHL match keeps the 'SHL' centre mark and the dusk lines and
    /// hangs no banners; an NHL match swaps in the home crest, the NHL lines and the banners; the
    /// trapezoid follows its rule in either league; and arena.glb carries each layer as a part of its own.
    /// </summary>
    public sealed class ArenaLayersTests : CheckedTest
    {
        [Test]
        public void AnShlArenaIsTheArenaAsItWas()
        {
            ArenaLayers shl = ArenaLayers.For(League.Shl, trapezoid: false);
            Check(shl.ShlLogo && shl.Shows("Logo"), "the SHL mark shows");
            Check(!shl.HomeCrest, "no crest at centre ice");
            Check(!shl.NhlLines, "the lines keep their dusk colours");
            Check(!shl.Banners && !shl.Shows("Banners"), "no rafter banners");
            Check(!shl.Shows("Trapezoid"), "no trapezoid without the rule");
            Check(shl.Shows("Markings") && shl.Shows("Boards"), "every other part shows");
        }

        [Test]
        public void AnNhlArenaSwapsInItsLayers()
        {
            ArenaLayers nhl = ArenaLayers.For(League.Nhl, trapezoid: false);
            Check(!nhl.Shows("Logo"), "the SHL mark goes");
            Check(nhl.HomeCrest, "the home crest at centre ice");
            Check(nhl.NhlLines, "the NHL line colours");
            Check(nhl.Shows("Banners"), "the rafter banners hang");
            Check(!nhl.Shows("Trapezoid"), "no trapezoid without the rule");
        }

        [Test]
        public void TheTrapezoidFollowsItsRuleInEitherLeague()
        {
            Check(
                ArenaLayers.For(League.Shl, trapezoid: true).Shows("Trapezoid"),
                "SHL with the rule"
            );
            Check(
                ArenaLayers.For(League.Nhl, trapezoid: true).Shows("Trapezoid"),
                "NHL with the rule"
            );
        }

        [Test]
        public void TheCrestTakesTheIcesLight()
        {
            Color full = CentreIce.Tone(ArenaLook.DuskIce);
            Check(full.R == 255 && full.G == 255 && full.B == 255, "white under the dusk lights");
            Color dim = CentreIce.Tone(new Color(0x66, 0x64, 0x69, 255));
            Check(
                dim.R < 140 && dim.G < 140 && dim.B < 140,
                $"dimmed with the ice ({dim.R}, {dim.G}, {dim.B})"
            );
        }

        [Test]
        public void TheArenaCarriesEachLayerAsItsOwnPart()
        {
            using JsonDocument doc = JsonDocument.Parse(
                ResourceLoader.ReadText(Assets.ArenaMeshes)
            );
            List<(string Part, string Material)> meshes =
            [
                .. doc
                    .RootElement.GetProperty("meshes")
                    .EnumerateArray()
                    .Select(m =>
                        (
                            m.GetProperty("part").GetString() ?? "",
                            m.GetProperty("material").GetString() ?? ""
                        )
                    ),
            ];
            Check(
                meshes.Exists(m =>
                    string.Equals(m.Part, "Logo", StringComparison.Ordinal)
                    && string.Equals(m.Material, "blue_line", StringComparison.Ordinal)
                ),
                "the SHL mark is its own part"
            );
            Check(
                meshes.Exists(m =>
                    string.Equals(m.Part, "Trapezoid", StringComparison.Ordinal)
                    && string.Equals(m.Material, "trapezoid_line", StringComparison.Ordinal)
                ),
                "the trapezoid is its own part, in its own lighter red (W72)"
            );
            Check(
                meshes.Exists(m => string.Equals(m.Part, "Banners", StringComparison.Ordinal)),
                "the banners are their own part"
            );
        }
    }
}
