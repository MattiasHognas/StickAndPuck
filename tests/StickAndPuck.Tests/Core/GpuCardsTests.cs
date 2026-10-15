using StickAndPuck.App.Core;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests;

/// <summary>
/// The graphics card list: names from pci.ids lines, the cards under a made-up /sys/class/drm (connectors
/// skipped, the boot_vga card the default and first), picking a card by id or name, the GL_RENDERER
/// tidied for the font, and SAP_GPU laid over the saved card.
/// </summary>
public sealed class GpuCardsTests : CheckedTest
{
    static readonly string[] PciIds =
    [
        "# comment",
        "1002  Advanced Micro Devices, Inc. [AMD/ATI]",
        "\t13c0  Granite Ridge [Radeon 610M]",
        "\t\t1002 13c0  Some subsystem",
        "10de  NVIDIA Corporation",
        "\t13c0  GM204 [GeForce GTX 980]",
        "\t2c05  GB203 [GeForce RTX 5070 Ti]",
        "1234  Some Vendor, Inc.",
        "\t0001  Plain Card",
    ];

    [Test]
    public void NamesComeFromPciIds()
    {
        Check(Is(GpuCards.NameOf(PciIds, "10de", "2c05"), "NVIDIA GeForce RTX 5070 Ti"), GpuCards.NameOf(PciIds, "10de", "2c05"));
        Check(Is(GpuCards.NameOf(PciIds, "1002", "13c0"), "AMD Radeon 610M"), GpuCards.NameOf(PciIds, "1002", "13c0"));
        Check(Is(GpuCards.NameOf(PciIds, "1234", "0001"), "Some Plain Card"), GpuCards.NameOf(PciIds, "1234", "0001"));
        Check(Is(GpuCards.NameOf(PciIds, "10de", "ffff"), "10de:ffff"), "an unknown device shows its ids");
    }

    [Test]
    public void SysfsListsEachCardOnceDefaultFirst()
    {
        string drm = Path.Combine(Path.GetTempPath(), $"sap-drm-{Guid.NewGuid():N}");
        try
        {
            Card(drm, "card0", "0x1002", "0x13c0", "0000:79:00.0", boot: false);
            Card(drm, "card1", "0x10de", "0x2c05", "0000:01:00.0", boot: true);
            Directory.CreateDirectory(Path.Combine(drm, "card1-DP-1"));    // a connector, not a card
            string ids = Path.Combine(drm, "pci.ids");
            File.WriteAllLines(ids, PciIds);

            var cards = GpuCards.FromSysfs(drm, ids);
            Check(cards.Count == 2, $"two cards ({cards.Count})");
            Check(Is(cards[0].Id, "0000:01:00.0") && cards[0].IsDefault && Is(cards[0].Vendor, "10de"), $"the boot_vga card first and default ({cards[0]})");
            Check(Is(cards[1].Name, "AMD Radeon 610M") && !cards[1].IsDefault, $"then the other ({cards[1]})");
            Check(Is(GpuCards.Resolve(cards, "").Id, "0000:01:00.0"), "\"\" picks the default card");
            Check(Is(GpuCards.Resolve(cards, "amd").Id, "0000:79:00.0"), "a part of the name picks that card");
            Check(Is(GpuCards.Resolve(cards, "0000:79:00.0").Name, "AMD Radeon 610M"), "the id picks that card");
            Check(GpuCards.Resolve(cards, "nonsense").IsDefault, "an unknown card falls back to the default");
        }
        finally
        {
            Directory.Delete(drm, recursive: true);
        }
    }

    [Test]
    public void RendererIsTidiedForTheFont()
    {
        Check(Is(GpuCards.ShortRenderer("NVIDIA GeForce RTX 5070 Ti/PCIe/SSE2"), "NVIDIA GeForce RTX 5070 Ti"), "slashes go");
        Check(Is(GpuCards.ShortRenderer("AMD Ryzen 9 9950X3D 16-Core Processor (radeonsi, raphael_mendocino, ACO)"), "AMD Ryzen 9 9950X3D 16-Core Processor"), "the driver bracket goes");
        Check(Is(GpuCards.ShortRenderer(""), "?"), "no renderer shows ?");
    }

    [Test]
    public void SapGpuOverridesTheSavedCard()
    {
        Env.Set("SAP_GPU", "amd");
        try
        {
            Check(Is(Settings.FromEnvironment(new Settings { Gpu = "0000:01:00.0" }).Gpu, "amd"), "SAP_GPU wins over the saved card");
        }
        finally
        {
            Env.Set("SAP_GPU", "");
        }

        Check(Is(Settings.FromEnvironment(new Settings { Gpu = "0000:01:00.0" }).Gpu, "0000:01:00.0"), "without SAP_GPU the saved card stays");
    }

    static bool Is(string a, string b) => string.Equals(a, b, StringComparison.Ordinal);

    static void Card(string drm, string name, string vendor, string device, string slot, bool boot)
    {
        string dev = Path.Combine(drm, name, "device");
        Directory.CreateDirectory(dev);
        File.WriteAllText(Path.Combine(dev, "vendor"), vendor + "\n");
        File.WriteAllText(Path.Combine(dev, "device"), device + "\n");
        File.WriteAllText(Path.Combine(dev, "uevent"), $"DRIVER=x\nPCI_SLOT_NAME={slot}\n");
        File.WriteAllText(Path.Combine(dev, "boot_vga"), boot ? "1\n" : "0\n");
    }
}
