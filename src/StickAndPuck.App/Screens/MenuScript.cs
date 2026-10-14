using StickAndPuck.App.Core;

namespace StickAndPuck.App.Screens;

/// <summary>SAP_SCREEN_KEYS="down enter ..." presses scripted buttons instead of the devices (one every
/// <see cref="ScriptGap"/> steps while a menu is up), for headless runs through the screens;
/// "shot:name" saves name.png of the last frame and "wait" does nothing for one gap. Read once
/// and kept for the whole run, across the worlds the screens hand over to.</summary>
internal sealed class MenuScript
{
    /// <summary>Steps between scripted presses.</summary>
    public const int ScriptGap = 20;

    Maybe<Queue<string>> script;
    int scriptWait;

    /// <summary>Gets a value indicating whether a script drives the menus.</summary>
    public bool IsScripted => script.HasValue;

    /// <summary>Reads SAP_SCREEN_KEYS once.</summary>
    public void Load()
    {
        if (script.HasValue || Env.Get("SAP_SCREEN_KEYS") is not { Length: > 0 } keys) return;
        var queue = new Queue<string>(keys.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries));
        script = queue;
        Console.WriteLine($"SAP: screens: {queue.Count} scripted presses");
    }

    /// <summary>The scripted press for this step, usually none.</summary>
    /// <param name="menuUp">Whether a menu is showing; while a match runs only a scripted "pause" is taken.</param>
    public MenuButton Next(bool menuUp)
    {
        if (!script.TryGet(out var queue) || queue.Count == 0) return MenuButton.None;
        string token = queue.Peek();
        if (!menuUp && !string.Equals(token, "pause", StringComparison.OrdinalIgnoreCase))
        {
            scriptWait = 0;    // the gap counts from when a menu shows, so it has been drawn before a shot
            return MenuButton.None;
        }
        if (++scriptWait < ScriptGap) return MenuButton.None;
        queue.Dequeue();
        scriptWait = 0;
        Console.WriteLine($"SAP: screens: scripted {token}");
        if (token.StartsWith("shot:", StringComparison.OrdinalIgnoreCase))
        {
            AssetRoot.SaveScreen(token[5..] + ".png");
            return MenuButton.None;
        }
        return token.ToLowerInvariant() switch
        {
            "up" => MenuButton.Up,
            "down" => MenuButton.Down,
            "left" => MenuButton.Left,
            "right" => MenuButton.Right,
            "enter" => MenuButton.Accept,
            "back" => MenuButton.Back,
            "pause" => MenuButton.Pause,
            _ => MenuButton.None,
        };
    }
}
