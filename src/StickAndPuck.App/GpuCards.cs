
// Stick & Puck: a pixel-art hockey game drawn in 3D. Everything renders to a 480x270 target
// that is scaled up by whole pixels, so the window size never changes the look.
// OpenGL picks its graphics card when the window is made, so a game that should run on another
// card than it started on starts itself again on that card first (see GpuLaunch).

namespace StickAndPuck.App.Core
{
    using System.Globalization;
    using System.Runtime.InteropServices;

    /// <summary>The graphics cards on this machine. Linux reads /sys/class/drm/card*/device and names the
    /// cards from the system pci.ids; Windows asks DXGI for its adapter list. The default card is the
    /// one the firmware booted on (boot_vga) on Linux and DXGI's first adapter on Windows.</summary>
    public static partial class GpuCards
    {
        /// <summary>The NVIDIA PCI vendor id.</summary>
        public const string Nvidia = "10de";

        static readonly Lazy<IReadOnlyList<GpuCard>> Found = new(Find);
        static string renderer = "";
        static bool asked;

        /// <summary>Gets every card found, the default one first.</summary>
        public static IReadOnlyList<GpuCard> All => Found.Value;

        /// <summary>Gets the GL_RENDERER of the running window, asked once (call it with the window open).</summary>
        public static string Renderer
        {
            get
            {
                if (asked) return renderer;
                asked = true;
                renderer = AskRenderer();
                Console.WriteLine($"SAP: gpu: GL_RENDERER {renderer}");
                return renderer;
            }
        }

        /// <summary>Finds the card a saved id or a SAP_GPU value means: an exact id, else a card whose name
        /// contains it ("amd", "nvidia"). "" and an unknown value mean the default card.</summary>
        public static GpuCard Resolve(IReadOnlyList<GpuCard> cards, string wanted)
        {
            string w = wanted.Trim();
            var exact = cards.Where(c => string.Equals(c.Id, w, StringComparison.OrdinalIgnoreCase)).ToList();
            var named = cards.Where(c => w.Length > 0 && c.Name.Contains(w, StringComparison.OrdinalIgnoreCase)).ToList();
            var pick = exact.Concat(named).Concat(cards.Where(c => c.IsDefault)).Concat(cards).ToList();
            return pick.Count > 0 ? pick[0] : new GpuCard("", "?", "", true);
        }

        /// <summary>Lists the cards under a DRM folder like /sys/class/drm, named from a pci.ids file.</summary>
        public static IReadOnlyList<GpuCard> FromSysfs(string drm, string pciIds)
        {
            if (!Directory.Exists(drm)) return [];
            string[] ids = File.Exists(pciIds) ? File.ReadAllLines(pciIds) : [];
            var cards = new List<GpuCard>();
            foreach (string card in Directory.GetDirectories(drm, "card*").Order(StringComparer.Ordinal))
            {
                string dev = Path.Combine(card, "device");
                if (!Path.GetFileName(card)[4..].All(char.IsAsciiDigit) || !File.Exists(Path.Combine(dev, "vendor"))) continue;    // card1-DP-2 is a connector
                string vendor = Hex(File.ReadAllText(Path.Combine(dev, "vendor"))), device = Hex(ReadOr(Path.Combine(dev, "device"), ""));
                string slot = ReadOr(Path.Combine(dev, "uevent"), "").Split('\n').Where(l => l.StartsWith("PCI_SLOT_NAME=", StringComparison.Ordinal))
                    .Select(l => l[14..].Trim()).DefaultIfEmpty(Path.GetFileName(card)).First();
                if (cards.Exists(c => string.Equals(c.Id, slot, StringComparison.Ordinal))) continue;
                cards.Add(new GpuCard(slot, NameOf(ids, vendor, device), vendor, string.Equals(ReadOr(Path.Combine(dev, "boot_vga"), "0").Trim(), "1", StringComparison.Ordinal)));
            }

            if (cards.Count > 0 && !cards.Exists(c => c.IsDefault)) cards[0] = cards[0] with { IsDefault = true };
            return [.. cards.OrderBy(c => !c.IsDefault)];
        }

        /// <summary>Names a card from pci.ids lines: "NVIDIA GeForce RTX 5070 Ti" from "GB203 [GeForce RTX 5070 Ti]",
        /// or "vendor:device" when the file does not know it.</summary>
        public static string NameOf(IReadOnlyList<string> pciIds, string vendor, string device)
        {
            string vendorName = "", deviceName = "";
            bool inVendor = false;
            foreach (string line in pciIds)
            {
                if (line.Length < 6 || line[0] == '#') continue;
                if (line[0] != '\t')
                {
                    if (inVendor) break;
                    inVendor = line.StartsWith(vendor + "  ", StringComparison.OrdinalIgnoreCase);
                    if (inVendor) vendorName = line[6..].Trim();
                }
                else if (inVendor && line[1] != '\t' && line.Length > 7 && line[1..].StartsWith(device + "  ", StringComparison.OrdinalIgnoreCase))
                {
                    deviceName = line[7..].Trim();
                }
            }

            if (deviceName.Length == 0) return $"{vendor}:{device}";
            string brand = vendor switch { Nvidia => "NVIDIA", "1002" => "AMD", "8086" => "Intel", _ => Bracketed(vendorName).Split(' ')[0].TrimEnd(',') };
            string model = Bracketed(deviceName);
            return model.StartsWith(brand, StringComparison.OrdinalIgnoreCase) ? model : $"{brand} {model}";
        }

        /// <summary>Tidies a GL_RENDERER for the pixel font: "AMD Ryzen 9 ... (radeonsi, ...)" loses the
        /// bracket and "NVIDIA GeForce RTX 5070 Ti/PCIe/SSE2" its slashes.</summary>
        public static string ShortRenderer(string glRenderer)
        {
            string s = glRenderer.Split(" (", StringSplitOptions.None)[0].Split('/')[0].Trim();
            return s.Length > 0 ? s : "?";
        }

        static string Bracketed(string name)
        {
            int open = name.LastIndexOf('['), close = name.LastIndexOf(']');
            return open >= 0 && close > open ? name[(open + 1)..close] : name;
        }

        static string Hex(string text) => text.Trim().Replace("0x", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();

        static string ReadOr(string path, string fallback) => File.Exists(path) ? File.ReadAllText(path) : fallback;

        static IReadOnlyList<GpuCard> Find()
        {
            if (OperatingSystem.IsBrowser()) return [];

            string pciIdsPath = File.Exists("/usr/share/hwdata/pci.ids") ? "/usr/share/hwdata/pci.ids" : "/usr/share/misc/pci.ids";
            var cards = OperatingSystem.IsWindows() ? FromDxgi() : FromSysfs("/sys/class/drm", pciIdsPath);
            Console.WriteLine($"SAP: gpu: {cards.Count} card(s): {string.Join(", ", cards.Select(c => $"{c.Name} [{c.Id}]{(c.IsDefault ? " default" : "")}"))}");
            return cards;
        }

        static string AskRenderer()
        {
            if (OperatingSystem.IsBrowser()) return "WebGL";
            try
            {
                nint p = OperatingSystem.IsWindows() ? WglGetString(0x1F01) : GlxGetString(0x1F01);
                return p == 0 ? "" : Marshal.PtrToStringUTF8(p) ?? "";
            }
            catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
            {
                return "";
            }
        }

        /// <summary>Windows: DXGI's adapters, skipping the software renderer. DXGI's first adapter is the default.</summary>
        static IReadOnlyList<GpuCard> FromDxgi()
        {
            var cards = new List<GpuCard>();
            Guid iid = new("770aae78-f26f-4dba-a829-253c83d1b387");    // IDXGIFactory1
            if (CreateDXGIFactory1(ref iid, out nint factory) < 0) return cards;

            try
            {
                nint vtable = Marshal.ReadIntPtr(factory);
                var enumAdapters1 = Marshal.GetDelegateForFunctionPointer<EnumAdapters1Delegate>(
                    Marshal.ReadIntPtr(vtable, 12 * IntPtr.Size));
                uint i = 0;
                while (true)
                {
                    if (enumAdapters1(factory, i, out nint adapter) < 0) break;    // EnumAdapters1
                    try
                    {
                        nint adapterVtable = Marshal.ReadIntPtr(adapter);
                        var getDesc1 = Marshal.GetDelegateForFunctionPointer<GetDesc1Delegate>(
                            Marshal.ReadIntPtr(adapterVtable, 10 * IntPtr.Size));
                        var desc = default(AdapterDesc1);
                        if (getDesc1(adapter, out desc) >= 0 && (desc.Flags & 2u) == 0)    // not software
                        {
                            string vendor = desc.VendorId.ToString("x4", CultureInfo.InvariantCulture);
                            cards.Add(new GpuCard($"{vendor}:{desc.DeviceId.ToString("x4", CultureInfo.InvariantCulture)}",
                                desc.Description.TrimEnd('\0'), vendor, cards.Count == 0));
                        }
                    }
                    finally
                    {
                        nint adapterVtable = Marshal.ReadIntPtr(adapter);
                        var releaseAdapter = Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(
                            Marshal.ReadIntPtr(adapterVtable, 2 * IntPtr.Size));
                        releaseAdapter(adapter);
                    }

                    i++;
                }
            }
            finally
            {
                nint vtable = Marshal.ReadIntPtr(factory);
                var releaseFactory = Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(
                    Marshal.ReadIntPtr(vtable, 2 * IntPtr.Size));
                releaseFactory(factory);
            }

            return cards.DistinctBy(c => c.Id, StringComparer.Ordinal).ToList();
        }

        [LibraryImport("libGL.so.1", EntryPoint = "glGetString")]
        private static partial nint GlxGetString(uint name);

        [LibraryImport("opengl32.dll", EntryPoint = "glGetString")]
        private static partial nint WglGetString(uint name);

        [LibraryImport("dxgi.dll")]
        private static partial int CreateDXGIFactory1(ref Guid riid, out nint factory);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int EnumAdapters1Delegate(nint factory, uint adapterIndex, out nint adapter);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetDesc1Delegate(nint adapter, out AdapterDesc1 desc);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint ReleaseDelegate(nint instance);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct AdapterDesc1
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string Description;
            public uint VendorId, DeviceId, SubSysId, Revision;
            public nuint DedicatedVideoMemory, DedicatedSystemMemory, SharedSystemMemory;
            public uint LuidLow;
            public int LuidHigh;
            public uint Flags;
            public int GraphicsPreemptionGranularity;
            public int ComputePreemptionGranularity;
        }
    }
}
