using Raylib_cs;
using StickAndPuck.Core;
using static Raylib_cs.Raylib;

namespace StickAndPuck.Screens;

/// <summary>"New game": every <see cref="IGameMode"/>, exhibition first. Picking one asks for its clubs, then starts it.</summary>
internal sealed class ModeScreen : MenuScreen
{
    const int PanelW = 200;

    readonly IReadOnlyList<IGameMode> modes = GameModes.All();

    /// <inheritdoc/>
    public override IReadOnlyList<MenuItem> Items()
    {
        return modes.Select(m => new MenuItem(m.Title, () => Choose(m))).ToList();
    }

    /// <inheritdoc/>
    public override void Draw(ScreenArt art, World world)
    {
        var items = Items();
        var (x, top) = art.Panel("NEW GAME", PanelW, (items.Count * ScreenArt.Row) + 30, 70);
        art.Menu(items, Selected, x, PanelW, top);
        art.Hint("ENTER PICKS   ESC BACK");
    }

    /// <summary>Asks for the mode's clubs one at a time, then starts it.</summary>
    /// <param name="mode">The mode.</param>
    public static void Choose(IGameMode mode)
    {
        var picks = new List<ClubData>();
        void Next()
        {
            if (picks.Count >= Math.Clamp(mode.ClubPicks, 0, 2))
            {
                ScreenFlow.PlayMode(mode, picks);
                return;
            }
            int index = picks.Count;
            string heading = index == 0 ? "PICK YOUR CLUB" : "PICK THE OPPONENT";
            ScreenFlow.Push(new TeamSelectScreen(heading, index > 0 ? picks[0] : null, club =>
            {
                picks.RemoveRange(index, picks.Count - index);    // back from a later pick and pick again
                picks.Add(club);
                Next();
            }));
        }

        Next();
    }
}

/// <summary>Team select: every club's crest and name in two columns. Arrows move, enter picks.</summary>
/// <param name="heading">What is being picked.</param>
/// <param name="taken">A club already picked, shown greyed and skipped.</param>
/// <param name="picked">What to do with the pick.</param>
internal sealed class TeamSelectScreen(string heading, ClubData? taken, Action<ClubData> picked) : Screen
{
    const int ColW = 150, RowH = 20, PanelW = (ColW * 2) + 24;

    int selected = -1;

    static IReadOnlyList<ClubData> Clubs => ScreenFlow.Clubs;

    static int Rows => (Clubs.Count + 1) / 2;

    /// <inheritdoc/>
    public override void Update(World world, IReadOnlySet<MenuButton> press)
    {
        if (selected < 0) selected = Start();
        int col = selected / Rows, row = selected % Rows;
        if (press.Contains(MenuButton.Up)) Move(-1);
        else if (press.Contains(MenuButton.Down)) Move(1);
        else if (press.Contains(MenuButton.Left) || press.Contains(MenuButton.Right))
        {
            int other = ((1 - col) * Rows) + row;
            if (other < Clubs.Count && Clubs[other] != taken) selected = other;
        }
        else if (press.Contains(MenuButton.Accept))
        {
            Console.WriteLine($"SAP: screens: picked {Clubs[selected].Id} ({heading.ToLowerInvariant()})");
            picked(Clubs[selected]);
        }
        else if (press.Contains(MenuButton.Back))
        {
            ScreenFlow.Pop();
        }
    }

    /// <inheritdoc/>
    public override void Draw(ScreenArt art, World world)
    {
        if (selected < 0) selected = Start();
        int h = 5 + ScreenArt.Row + 4 + (Rows * RowH) + 4;
        var (x, top) = art.Panel(heading, PanelW, h, Math.Max(4, (Game.Height - h - 18) / 2));
        for (int i = 0; i < Clubs.Count; i++)
        {
            var club = Clubs[i];
            int cx = x + 12 + ((i / Rows) * ColW), cy = top + ((i % Rows) * RowH);
            bool on = i == selected;
            if (on) DrawRectangle(cx - 2, cy - 2, ColW - 4, RowH, new Color(0x42, 0x32, 0x36, 0xff));
            art.Hud.DrawCrest(club.Id, cx, cy);
            var ink = club == taken ? ScreenArt.Muted : on ? art.Gold : art.Text;
            art.Left(club.Name, cx + 22, cy + 3, ink);
        }
        art.Hint("ARROWS MOVE   ENTER PICKS   ESC BACK");
    }

    int Start()
    {
        int last = ScreenFlow.Clubs.ToList().FindIndex(c => c != taken && string.Equals(c.Id, Scenes.DefaultSetup(Clubs).Home.Id, StringComparison.Ordinal));
        return last >= 0 ? last : Math.Max(0, ScreenFlow.Clubs.ToList().FindIndex(c => c != taken));
    }

    void Move(int dir)
    {
        int n = Clubs.Count, i = selected;
        do i = ((i + dir) % n + n) % n;
        while (Clubs[i] == taken && i != selected);
        selected = i;
    }
}

/// <summary>A yes or no question; yes runs the action, no goes back.</summary>
/// <param name="question">The question.</param>
/// <param name="yes">What yes does.</param>
internal sealed class ConfirmScreen(string question, Action yes) : MenuScreen
{
    /// <inheritdoc/>
    public override IReadOnlyList<MenuItem> Items()
    {
        return [new MenuItem("NO", ScreenFlow.Pop), new MenuItem("YES", yes)];
    }

    /// <inheritdoc/>
    public override void Draw(ScreenArt art, World world)
    {
        int w = Math.Max(160, art.Hud.Measure(question.ToUpperInvariant()) + 30);
        var (x, top) = art.Panel(question, w, 5 + ScreenArt.Row + 2 + (2 * ScreenArt.Row) + 8, 100, "panel_red");
        art.Menu(Items(), Selected, x, w, top);
    }
}
