using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Hud;
using StickAndPuck.App.Rules;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Fights;

internal sealed partial class FightSystem
{
    public void DrawHud(World world, RenderContext render)
    {
        if (!art.TryGet(out var hud) || !MatchFlow.IsRunning(world)) return;
        var s = Fighting.State(world);
        if (s.Fight.TryGet(out var fight)) DrawFight(world, hud, fight);
        else if (s.Offer.TryGet(out var offer) && (offer.Slots[0] >= 0 || offer.Slots[1] >= 0)) DrawOffer(world, hud, offer);
    }

    static void DrawOffer(World world, HudArt art, FightOffer offer)
    {
        string title = Text.Get(K.GlovesOff), hint = Text.Get(K.FightHint);
        int w = Math.Max(art.Measure(title), art.Measure(hint)) + 16, h = 30;
        int x = (Game.Width - w) / 2, y = Game.Height - h - 8;
        art.DrawPanel("panel_red", x, y, w, h);
        art.DrawText(title, x + ((w - art.Measure(title)) / 2), y + 3, art.Score);
        art.DrawText(hint, x + ((w - art.Measure(hint)) / 2), y + 15, art.Text);
        // how long is left to accept, as a shrinking line under the panel
        float left = 1f - Math.Clamp((world.Time - offer.Time) / FightTuning.OfferSeconds, 0f, 1f);
        Raylib.DrawRectangle(x + 4, y + h - 3, (int)((w - 8) * left), 1, art.Text);
    }

    static void DrawFight(World world, HudArt art, Fight fight)
    {
        const int w = 300, h = 34, barW = 76;
        int x = (Game.Width - w) / 2, y = Game.Height - h - 6;
        art.DrawPanel("panel", x, y, w, h);
        DrawFighter(world, art, fight.Checker, x + 4, y + 4, w / 2 - 4 - (barW / 2) - 6, left: true);
        DrawFighter(world, art, fight.Victim, x + (w / 2) + (barW / 2) + 6, y + 4, w / 2 - 4 - (barW / 2) - 6, left: false);

        // the timing bar: the sweet spot in the middle, the marker sweeping across
        int bx = x + ((w - barW) / 2), by = y + 6;
        Raylib.DrawRectangle(bx, by, barW, 8, Bar);
        int sweet = (int)(barW * FightTuning.SweetHalf * 2);
        Raylib.DrawRectangle(bx + ((barW - sweet) / 2), by, sweet, 8, Sweet);
        Raylib.DrawRectangle(bx + (barW / 2) - 1, by, 2, 8, Centre);
        if (fight.Stage == FightStage.Exchange) Raylib.DrawRectangle(bx + (int)((barW - 2) * fight.Sweep), by - 2, 2, 12, Marker);

        string label = fight.Stage switch
        {
            FightStage.Grab => Text.Get(K.Fight),
            FightStage.Exchange => Text.Format(K.FightRound, fight.Exchange + 1),
            _ => Text.Get(fight.Knockdown ? K.FightDown : K.FightBrokenUp),
        };
        art.DrawText(label, bx + ((barW - art.Measure(label)) / 2), by + 11, fight.Stage == FightStage.Over ? art.Score : art.Text);
        if (fight.Both.Any(f => f.Slot >= 0) && fight.Stage != FightStage.Over)
        {
            string keys = Text.Get(K.FightKeys);
            art.DrawText(keys, (Game.Width - art.Measure(keys)) / 2, y - 12, art.Text);
        }
    }

    static void DrawFighter(World world, HudArt art, Fighter f, int x, int y, int w, bool left)
    {
        var p = world.Players[f.PlayerId];
        string name = Text.Format(K.PlayerTag, p.Data.Number, p.Data.LastName);
        while (name.Length > 4 && art.Measure(name) > w - 20) name = name[..^1];
        int crestX = left ? x : x + w - 16;
        art.DrawCrest(world.Setup.Club(p.Side).Id, crestX, y);
        int tx = left ? x + 20 : x + w - 20 - art.Measure(name);
        art.DrawText(name, tx, y - 1, art.Text);
        // balance: gold while standing, red as it runs out
        int bw = w - 20, filled = (int)(bw * Math.Clamp(f.Balance, 0f, 1f));
        int bx = left ? x + 20 : x;
        Raylib.DrawRectangle(bx, y + 12, bw, 4, Bar);
        Raylib.DrawRectangle(left ? bx : bx + bw - filled, y + 12, filled, 4, f.Balance > 0.35f ? Health : Hurt);
    }
}
