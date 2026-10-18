// Stick & Puck: a pixel-art hockey game drawn in 3D. Everything renders to a 480x270 target
// that is scaled up by whole pixels, so the window size never changes the look.
// OpenGL picks its graphics card when the window is made, so a game that should run on another
// card than it started on starts itself again on that card first (see GpuLaunch).

using System.Diagnostics;
using System.Runtime.Versioning;
using StickAndPuck.App.Core;
using StickAndPuck.App.Screens;

namespace StickAndPuck.App
{
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
        private const string Marker = "SAP_GPU_LAUNCHED";
        private const string RevertTo = "SAP_GPU_REVERT";

        private static readonly string[] CardVariables =
        [
            "__NV_PRIME_RENDER_OFFLOAD",
            "__GLX_VENDOR_LIBRARY_NAME",
            "__VK_LAYER_NV_optimus",
            "DRI_PRIME",
            "__EGL_VENDOR_LIBRARY_FILENAMES",
        ];

        private static Maybe<GpuCard> pending = Maybe.None<GpuCard>();
        private static bool ask;

        /// <summary>Gets the card this game draws on.</summary>
        public static GpuCard Running => GpuCards.Resolve(GpuCards.All, Env.Get(Marker));

        /// <summary>Gets the card to go back to while this game, started on a newly picked card, waits to be kept; None once kept.</summary>
        public static Maybe<GpuCard> Unconfirmed =>
            Env.Get(RevertTo) is { Length: > 0 } back
                ? GpuCards.Resolve(GpuCards.All, back)
                : Maybe.None<GpuCard>();

        /// <summary>Gets a value indicating whether picking a card starts the game again on it (Linux only).</summary>
        public static bool CanSwitch => OperatingSystem.IsLinux() && GpuCards.All.Count > 1;

        /// <summary>Starts the game again on the wanted card when this one runs elsewhere.</summary>
        /// <returns>True when that game ran (and has ended) instead of this one.</returns>
        public static bool OntoWanted()
        {
            if (OperatingSystem.IsBrowser())
            {
                return false;
            }

            if (!CanSwitch)
            {
                return false;
            }

            GpuCard wanted = GpuCards.Resolve(
                GpuCards.All,
                Settings.FromEnvironment(ScreenPrefsFile.Read().Settings).Gpu
            );
            if (string.Equals(wanted.Id, Running.Id, StringComparison.Ordinal))
            {
                return false;
            }

            Relaunch(wanted, Env.Get("SAP_GPU"), "");
            return true;
        }

        /// <summary>Keeps the card this game runs on: the keep question is answered.</summary>
        public static void Keep()
        {
            Env.Set(RevertTo, "");
        }

        /// <summary>Asks for the game to start again on a card once it ends; false when it runs there already.</summary>
        /// <param name="gpu">The card, as <see cref="Settings.Gpu"/> names it.</param>
        /// <param name="confirm">Whether the started game asks to keep the card, going back to this one otherwise.</param>
        public static bool Request(string gpu, bool confirm)
        {
            ask = confirm;
            GpuCard card = GpuCards.Resolve(GpuCards.All, gpu);
            pending =
                CanSwitch && !string.Equals(card.Id, Running.Id, StringComparison.Ordinal)
                    ? card
                    : Maybe.None<GpuCard>();
            return pending.HasValue;
        }

        /// <summary>Starts the game again on the card <see cref="Request"/> asked for, if any.</summary>
        [UnsupportedOSPlatform("browser")]
        public static void AfterRun()
        {
            if (pending.TryGet(out GpuCard card))
            {
                Relaunch(card, card.IsDefault ? "" : card.Id, ask ? Running.Id : "");
            }
        }

        [UnsupportedOSPlatform("browser")]
        private static void Relaunch(GpuCard card, string sapGpu, string revertTo)
        {
            using Process game = Start(card, sapGpu, revertTo);
            game.WaitForExit();
            Environment.ExitCode = game.ExitCode;
        }

        [UnsupportedOSPlatform("browser")]
        private static Process Start(GpuCard card, string sapGpu, string revertTo)
        {
            string host = Environment.ProcessPath ?? "";
            List<string> args = [.. Environment.GetCommandLineArgs()];
            bool viaDotnet = string.Equals(
                Path.GetFileNameWithoutExtension(host),
                "dotnet",
                StringComparison.OrdinalIgnoreCase
            );
            ProcessStartInfo start = new(host)
            {
                UseShellExecute = false,
                WorkingDirectory = AssetRoot.StartFolder,
            };
            foreach (string a in viaDotnet ? args : args.Skip(1))
            {
                start.ArgumentList.Add(a);
            }

            PrepareEnvironment(start, card, sapGpu, revertTo);
            Console.WriteLine($"SAP: gpu: starting again on {card.Name} [{card.Id}]");
            return Process.Start(start)
                ?? throw new InvalidOperationException($"could not start {host}");
        }

        [UnsupportedOSPlatform("browser")]
        private static void PrepareEnvironment(
            ProcessStartInfo start,
            GpuCard card,
            string sapGpu,
            string revertTo
        )
        {
            foreach (string v in CardVariables)
            {
                _ = start.Environment.Remove(v);
            }

            start.Environment[Marker] = card.Id;
            _ = start.Environment.Remove(RevertTo);
            if (revertTo.Length > 0)
            {
                start.Environment[RevertTo] = revertTo;
                start.Environment["SAP_SCREEN"] = "settings"; // the question shows where the card was picked
            }

            if (sapGpu.Length > 0)
            {
                start.Environment["SAP_GPU"] = sapGpu;
            }
            else
            {
                _ = start.Environment.Remove("SAP_GPU");
            }

            if (
                !card.IsDefault
                && string.Equals(card.Vendor, GpuCards.Nvidia, StringComparison.Ordinal)
            )
            {
                SetNvidia(card, start);
            }
            else if (!card.IsDefault)
            {
                SetMesa(card, start);
            }
        }

        [UnsupportedOSPlatform("browser")]
        private static void SetNvidia(GpuCard card, ProcessStartInfo start)
        {
            _ = card;
            start.Environment["__NV_PRIME_RENDER_OFFLOAD"] = "1";
            start.Environment["__GLX_VENDOR_LIBRARY_NAME"] = "nvidia";
            start.Environment["__VK_LAYER_NV_optimus"] = "NVIDIA_only";
        }

        [UnsupportedOSPlatform("browser")]
        private static void SetMesa(GpuCard card, ProcessStartInfo start)
        {
            start.Environment["DRI_PRIME"] = "pci-" + card.Id.Replace(':', '_').Replace('.', '_');
            start.Environment["__GLX_VENDOR_LIBRARY_NAME"] = "mesa"; // NVIDIA's GLX would take the window otherwise
            const string mesaEgl = "/usr/share/glvnd/egl_vendor.d/50_mesa.json";
            if (File.Exists(mesaEgl))
            {
                start.Environment["__EGL_VENDOR_LIBRARY_FILENAMES"] = mesaEgl;
            }
        }
    }
}
