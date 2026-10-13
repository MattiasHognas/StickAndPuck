using System.Globalization;

namespace StickAndPuck.App.Core;

/// <summary>The one place the game reads environment variables. The SAP_* switches (and
/// SDL_GAMECONTROLLERCONFIG) are read once, at the first ask; after that a replay or a test
/// changes them here with <see cref="Set"/>, never in the process environment.</summary>
public static class Env
{
    static readonly Dictionary<string, string> Values = Read();

    /// <summary>Gets every SAP_* variable that is set, by name.</summary>
    public static IEnumerable<KeyValuePair<string, string>> Sap =>
        Values.Where(v => v.Key.StartsWith("SAP_", StringComparison.Ordinal)).ToList();

    /// <summary>Gets a variable.</summary>
    public static string Get(string name) => Values.GetValueOrDefault(name, "");

    /// <summary>Gets a value indicating whether a variable is set to exactly this value.</summary>
    public static bool Is(string name, string value) => string.Equals(Get(name), value, StringComparison.Ordinal);

    /// <summary>Reads a number.</summary>
    public static Maybe<float> Float(string name) =>
        float.TryParse(Get(name), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : Maybe<float>.None;

    /// <summary>Reads a whole number.</summary>
    /// <returns>The number, or None when the variable is not set or not a whole number.</returns>
    public static Maybe<int> Int(string name) =>
        int.TryParse(Get(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : Maybe<int>.None;

    /// <summary>Reads a whole number.</summary>
    /// <returns>The number, or None when the variable is not set or not a whole number.</returns>
    public static Maybe<long> Long(string name) =>
        long.TryParse(Get(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out long v) ? v : Maybe<long>.None;

    /// <summary>Sets a variable for the rest of the run; "" removes it.</summary>
    public static void Set(string name, string value)
    {
        if (value.Length == 0) Values.Remove(name);
        else Values[name] = value;
    }

    static Dictionary<string, string> Read()
    {
        var values = new Dictionary<string, string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (System.Collections.DictionaryEntry e in Environment.GetEnvironmentVariables())
        {
            if (e.Key is string k && e.Value is string { Length: > 0 } v && (k.StartsWith("SAP_", StringComparison.Ordinal) || string.Equals(k, "SDL_GAMECONTROLLERCONFIG", StringComparison.Ordinal)))
                values[k] = v;
        }

        return values;
    }
}
