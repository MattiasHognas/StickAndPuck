using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Hud;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Screens;

/// <summary>The A3 art the screens draw with: the HUD's <see cref="HudArt"/> (font, panels, crests, icons) plus
/// the title logo, and a few layout helpers. Everything lands on whole pixels at 480x270.</summary>
internal sealed class ScreenArt
{
    /// <summary>The height of one menu row.</summary>
    public const int Row = 14;

    /// <summary>Muted text for items that cannot be picked (palette grey).</summary>
    public static readonly Color Muted = new(0x8a, 0x87, 0x90, 0xff);

    // the HUD target blends alpha into itself, so this reads as about half dark over the rink
    static readonly Color Shade = new(0x18, 0x13, 0x1f, 0xd8);
    static readonly Color Band = new(0x18, 0x13, 0x1f, 0xff);

    Texture2D title;

    ScreenArt(HudArt hud)
    {
        Hud = hud;
    }

    public HudArt Hud { get; }

    /// <summary>Gets the cream text colour.</summary>
    public Color Text => Hud.Text;

    /// <summary>Gets the gold highlight colour.</summary>
    public Color Gold => Hud.Score;

    /// <summary>Loads the art. Needs an open window.</summary>
    public static ScreenArt Load()
    {
        var art = new ScreenArt(HudArt.Load());
        art.title = LoadTexture("art/ui/title.png");
        return art;
    }

    /// <summary>Darkens the rink behind a screen.</summary>
    public static void Dim()
    {
        DrawRectangle(0, 0, Game.Width, Game.Height, Shade);
    }

    /// <summary>Draws the Stick and Puck logo centred at the given top.</summary>
    public void DrawTitle(int y)
    {
        DrawTexture(title, (Game.Width - title.Width) / 2, y, Color.White);
    }

    /// <summary>Draws text centred on a column.</summary>
    public void Centred(string text, int cx, int y, Color colour)
    {
        text = Fit(text);
        Hud.DrawText(text, cx - (Hud.Measure(text) / 2), y, colour);
    }

    /// <summary>Draws text with its right edge on a column.</summary>
    public void Right(string text, int right, int y, Color colour)
    {
        text = Fit(text);
        Hud.DrawText(text, right - Hud.Measure(text), y, colour);
    }

    /// <summary>Draws text with its left edge on a column.</summary>
    public void Left(string text, int x, int y, Color colour)
    {
        Hud.DrawText(Fit(text), x, y, colour);
    }

    /// <summary>Shortens text to a width, ending it with a dot when letters had to go.</summary>
    public string Clip(string text, int width)
    {
        text = Fit(text);
        if (Hud.Measure(text) <= width) return text;
        for (int n = text.Length - 1; n > 0; n--)
        {
            string cut = text[..n].TrimEnd() + ".";
            if (Hud.Measure(cut) <= width) return cut;
        }
        return "";
    }

    /// <summary>Draws a 0 to 100 rating as a bar with its number on the right.</summary>
    public void Bar(int value, int x, int y, int w, Color ink)
    {
        value = Math.Clamp(value, 0, 100);
        DrawRectangle(x, y + 3, w, 5, Band);
        DrawRectangle(x, y + 3, Math.Max(1, value * w / 100), 5, ink);
        Left(value.ToString(System.Globalization.CultureInfo.InvariantCulture), x + w + 4, y, Text);
    }

    /// <summary>Upper-cases text and swaps letters the pixel font lacks for their plain form (Á becomes A),
    /// keeping the Swedish Å, Ä, Ö and the É and Ü the font has.</summary>
    public static string Fit(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (char c in text.ToUpperInvariant())
        {
            if (c < 128 || "ÅÄÖÉÜ".Contains(c, StringComparison.Ordinal)) { sb.Append(c); continue; }
            foreach (char d in c.ToString().Normalize(System.Text.NormalizationForm.FormD))
                if (d < 128) sb.Append(d);
        }
        return sb.ToString();
    }

    /// <summary>Draws a panel with a heading in gold, centred on the frame, and returns where its body starts.</summary>
    public (int X, int Top) Panel(string heading, int w, int h, int y, string panel = "panel")
    {
        int x = (Game.Width - w) / 2;
        Hud.DrawPanel(panel, x, y, w, h);
        if (heading.Length == 0) return (x, y + 6);
        Centred(heading, Game.Width / 2, y + 5, Gold);
        return (x, y + 5 + Row + 2);
    }

    /// <summary>Draws a menu: one row per item, the selected one in gold with an arrow, values right-aligned with arrows either side.</summary>
    public void Menu(IReadOnlyList<MenuItem> items, int selected, int x, int w, int top)
    {
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            int y = top + (i * Row);
            bool on = i == selected;
            var ink = !item.Enabled ? Muted : on ? Gold : Text;
            if (on) Hud.DrawIcon("arrow_right", x + 6, y + 2);
            Left(item.Label, x + 18, y, ink);
            if (item.Value.HasValue)
            {
                string value = item.Value.Or("");
                string shown = on ? $"< {value} >" : value;
                Right(shown, x + w - 10, y, ink);
            }
        }
    }

    /// <summary>Draws the key hints along the bottom edge.</summary>
    public void Hint(string hint)
    {
        DrawRectangle(0, Game.Height - 15, Game.Width, 15, Band);
        Centred(hint, Game.Width / 2, Game.Height - 13, Muted);
    }

    /// <summary>Releases the textures.</summary>
    public void Unload()
    {
        UnloadTexture(title);
        Hud.Unload();
    }
}
