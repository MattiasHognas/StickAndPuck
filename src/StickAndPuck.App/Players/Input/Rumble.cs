using System.Runtime.InteropServices;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Players;

/// <summary>Gamepad rumble. Raylib's desktop build has no rumble (its SetGamepadVibration only logs a
/// warning there), so this talks to the system directly: on Linux a force-feedback rumble effect on
/// the pad's /dev/input event device, on Windows XInput. A pad or driver with no rumble is noted
/// once in the log and left quiet. SAP_RUMBLE=0 turns it off.</summary>
internal sealed partial class Rumble(Gamepads pads)
{
    const int Pads = 2;

    static readonly bool Off = OperatingSystem.IsBrowser() || Env.Is("SAP_RUMBLE", "0");
    readonly Maybe<IRumbler>[] rumblers = new Maybe<IRumbler>[Pads];
    readonly string[] opened = [.. Enumerable.Repeat(string.Empty, Pads)];   // "" for none: a pad's name is never empty

    interface IRumbler : IDisposable
    {
        void Start(float strong, float weak, float seconds);

        void Tick(float dt);
    }

    /// <summary>Rumbles the pad a device names; keyboard and none do nothing.</summary>
    public void Pulse(InputDevice device, float strong, float weak, float seconds)
    {
        int index = device switch { InputDevice.Gamepad1 => 0, InputDevice.Gamepad2 => 1, _ => -1 };
        if (index < 0 || Off) return;
        if (Rumbler(index).TryGet(out var r)) r.Start(Math.Clamp(strong, 0, 1), Math.Clamp(weak, 0, 1), seconds);
    }

    /// <summary>Stops rumbles whose time is up, where the system does not time them itself. Call every step.</summary>
    public void Update(float dt)
    {
        foreach (var r in rumblers)
            if (r.TryGet(out var rumbler)) rumbler.Tick(dt);
    }

    /// <summary>Finds or reopens the rumble for the n-th pad; a different pad in that place gets a fresh one.</summary>
    Maybe<IRumbler> Rumbler(int index)
    {
        if (!pads.Name(index).TryGet(out string name)) return Maybe<IRumbler>.None;
        if (string.Equals(opened[index], name, StringComparison.Ordinal)) return rumblers[index];
        if (rumblers[index].TryGet(out var old)) old.Dispose();
        opened[index] = name;
        rumblers[index] = OperatingSystem.IsLinux() ? Evdev.Open(index, name)
            : OperatingSystem.IsWindows() ? Maybe.Some<IRumbler>(new XInput(index))
            : Maybe<IRumbler>.None;
        if (!rumblers[index].HasValue) Console.WriteLine($"SAP: rumble: {name} has no rumble here");
        return rumblers[index];
    }

    /// <summary>Linux: a rumble effect uploaded to the pad's event device and played; the kernel stops it after its length.</summary>
    sealed partial class Evdev : IRumbler
    {
        const int OpenWrite = 1;            // O_WRONLY
        const ushort FfRumble = 0x50, EvFf = 0x15;
        const int FfRumbleBit = 0x50;

        // _IOW('E', 0x80, struct ff_effect), with sizeof(struct ff_effect) 48 on 64-bit
        const uint SetEffect = 0x40304580;

        readonly int fd;
        short effect = -1;

        Evdev(int fd)
        {
            this.fd = fd;
        }

        /// <summary>Opens the event device of the n-th pad that can rumble, preferring one whose name matches.</summary>
        public static Maybe<IRumbler> Open(int index, string name)
        {
            var pads = new List<(string Path, string Name)>();
            foreach (string dir in Directory.Exists("/sys/class/input") ? Directory.GetDirectories("/sys/class/input", "event*").Order(StringComparer.Ordinal) : Enumerable.Empty<string>())
            {
                string device = Path.Combine(dir, "device");
                // a joystick has a jsN handler next to its event device; mice and keyboards do not
                if (!Directory.Exists(device) || !Directory.GetDirectories(device, "js*").Any()) continue;
                if (!CanRumble(Path.Combine(device, "capabilities", "ff"))) continue;
                string devName = Read(Path.Combine(device, "name"));
                pads.Add(("/dev/input/" + Path.GetFileName(dir), devName));
            }

            var found = pads.FirstOrNone(p => Matches(p.Name, name));
            if (!found.HasValue && index < pads.Count) found = pads[index];
            if (!found.TryGet(out var pick)) return Maybe<IRumbler>.None;
            int fd = open(pick.Path, OpenWrite);
            if (fd < 0)
            {
                Console.WriteLine($"SAP: rumble: cannot open {pick.Path} for {name} (no write access)");
                return Maybe<IRumbler>.None;
            }
            Console.WriteLine($"SAP: rumble: {name} on {pick.Path} ({pick.Name})");
            return Maybe.Some<IRumbler>(new Evdev(fd));
        }

        public unsafe void Start(float strong, float weak, float seconds)
        {
            byte* e = stackalloc byte[48];
            new Span<byte>(e, 48).Clear();
            *(ushort*)e = FfRumble;
            *(short*)(e + 2) = effect;                                       // -1 uploads a new one, else replaces it
            *(ushort*)(e + 10) = (ushort)Math.Clamp(seconds * 1000, 1, 5000);   // replay.length in ms
            *(ushort*)(e + 16) = (ushort)(strong * ushort.MaxValue);
            *(ushort*)(e + 18) = (ushort)(weak * ushort.MaxValue);
            if (ioctl(fd, SetEffect, e) < 0)
            {
                effect = -1;
                return;
            }
            effect = *(short*)(e + 2);

            byte* play = stackalloc byte[24];
            new Span<byte>(play, 24).Clear();
            *(ushort*)(play + 16) = EvFf;
            *(ushort*)(play + 18) = (ushort)effect;
            *(int*)(play + 20) = 1;
            _ = write(fd, play, 24);
        }

        public void Tick(float dt)
        {
            // the kernel ends the effect after its length
        }

        public void Dispose()
        {
            _ = close(fd);
        }

        /// <summary>The ff capability bitmap is hex words, most significant first; FF_RUMBLE is bit 0x50.</summary>
        static bool CanRumble(string file)
        {
            string[] words = Read(file).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int word = FfRumbleBit / 64, bit = FfRumbleBit % 64;
            if (word >= words.Length) return false;
            return ulong.TryParse(words[words.Length - 1 - word], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out ulong bits)
                && (bits & (1UL << bit)) != 0;
        }

        static bool Matches(string kernelName, string padName)
        {
            return kernelName.Length > 0
                && (kernelName.Contains(padName, StringComparison.OrdinalIgnoreCase) || padName.Contains(kernelName, StringComparison.OrdinalIgnoreCase));
        }

        static string Read(string file)
        {
            try
            {
                return File.Exists(file) ? File.ReadAllText(file).Trim() : string.Empty;
            }
            catch (IOException)
            {
                return string.Empty;
            }
            catch (UnauthorizedAccessException)
            {
                return string.Empty;
            }
        }

        [LibraryImport("libc", EntryPoint = "open", StringMarshalling = StringMarshalling.Utf8)]
        private static partial int open(string path, int flags);

        [LibraryImport("libc", EntryPoint = "ioctl")]
        private static unsafe partial int ioctl(int fd, nuint request, void* arg);

        [LibraryImport("libc", EntryPoint = "write")]
        private static unsafe partial nint write(int fd, void* buffer, nuint count);

        [LibraryImport("libc", EntryPoint = "close")]
        private static partial int close(int fd);
    }

    /// <summary>Windows: XInput motors, switched off again when the time is up.</summary>
    sealed partial class XInput(int index) : IRumbler
    {
        float left;

        public void Start(float strong, float weak, float seconds)
        {
            left = seconds;
            Set(strong, weak);
        }

        public void Tick(float dt)
        {
            if (left <= 0) return;
            left -= dt;
            if (left <= 0) Set(0, 0);
        }

        public void Dispose()
        {
            if (left > 0) Set(0, 0);
        }

        void Set(float strong, float weak)
        {
            try
            {
                uint motors = (uint)(strong * ushort.MaxValue) | ((uint)(weak * ushort.MaxValue) << 16);
                _ = XInputSetState((uint)index, ref motors);
            }
            catch (DllNotFoundException)
            {
                left = 0;
            }
            catch (EntryPointNotFoundException)
            {
                left = 0;
            }
        }

        [LibraryImport("xinput1_4.dll")]
        private static partial uint XInputSetState(uint userIndex, ref uint vibration);
    }
}
