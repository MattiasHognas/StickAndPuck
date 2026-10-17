using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Season;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens;

/// <summary>A game settled with "sim this game" (<see cref="SeasonSession.SimCurrentGame"/>):
/// the stage, both crests and the score with how it was decided, then the next step. When the sim
/// decided the final, CHAMPIONS opens the trophy screen.</summary>
internal sealed class SimResultScreen(AppState app, SimmedGame game, bool crowned) : MenuScreen(app)
{
    const int PanelW = 300;

    /// <inheritdoc/>
    public override IReadOnlyList<MenuItem> Items()
    {
        List<MenuItem> items = [];
        if (crowned) items.Add(new MenuItem(Text.Get(K.Champions), () => Flow.Replace(TrophyScreen.Season(App))));
        items.Add(new MenuItem(Text.Get(K.Continue), Back));
        if (Flow.CanSimGame()) items.Add(new MenuItem(Text.Get(K.SimNextGame), Flow.SimSeasonGame));
        if (Flow.SeasonHasNextGame()) items.Add(new MenuItem(Text.Get(K.PlayNextGame), Flow.PlaySeasonGame));
        return items;
    }

    /// <inheritdoc/>
    public override void Draw(ScreenArt art, World world)
    {
        var items = Items();
        var clubs = Flow.Clubs;
        int h = 5 + ScreenArt.Row + 2 + 24 + 14 + 6 + (items.Count * ScreenArt.Row) + 6;
        string kind = game.Result.Kind switch { ResultKind.Overtime => Text.Get(K.KindOt), ResultKind.Shootout => Text.Get(K.KindShootout), _ => "" };
        var (x, top) = art.Panel(Text.Format(K.SimmedHeading, game.Stage, kind), PanelW, h, Math.Max(4, (Game.Height - h - 16) / 2));
        int cx = Game.Width / 2;
        bool homeWon = game.Result.HomeGoals > game.Result.AwayGoals;
        art.Hud.DrawCrest(game.Home, cx - 30 - 16, top);
        art.Hud.DrawCrest(game.Away, cx + 30, top);
        art.Centred($"{N(game.Result.HomeGoals)} - {N(game.Result.AwayGoals)}", cx, top + 3, art.Gold);
        art.Right(art.Clip(Name(clubs, game.Home), cx - 52 - x - 8), cx - 30 - 22, top + 3, homeWon ? art.Gold : art.Text);
        art.Left(art.Clip(Name(clubs, game.Away), x + PanelW - 8 - (cx + 52)), cx + 30 + 22, top + 3, homeWon ? art.Text : art.Gold);
        top += 24;
        art.Centred(Text.Get(K.SettledOnPaper), cx, top, ScreenArt.Muted);
        top += 14 + 6;
        art.Menu(items, Selected, x + 60, PanelW - 120, top);
    }

    static string Name(IReadOnlyList<ClubData> clubs, string id) =>
        clubs.FirstOrNone(c => string.Equals(c.Id, id, StringComparison.Ordinal)).Select(c => c.Name).Or(id);

    static string N(int n) => n.ToString(CultureInfo.InvariantCulture);
}
