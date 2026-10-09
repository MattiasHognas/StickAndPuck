using System.Numerics;
using Raylib_cs;
using StickAndPuck.Core;
using StickAndPuck.Render;
using StickAndPuck.Rules;

namespace StickAndPuck.Hud;

/// <summary>
/// W10: scorebug with crests and short names, clock, period and goal messages, drawn in DrawHud at
/// 480x270 from <see cref="World.Match"/> and <see cref="World.Events"/>. Shows only in worlds
/// that run the match flow (or when <see cref="ShowAlways"/> is set), so test scenes stay clean.
/// </summary>
internal sealed class HudSystem : ISystem
{
    /// <summary>The first free row under the scorebug; W14's penalty box goes here.</summary>
    public const int PenaltyTop = 28;

    /// <summary>The top of the centre banner, above the centre-ice faceoff.</summary>
    const int BannerTop = 88;

    readonly HudMessages messages = new();
    HudArt? art;

    /// <summary>Gets or sets a value indicating whether the scorebug shows in a world without the match flow (the hud scene).</summary>
    public static bool ShowAlways { get; set; }

    public bool RunsWhilePaused => true;

    public void Start(World world)
    {
        art = HudArt.Load();
        messages.Clear();
    }

    public void Update(World world, float dt)
    {
        HudDemo.Update(world);
        messages.Update(world);
    }

    public void DrawHud(World world, RenderContext render)
    {
        if (art == null || !(ShowAlways || MatchFlow.IsRunning(world))) return;
        DrawScorebug(world, art);
        if (world.Match.Phase == MatchPhase.Intro) DrawIntro(world, art);
        else if (messages.Current is { } m) DrawBanner(m, art);
    }

    /// <summary>Gets the period text: 1, 2, 3, OT or SO.</summary>
    /// <param name="world">The match state.</param>
    /// <returns>The text.</returns>
    public static string PeriodText(World world)
    {
        var m = world.Match;
        if (m.Phase == MatchPhase.Shootout) return "SO";
        return m.Period > world.Setup.Periods ? "OT" : m.Period.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Gets the club colour for the stripe under a short name: the kit that side wears, on the nearest palette colour.</summary>
    /// <param name="world">The match state.</param>
    /// <param name="side">Which club.</param>
    /// <returns>The colour.</returns>
    public static Color StripeColour(World world, Side side)
    {
        var club = world.Setup.Club(side);
        var c = HudArt.Hex(side == Side.Home ? club.Home.Primary : club.Away.Primary);
        var want = new Vector3(c.R, c.G, c.B) / 255f;
        var near = Palette.Current.MinBy(p => Vector3.DistanceSquared(p, want)) * 255f;
        return new Color((int)MathF.Round(near.X), (int)MathF.Round(near.Y), (int)MathF.Round(near.Z), 255);
    }

    static void DrawScorebug(World world, HudArt art)
    {
        var m = world.Match;
        art.DrawScorebug();
        foreach (var side in new[] { Side.Home, Side.Away })
        {
            string key = side == Side.Home ? "home" : "away";
            var club = world.Setup.Club(side);
            var crest = art.Slot($"{key}_crest");
            art.DrawCrest(club.Id, (int)crest.X, (int)crest.Y);
            art.DrawInSlot(club.ShortName, art.Slot($"{key}_name"), art.Text);
            var stripe = art.Slot($"{key}_stripe");
            Raylib.DrawRectangle((int)stripe.X, (int)stripe.Y, (int)stripe.Width, (int)stripe.Height, StripeColour(world, side));
            art.DrawInSlot(m.Score[(int)side].ToString(System.Globalization.CultureInfo.InvariantCulture), art.Slot($"{key}_score"), art.Score);
        }
        art.DrawInSlot(PeriodText(world), art.Slot("period"), art.Text);
        art.DrawInSlot(MatchFlow.ClockText(m.Clock), art.Slot("clock"), art.Text);
    }

    /// <summary>The opening card: both crests and club names.</summary>
    static void DrawIntro(World world, HudArt art)
    {
        string home = world.Setup.Home.Name.ToUpperInvariant(), away = world.Setup.Away.Name.ToUpperInvariant();
        const int gap = 8, crest = 16;
        int vs = art.Measure("VS");
        int textW = art.Measure(home) + gap + vs + gap + art.Measure(away);
        int w = textW + (2 * (crest + gap)) + 12, h = crest + 12;
        int x = (Game.Width - w) / 2, y = BannerTop;
        art.DrawPanel("panel", x, y, w, h);
        int cx = x + 6;
        art.DrawCrest(world.Setup.Home.Id, cx, y + 6);
        cx += crest + gap;
        int ty = y + 6 + 2;      // a capital is 7 px; text y is 2 px above it, so this centres caps on the crest
        art.DrawText(home, cx, ty, art.Text);
        cx += art.Measure(home) + gap;
        art.DrawText("VS", cx, ty, art.Score);
        cx += vs + gap;
        art.DrawText(away, cx, ty, art.Text);
        cx += art.Measure(away) + gap;
        art.DrawCrest(world.Setup.Away.Id, cx, y + 6);
    }

    static void DrawBanner(HudMessage m, HudArt art)
    {
        const int pad = 6, crest = 16, gap = 6, line = 11;
        int lead = m.Crest != null ? crest + gap : m.Icon != null ? 8 + gap : 0;
        int textW = Math.Max(art.Measure(m.Title.ToUpperInvariant()), art.Measure(m.Detail.ToUpperInvariant()));
        int lines = m.Detail.Length > 0 ? 2 : 1;
        int textH = (lines * line) - 2;
        int inner = Math.Max(textH, m.Crest != null ? crest : 0);
        int w = pad + lead + textW + pad, h = pad + inner + pad;
        int x = (Game.Width - w) / 2, y = BannerTop;
        art.DrawPanel(m.Panel, x, y, w, h);

        int top = y + pad + ((inner - textH) / 2) - 2;     // text y is 2 px above the capitals
        if (m.Crest != null) art.DrawCrest(m.Crest, x + pad, y + pad + ((inner - crest) / 2));
        else if (m.Icon != null) art.DrawIcon(m.Icon, x + pad, top + 2);
        int tx = x + pad + lead;
        art.DrawText(m.Title, tx, top, string.Equals(m.Panel, "panel_gold", StringComparison.Ordinal) ? art.Text : art.Score);
        if (lines == 2) art.DrawText(m.Detail, tx, top + line, art.Text);
    }
}
