
// Stick & Puck: a pixel-art hockey game drawn in 3D. Everything renders to a 480x270 target
// that is scaled up by whole pixels, so the window size never changes the look.
// OpenGL picks its graphics card when the window is made, so a game that should run on another
// card than it started on starts itself again on that card first (see GpuLaunch).
StickAndPuck.App.Core.AssetRoot.Enter();
if (!OperatingSystem.IsBrowser() && !StickAndPuck.App.Core.GpuLaunch.OntoWanted())
{
    StickAndPuck.App.Core.Game.Run();
    if (!OperatingSystem.IsBrowser())
    {
        StickAndPuck.App.Core.GpuLaunch.AfterRun();
    }
}

namespace StickAndPuck.App.Core
{
    using System.Diagnostics;
    using System.Globalization;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using StickAndPuck.App.Screens;

    /// <summary>A graphics card found on the machine.</summary>
    /// <param name="Id">The stable id saved in <see cref="Settings.Gpu"/>: the PCI address on Linux, the adapter's vendor:device on Windows.</param>
    /// <param name="Name">The name to show, like "NVIDIA GeForce RTX 5070 Ti".</param>
    /// <param name="Vendor">The PCI vendor id in lower-case hex, like "10de".</param>
    /// <param name="IsDefault">Whether the machine draws on this card when nothing says otherwise.</param>
    public sealed record GpuCard(string Id, string Name, string Vendor, bool IsDefault);

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
            var cards = OperatingSystem.IsWindows() ? FromDxgi()
                : FromSysfs("/sys/class/drm", File.Exists("/usr/share/hwdata/pci.ids") ? "/usr/share/hwdata/pci.ids" : "/usr/share/misc/pci.ids");
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
        static unsafe IReadOnlyList<GpuCard> FromDxgi()
        {
            var cards = new List<GpuCard>();
            Guid iid = new("770aae78-f26f-4dba-a829-253c83d1b387");    // IDXGIFactory1
            if (CreateDXGIFactory1(&iid, out nint factory) < 0) return cards;
            void** vt = *(void***)factory;
            for (uint i = 0; ; i++)
            {
                nint adapter;
                if (((delegate* unmanaged[Stdcall]<nint, uint, nint*, int>)vt[12])(factory, i, &adapter) < 0) break;    // EnumAdapters1
                AdapterDesc desc;
                void** avt = *(void***)adapter;
                if (((delegate* unmanaged[Stdcall]<nint, AdapterDesc*, int>)avt[10])(adapter, &desc) >= 0 && (desc.Flags & 2) == 0)    // GetDesc1, not software
                {
                    string vendor = desc.VendorId.ToString("x4", CultureInfo.InvariantCulture);
                    cards.Add(new GpuCard($"{vendor}:{desc.DeviceId.ToString("x4", CultureInfo.InvariantCulture)}", new string(desc.Description).TrimEnd('\0'), vendor, cards.Count == 0));
                }
                ((delegate* unmanaged[Stdcall]<nint, uint>)avt[2])(adapter);    // Release
            }

            ((delegate* unmanaged[Stdcall]<nint, uint>)vt[2])(factory);
            return cards.DistinctBy(c => c.Id).ToList();
        }

        [LibraryImport("libGL.so.1", EntryPoint = "glGetString")]
        private static partial nint GlxGetString(uint name);

        [LibraryImport("opengl32.dll", EntryPoint = "glGetString")]
        private static partial nint WglGetString(uint name);

        [LibraryImport("dxgi.dll")]
        private static unsafe partial int CreateDXGIFactory1(Guid* riid, out nint factory);

        [StructLayout(LayoutKind.Sequential)]
        unsafe struct AdapterDesc
        {
            public fixed char Description[128];
            public uint VendorId, DeviceId, SubSysId, Revision;
            public nuint DedicatedVideoMemory, DedicatedSystemMemory, SharedSystemMemory;
            public uint LuidLow;
            public int LuidHigh;
            public uint Flags;
        }
    }

    /// <summary>Starts the game on the card the player picked (<see cref="Settings.Gpu"/>, or SAP_GPU). On
    /// Linux the game starts itself again with the card's variables: PRIME render offload for an NVIDIA
    /// card, DRI_PRIME and Mesa's GL for any other; the default card runs with neither. SAP_GPU_LAUNCHED
    /// marks the started game with its card, so it never starts itself twice. A game started on a newly
    /// picked card opens on the settings screen and asks to keep it; SAP_GPU_REVERT names the card to go
    /// back to when nobody answers (a card that cannot show its window leaves the screen black). On Windows OpenGL follows the
    /// system's per-app graphics preference (a managed exe cannot export NvOptimusEnablement or
    /// AmdPowerXpressRequestHighPerformance), so the game lists DXGI's cards and runs where Windows puts it.</summary>
    public static class GpuLaunch
    {
        const string Marker = "SAP_GPU_LAUNCHED";
        const string RevertTo = "SAP_GPU_REVERT";

        static readonly string[] CardVariables = ["__NV_PRIME_RENDER_OFFLOAD", "__GLX_VENDOR_LIBRARY_NAME", "__VK_LAYER_NV_optimus", "DRI_PRIME", "__EGL_VENDOR_LIBRARY_FILENAMES"];

        static Maybe<GpuCard> pending = Maybe<GpuCard>.None;
        static bool ask;

        /// <summary>Gets the card this game draws on.</summary>
        public static GpuCard Running => GpuCards.Resolve(GpuCards.All, Env.Get(Marker));

        /// <summary>Gets the card to go back to while this game, started on a newly picked card, waits to be kept; None once kept.</summary>
        public static Maybe<GpuCard> Unconfirmed =>
            Env.Get(RevertTo) is { Length: > 0 } back ? GpuCards.Resolve(GpuCards.All, back) : Maybe<GpuCard>.None;

        /// <summary>Gets a value indicating whether picking a card starts the game again on it (Linux only).</summary>
        public static bool CanSwitch => OperatingSystem.IsLinux() && GpuCards.All.Count > 1;

        /// <summary>Starts the game again on the wanted card when this one runs elsewhere.</summary>
        /// <returns>True when that game ran (and has ended) instead of this one.</returns>
        public static bool OntoWanted()
        {
            if (OperatingSystem.IsBrowser())
                return false;
            if (!CanSwitch) return false;
            var wanted = GpuCards.Resolve(GpuCards.All, Settings.FromEnvironment(ScreenPrefsFile.Read().Settings).Gpu);
            if (string.Equals(wanted.Id, Running.Id, StringComparison.Ordinal)) return false;
            Relaunch(wanted, Env.Get("SAP_GPU"), "");
            return true;
        }

        /// <summary>Keeps the card this game runs on: the keep question is answered.</summary>
        public static void Keep() => Env.Set(RevertTo, "");

        /// <summary>Asks for the game to start again on a card once it ends; false when it runs there already.</summary>
        /// <param name="gpu">The card, as <see cref="Settings.Gpu"/> names it.</param>
        /// <param name="confirm">Whether the started game asks to keep the card, going back to this one otherwise.</param>
        public static bool Request(string gpu, bool confirm)
        {
            ask = confirm;
            var card = GpuCards.Resolve(GpuCards.All, gpu);
            pending = CanSwitch && !string.Equals(card.Id, Running.Id, StringComparison.Ordinal) ? card : Maybe<GpuCard>.None;
            return pending.HasValue;
        }

        /// <summary>Starts the game again on the card <see cref="Request"/> asked for, if any.</summary>
        [UnsupportedOSPlatform("browser")]
        public static void AfterRun()
        {
            if (pending.TryGet(out var card)) Relaunch(card, card.IsDefault ? "" : card.Id, ask ? Running.Id : "");
        }

        [UnsupportedOSPlatform("browser")]
        static void Relaunch(GpuCard card, string sapGpu, string revertTo)
        {
            string host = Environment.ProcessPath ?? "";
            var args = Environment.GetCommandLineArgs().ToList();
            bool viaDotnet = string.Equals(Path.GetFileNameWithoutExtension(host), "dotnet", StringComparison.OrdinalIgnoreCase);
            var start = new ProcessStartInfo(host) { UseShellExecute = false, WorkingDirectory = AssetRoot.StartFolder };
            foreach (string a in viaDotnet ? args : args.Skip(1)) start.ArgumentList.Add(a);
            foreach (string v in CardVariables) start.Environment.Remove(v);
            start.Environment[Marker] = card.Id;
            start.Environment.Remove(RevertTo);
            if (revertTo.Length > 0)
            {
                start.Environment[RevertTo] = revertTo;
                start.Environment["SAP_SCREEN"] = "settings";    // the question shows where the card was picked
            }

            if (sapGpu.Length > 0) start.Environment["SAP_GPU"] = sapGpu;
            else start.Environment.Remove("SAP_GPU");
            if (!card.IsDefault && string.Equals(card.Vendor, GpuCards.Nvidia, StringComparison.Ordinal))
            {
                start.Environment["__NV_PRIME_RENDER_OFFLOAD"] = "1";
                start.Environment["__GLX_VENDOR_LIBRARY_NAME"] = "nvidia";
                start.Environment["__VK_LAYER_NV_optimus"] = "NVIDIA_only";
            }
            else if (!card.IsDefault)
            {
                start.Environment["DRI_PRIME"] = "pci-" + card.Id.Replace(':', '_').Replace('.', '_');
                start.Environment["__GLX_VENDOR_LIBRARY_NAME"] = "mesa";    // NVIDIA's GLX would take the window otherwise
                const string mesaEgl = "/usr/share/glvnd/egl_vendor.d/50_mesa.json";
                if (File.Exists(mesaEgl)) start.Environment["__EGL_VENDOR_LIBRARY_FILENAMES"] = mesaEgl;
            }

            Console.WriteLine($"SAP: gpu: starting again on {card.Name} [{card.Id}]");
            using var game = Process.Start(start) ?? throw new InvalidOperationException($"could not start {host}");
            game.WaitForExit();
            Environment.ExitCode = game.ExitCode;
        }
    }
}
