using Raylib_cs;
using static Raylib_cs.Raylib;

namespace StickAndPuck.Players;

/// <summary>
/// W38: which Raylib gamepad slots hold a pad. "Gamepad 1" is the first connected pad and
/// "gamepad 2" the second, whatever slot Raylib put them in, so a pad that lands on slot 2 (after
/// a replug, or behind a joystick-like device that took slot 0) still plays. Pads plugged in after
/// start are picked up on the next step. Extra SDL mappings for pads Raylib does not know are read
/// once from SDL_GAMECONTROLLERCONFIG and from gamecontrollerdb.txt next to the game or in the
/// settings folder.
/// </summary>
public static class Gamepads
{
    /// <summary>Raylib's number of gamepad slots.</summary>
    public const int MaxSlots = 4;

    /// <summary>
    /// Mappings shipped with the game for pads missing from Raylib's built-in list, in SDL format.
    /// Razer Raiju Ultimate Wired (1532:1004) runs on hid-generic on Linux with the PS4 HID layout.
    /// </summary>
    const string BuiltInMappings =
        "03000000321500000410000000010000,Razer Raiju Ultimate Wired,a:b1,b:b2,back:b8,dpdown:h0.4,dpleft:h0.8,dpright:h0.2,dpup:h0.1,guide:b12,leftshoulder:b4,leftstick:b10,lefttrigger:a3,leftx:a0,lefty:a1,rightshoulder:b5,rightstick:b11,righttrigger:a4,rightx:a2,righty:a5,start:b9,x:b0,y:b3,platform:Linux,\n";

    static readonly string?[] Seen = new string?[MaxSlots];
    static bool mappingsLoaded;
    static double lastLog = double.NegativeInfinity;

    /// <summary>Gets or sets a value indicating whether every slot is printed twice a second (SAP_SCENE=padtest).</summary>
    public static bool Logging { get; set; }

    /// <summary>Gets the Raylib slots that hold a pad now, lowest first.</summary>
    /// <returns>The slots; empty when no pad is connected or the window is not open.</returns>
    public static IReadOnlyList<int> Connected()
    {
        if (!IsWindowReady()) return [];
        Refresh();
        var slots = new List<int>(MaxSlots);
        for (int slot = 0; slot < MaxSlots; slot++)
            if (IsGamepadAvailable(slot)) slots.Add(slot);
        return slots;
    }

    /// <summary>Gets the Raylib slot of the n-th connected pad.</summary>
    /// <param name="index">0 for the first pad, 1 for the second.</param>
    /// <returns>The slot, or -1 when fewer pads are connected.</returns>
    public static int Slot(int index)
    {
        var slots = Connected();
        return index >= 0 && index < slots.Count ? slots[index] : -1;
    }

    /// <summary>Gets the name of the n-th connected pad, as the settings and versus screens show it.</summary>
    /// <param name="index">0 for the first pad, 1 for the second.</param>
    /// <returns>The pad's name, or null when it is not connected.</returns>
    public static string? Name(int index)
    {
        int slot = Slot(index);
        return slot < 0 ? null : SlotName(slot);
    }

    /// <summary>Gets the name Raylib reports for a slot.</summary>
    /// <param name="slot">The Raylib slot.</param>
    /// <returns>The name, or "GAMEPAD" when Raylib has none.</returns>
    public static string SlotName(int slot)
    {
        string name = IsGamepadAvailable(slot) ? GetGamepadName_(slot) : string.Empty;
        return string.IsNullOrWhiteSpace(name) ? "GAMEPAD" : name.Trim();
    }

    /// <summary>Prints every slot's pad, axes and held buttons twice a second while <see cref="Logging"/> is on.</summary>
    public static void Log()
    {
        if (!Logging || !IsWindowReady() || GetTime() - lastLog < 0.5) return;
        lastLog = GetTime();
        Refresh();
        for (int slot = 0; slot < MaxSlots; slot++)
        {
            if (!IsGamepadAvailable(slot)) { Console.WriteLine($"SAP: pad slot {slot}: none"); continue; }
            int axes = GetGamepadAxisCount(slot);
            string axisText = string.Join(" ", Enumerable.Range(0, axes).Select(a => GetGamepadAxisMovement(slot, (GamepadAxis)a).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)));
            string held = string.Join(" ", Enum.GetValues<GamepadButton>().Where(b => b != GamepadButton.Unknown && IsGamepadButtonDown(slot, b)));
            Console.WriteLine($"SAP: pad slot {slot}: {SlotName(slot)}, {axes} axes [{axisText}], held [{held}], last pressed {GetGamepadButtonPressed()}");
        }
    }

    /// <summary>Loads extra mappings once and logs pads coming and going.</summary>
    static void Refresh()
    {
        if (!mappingsLoaded)
        {
            mappingsLoaded = true;
            LoadMappings();
        }

        for (int slot = 0; slot < MaxSlots; slot++)
        {
            string? now = IsGamepadAvailable(slot) ? SlotName(slot) : null;
            if (string.Equals(now, Seen[slot], StringComparison.Ordinal)) continue;
            Console.WriteLine(now == null ? $"SAP: gamepad left slot {slot}" : $"SAP: gamepad in slot {slot}: {now}");
            Seen[slot] = now;
        }
    }

    static void LoadMappings()
    {
        if (string.Equals(Environment.GetEnvironmentVariable("SAP_PAD_MAPPINGS"), "0", StringComparison.Ordinal)) return;
        var sources = new List<(string From, string Text)> { ("the game", BuiltInMappings) };
        if (Environment.GetEnvironmentVariable("SDL_GAMECONTROLLERCONFIG") is { Length: > 0 } env) sources.Add(("SDL_GAMECONTROLLERCONFIG", env));
        string config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StickAndPuck");
        foreach (string folder in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory(), config }.Distinct())
        {
            string file = Path.Combine(folder, "gamecontrollerdb.txt");
            if (File.Exists(file)) sources.Add((file, File.ReadAllText(file)));
        }

        foreach (var (from, text) in sources)
        {
            // GLFW parses one mapping per line and skips the ones for other platforms
            int added = SetGamepadMappings(text);
            Console.WriteLine($"SAP: gamepad mappings from {from}: {(added > 0 ? "loaded" : "none taken")}");
        }
    }
}
