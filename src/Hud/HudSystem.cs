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
/// W24 adds the power play tag beside the scorebug (PP 5V4), the shootout panel under it with every
/// round and who scored, and the extra banner lines (hat trick, shutout, game winner, first goal of the season).
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
        if (PowerPlayText(world) is { } pp) DrawPowerPlay(world, art, pp);
        if (OvertimeRules.Shootout(world) is { } so && world.Match.Phase is MatchPhase.Shootout or MatchPhase.Final) DrawShootout(world, art, so);
        if (world.Match.Phase == MatchPhase.Intro) DrawIntro(world, art);
        else if (messages.Current is { } m) DrawBanner(m, art);
    }

    /// <summary>Gets the power play tag, "PP 5V4" or "PP 4V3" from the club with the extra skater, or null at even strength.</summary>
    /// <param name="world">The match state.</param>
    /// <returns>The text, or null.</returns>
    public static string? PowerPlayText(World world)
    {
        if (world.Match.Phase is MatchPhase.Intro or MatchPhase.Shootout or MatchPhase.Final) return null;
        if (Penalties.PowerPlay(world) is not { } pp) return null;
        var other = pp == Side.Home ? Side.Away : Side.Home;
        return $"PP {Skaters(world, pp)}V{Skaters(world, other)}";
    }

    /// <summary>Skaters a club has on the ice by the rules: five less the penalties it serves, never under three.</summary>
    static int Skaters(World world, Side side)
    {
        int full = world.Match.Phase == MatchPhase.Overtime ? OvertimeTuning.OvertimeSkaters : 5;
        return Math.Max(3, full - Penalties.Serving(world, side));
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

    /// <summary>The power play tag right of the scorebug: the crest of the club with the extra skater and PP 5V4.</summary>
    static void DrawPowerPlay(World world, HudArt art, string text)
    {
        var side = Penalties.PowerPlay(world) ?? Side.Home;
        const int pad = 3, crest = 16, gap = 4;
        int h = art.BugSize.H;
        int x = art.BugPosition.X + art.BugSize.W + 3, y = art.BugPosition.Y;
        int w = pad + crest + gap + art.Measure(text) + pad + 1;
        art.DrawPanel("panel_gold", x, y, w, h);
        art.DrawCrest(world.Setup.Club(side).Id, x + pad, y + ((h - crest) / 2));
        int tx = x + pad + crest + gap, ty = y + ((h - 7) / 2) - 2;
        art.DrawText("PP", tx, ty, art.Score);
        art.DrawText(text[2..], tx + art.Measure("PP"), ty, art.Text);
    }

    /// <summary>
    /// The shootout panel under the scorebug: one row per club with its crest, short name and a mark per
    /// round (check scored, cross missed, a dot still to come), then who is shooting or who won.
    /// </summary>
    static void DrawShootout(World world, HudArt art, ShootoutState so)
    {
        const int pad = 3, crest = 16, gap = 4, row = 20, mark = 8, step = 10;
        int rounds = Math.Max(OvertimeTuning.Rounds, Math.Max(so.Taken(Side.Home), so.Taken(Side.Away)));
        int nameW = Math.Max(art.Measure(world.Setup.Home.ShortName), art.Measure(world.Setup.Away.ShortName));
        string foot = Footer(world, so);
        int footH = foot.Length > 0 ? 12 : 0;
        int x = art.BugPosition.X, y = PenaltyTop;
        int marksX = pad + crest + gap + nameW + gap;
        int w = Math.Max(marksX + (rounds * step) + pad - 2, pad + art.Measure(foot) + pad + 1);
        int h = pad + (2 * row) + footH + pad;
        art.DrawPanel("panel", x, y, w, h);

        int ry = y + pad;
        foreach (var side in new[] { Side.Home, Side.Away })
        {
            var club = world.Setup.Club(side);
            art.DrawCrest(club.Id, x + pad, ry + ((row - crest) / 2));
            art.DrawText(club.ShortName, x + pad + crest + gap, ry + ((row - 7) / 2) - 2, art.Text);
            var shots = so.Shots.Where(s => s.Side == side).ToList();
            for (int i = 0; i < rounds; i++)
            {
                int mx = x + marksX + (i * step), my = ry + ((row - mark) / 2);
                if (i < shots.Count) art.DrawIcon(shots[i].Scored ? "check" : "cross", mx, my);
                else Raylib.DrawRectangle(mx + 3, my + 3, 2, 2, StripeColour(world, side));
            }
            ry += row;
        }
        if (footH > 0) art.DrawText(foot, x + pad, ry - 1, art.Score);
    }

    /// <summary>The shootout panel's last line: the shooter up now, or the winner.</summary>
    static string Footer(World world, ShootoutState so)
    {
        if (so.Winner is { } w) return $"{world.Setup.Club(w).ShortName} WIN {so.Goals(w)}-{so.Goals(w == Side.Home ? Side.Away : Side.Home)}";
        if (so.ShooterId >= 0 && so.ShooterId < world.Players.Count)
        {
            var p = world.Players[so.ShooterId];
            return $"ROUND {so.Taken(p.Side) + 1}  #{p.Data.Number} {p.Data.LastName}";
        }
        return "";
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
        var rows = new List<string> { m.Title };
        if (m.Detail.Length > 0) rows.Add(m.Detail);
        rows.AddRange(m.Lines);
        int textW = rows.Max(r => art.Measure(r.ToUpperInvariant()));
        int textH = (rows.Count * line) - 2;
        int inner = Math.Max(textH, m.Crest != null ? crest : 0);
        int w = pad + lead + textW + pad, h = pad + inner + pad;
        int x = (Game.Width - w) / 2, y = BannerTop;
        art.DrawPanel(m.Panel, x, y, w, h);

        int top = y + pad + ((inner - textH) / 2) - 2;     // text y is 2 px above the capitals
        if (m.Crest != null) art.DrawCrest(m.Crest, x + pad, y + pad + ((inner - crest) / 2));
        else if (m.Icon != null) art.DrawIcon(m.Icon, x + pad, top + 2);
        int tx = x + pad + lead;
        bool gold = string.Equals(m.Panel, "panel_gold", StringComparison.Ordinal);
        art.DrawText(m.Title, tx, top, gold ? art.Text : art.Score);
        for (int i = 1; i < rows.Count; i++)
        {
            // the detail in cream; the notes (shutout, game winner, first of the season) in the other colour
            bool note = i >= (m.Detail.Length > 0 ? 2 : 1);
            art.DrawText(rows[i], tx, top + (i * line), note && !gold ? art.Score : art.Text);
        }
    }
}
