using System.Text.Json;
using Raylib_cs;
using StickAndPuck.App.Core;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Hud;

/// <summary>
/// The A3 HUD art from art/ui as A3 exported it: pixel font, scorebug frame with its slots,
/// 9-slice panels, icons and club crests. Layout numbers come from ui.json, so the art and the
/// game share one source. Every draw lands on whole pixels at the native 480x270.
/// </summary>
internal sealed class HudArt
{
    /// <summary>Text colour from ui.json (cream).</summary>
    public Color Text { get; private set; } = Hex("#efe0c2");

    /// <summary>Score colour from ui.json (gold).</summary>
    public Color Score { get; private set; } = Hex("#f2c14e");

    /// <summary>Where the scorebug's top-left corner sits on screen.</summary>
    public (int X, int Y) BugPosition { get; private set; } = (8, 8);

    /// <summary>The scorebug frame's size.</summary>
    public (int W, int H) BugSize => (scorebug.Width, scorebug.Height);

    /// <summary>The font's line height in pixels.</summary>
    public int LineHeight => font.BaseSize;

    readonly Dictionary<string, Rectangle> slots = new(StringComparer.Ordinal);
    readonly Dictionary<string, Rectangle> crests = new(StringComparer.Ordinal);
    readonly Dictionary<string, Rectangle> icons = new(StringComparer.Ordinal);
    readonly Dictionary<string, (Texture2D Texture, int Border)> panels = new(StringComparer.Ordinal);
    string crestFallback = "generic";
    Font font;
    Texture2D scorebug, crestSheet, iconSheet;

    /// <summary>Loads the layout and textures. Needs an open window.</summary>
    /// <returns>The loaded art.</returns>
    public static HudArt Load()
    {
        var art = new HudArt();
        using var doc = JsonDocument.Parse(File.ReadAllText(Assets.UiLayout));
        var root = doc.RootElement;

        art.font = LoadFont(root.GetProperty("font").GetProperty("file").GetString()!);
        SetTextureFilter(art.font.Texture, TextureFilter.Point);

        var bug = root.GetProperty("scorebug");
        art.scorebug = LoadTexture(bug.GetProperty("file").GetString()!);
        var pos = bug.GetProperty("position");
        art.BugPosition = (pos[0].GetInt32(), pos[1].GetInt32());
        art.Text = Hex(bug.GetProperty("textColour").GetString()!);
        art.Score = Hex(bug.GetProperty("scoreColour").GetString()!);
        ReadCells(bug.GetProperty("slots"), art.slots);

        foreach (var panel in root.GetProperty("panels").EnumerateObject())
            art.panels[panel.Name] = (LoadTexture(panel.Value.GetProperty("file").GetString()!), panel.Value.GetProperty("border")[0].GetInt32());

        var icon = root.GetProperty("icons");
        art.iconSheet = LoadTexture(icon.GetProperty("file").GetString()!);
        ReadCells(icon.GetProperty("cells"), art.icons);

        var crest = root.GetProperty("crests");
        art.crestSheet = LoadTexture(crest.GetProperty("file").GetString()!);
        ReadCells(crest.GetProperty("cells"), art.crests);
        art.crestFallback = crest.GetProperty("fallback").GetString() ?? "generic";
        return art;
    }

    /// <summary>Gets a scorebug slot in screen pixels.</summary>
    /// <param name="name">The slot name from ui.json, for example "home_score".</param>
    /// <returns>The slot rectangle.</returns>
    public Rectangle Slot(string name)
    {
        var r = slots[name];
        return new Rectangle(BugPosition.X + r.X, BugPosition.Y + r.Y, r.Width, r.Height);
    }

    /// <summary>Draws the empty scorebug frame.</summary>
    public void DrawScorebug()
    {
        DrawTexture(scorebug, BugPosition.X, BugPosition.Y, Color.White);
    }

    /// <summary>Draws a club's 16x16 crest, or the generic one for a club without art.</summary>
    /// <param name="clubId">The club id from its data file.</param>
    /// <param name="x">Left edge.</param>
    /// <param name="y">Top edge.</param>
    public void DrawCrest(string clubId, int x, int y)
    {
        if (!crests.TryGetValue(clubId, out var src)) src = crests[crestFallback];
        DrawTextureRec(crestSheet, src, new(x, y), Color.White);
    }

    /// <summary>Draws one 8x8 icon.</summary>
    /// <param name="name">The icon name from ui.json.</param>
    /// <param name="x">Left edge.</param>
    /// <param name="y">Top edge.</param>
    public void DrawIcon(string name, int x, int y)
    {
        if (icons.TryGetValue(name, out var src)) DrawTextureRec(iconSheet, src, new(x, y), Color.White);
    }

    /// <summary>Draws a 9-slice panel.</summary>
    /// <param name="name">"panel", "panel_gold" or "panel_red".</param>
    /// <param name="x">Left edge.</param>
    /// <param name="y">Top edge.</param>
    /// <param name="w">Width.</param>
    /// <param name="h">Height.</param>
    public void DrawPanel(string name, int x, int y, int w, int h)
    {
        var (tex, b) = panels[name];
        var info = new NPatchInfo
        {
            Source = new Rectangle(0, 0, tex.Width, tex.Height),
            Left = b, Top = b, Right = b, Bottom = b,
            Layout = NPatchLayout.NinePatch,
        };
        DrawTextureNPatch(tex, info, new Rectangle(x, y, w, h), new(0, 0), 0, Color.White);
    }

    /// <summary>Measures text in the pixel font, without the trailing one-pixel gap.</summary>
    /// <param name="text">Text in capitals.</param>
    /// <returns>The width in pixels.</returns>
    public int Measure(string text)
    {
        if (text.Length == 0) return 0;
        return (int)MeasureTextEx(font, text, font.BaseSize, 0).X - 1;
    }

    /// <summary>Draws text with its top-left corner on a whole pixel. The font has no lowercase, so text is upper-cased.</summary>
    /// <param name="text">The text.</param>
    /// <param name="x">Left edge.</param>
    /// <param name="y">Top of the accent rows; capitals start 2 px lower.</param>
    /// <param name="colour">Ink colour.</param>
    public void DrawText(string text, int x, int y, Color colour)
    {
        DrawTextEx(font, text.ToUpperInvariant(), new(x, y), font.BaseSize, 0, colour);
    }

    /// <summary>Draws text centred in a 7 px capital slot from the scorebug layout.</summary>
    /// <param name="text">The text.</param>
    /// <param name="slot">The slot in screen pixels.</param>
    /// <param name="colour">Ink colour.</param>
    public void DrawInSlot(string text, Rectangle slot, Color colour)
    {
        text = text.ToUpperInvariant();
        int x = (int)slot.X + (((int)slot.Width - Measure(text)) / 2);
        DrawText(text, x, (int)slot.Y - 2, colour);
    }

    /// <summary>Releases the textures.</summary>
    public void Unload()
    {
        UnloadFont(font);
        UnloadTexture(scorebug);
        UnloadTexture(crestSheet);
        UnloadTexture(iconSheet);
        foreach (var (tex, _) in panels.Values) UnloadTexture(tex);
    }

    /// <summary>Parses "#rrggbb".</summary>
    /// <param name="hex">The colour.</param>
    /// <returns>The colour, opaque.</returns>
    public static Color Hex(string hex)
    {
        int v = Convert.ToInt32(hex.TrimStart('#'), 16);
        return new Color((v >> 16) & 0xff, (v >> 8) & 0xff, v & 0xff, 255);
    }

    static void ReadCells(JsonElement cells, Dictionary<string, Rectangle> into)
    {
        foreach (var cell in cells.EnumerateObject())
        {
            var a = cell.Value;
            into[cell.Name] = new Rectangle(a[0].GetInt32(), a[1].GetInt32(), a[2].GetInt32(), a[3].GetInt32());
        }
    }
}
