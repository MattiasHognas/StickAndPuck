using Raylib_cs;
using StickAndPuck.Core;
using static Raylib_cs.Raylib;

namespace StickAndPuck.Screens;

/// <summary>"New game": every <see cref="IGameMode"/>, exhibition first. Picking one asks for its clubs, then starts it.</summary>
internal sealed class ModeScreen : MenuScreen
{
    const int PanelW = 200;

    readonly IReadOnlyList<IGameMode> modes = GameModes.All();
    readonly bool board = ArcadeBoardScreen.Any();

    /// <inheritdoc/>
    public override IReadOnlyList<MenuItem> Items()
    {
        var items = modes.Select(m => new MenuItem(m.Title, () => Choose(m))).ToList();
        if (board) items.Add(new MenuItem("ARCADE - BEST RUNS", () => ScreenFlow.Push(new ArcadeBoardScreen())));    // W33
        return items;
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
            string heading = mode.PickHeading(index);
            ScreenFlow.Push(new TeamSelectScreen(heading, index > 0 ? picks[0] : null, club =>
            {
                picks.RemoveRange(index, picks.Count - index);    // back from a later pick and pick again
                picks.Add(club);
                Next();
            }, RulePicks.Fixed(mode)));
        }

        Next();
    }
}

/// <summary>
/// Team select: every club's crest and name in two columns, and a RULES row under them (W44) with
/// the rule preset the match plays. Arrows move, enter picks; down from a column's last club reaches
/// RULES, where left and right pick SHL, ARCADE or NHL (clearing changed rules) and the choice is
/// saved with the settings. A mode fixed to a preset (arcade) shows it greyed out.
/// </summary>
/// <param name="heading">What is being picked.</param>
/// <param name="taken">A club already picked, shown greyed and skipped.</param>
/// <param name="picked">What to do with the pick.</param>
/// <param name="fixedRules">The rules the mode always plays, or null when the player picks.</param>
internal sealed class TeamSelectScreen(string heading, ClubData? taken, Action<ClubData> picked, RuleSet? fixedRules = null) : Screen
{
    const int ColW = 150, RowH = 20, PanelW = (ColW * 2) + 24, StrengthH = 40;

    int selected = -1;
    bool onRules;

    static IReadOnlyList<ClubData> Clubs => ScreenFlow.Clubs;

    static int Rows => (Clubs.Count + 1) / 2;

    /// <inheritdoc/>
    public override void Update(World world, IReadOnlySet<MenuButton> press)
    {
        if (selected < 0) selected = Start();
        if (onRules)
        {
            RulesRow(press);
            return;
        }
        int col = selected / Rows, row = selected % Rows;
        if (press.Contains(MenuButton.Up)) Move(-1);
        else if (press.Contains(MenuButton.Down))
        {
            if (row == Rows - 1 || selected + 1 >= Clubs.Count) onRules = true;
            else Move(1);
        }
        else if (press.Contains(MenuButton.Left) || press.Contains(MenuButton.Right))
        {
            int other = ((1 - col) * Rows) + row;
            if (other < Clubs.Count && Clubs[other] != taken) selected = other;
        }
        else if (press.Contains(MenuButton.Accept))
        {
            Console.WriteLine($"SAP: screens: picked {Clubs[selected].Id} ({heading.ToLowerInvariant()}), {Rules.Name} rules");
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
        int h = 5 + ScreenArt.Row + 4 + (Rows * RowH) + 4 + RowH + StrengthH;
        var (x, top) = art.Panel(heading, PanelW, h, Math.Max(4, (Game.Height - h - 18) / 2));
        for (int i = 0; i < Clubs.Count; i++)
        {
            var club = Clubs[i];
            int cx = x + 12 + ((i / Rows) * ColW), cy = top + ((i % Rows) * RowH);
            bool on = !onRules && i == selected;
            if (on) DrawRectangle(cx - 2, cy - 2, ColW - 4, RowH, new Color(0x42, 0x32, 0x36, 0xff));
            art.Hud.DrawCrest(club.Id, cx, cy);
            var ink = club == taken ? ScreenArt.Muted : on ? art.Gold : art.Text;
            art.Left(club.Name, cx + 22, cy + 3, ink);
        }
        DrawRules(art, x, top + (Rows * RowH) + 4);
        DrawStrengths(art, Clubs[selected], x, top + (Rows * RowH) + 4 + RowH);
        string hint = !onRules ? "ARROWS MOVE   ENTER PICKS   ESC BACK"
            : fixedRules != null ? "THIS MODE PLAYS ITS OWN RULES   UP BACK" : "LEFT RIGHT PICKS THE RULES   UP BACK";
        art.Hint(hint);
    }

    /// <summary>Gets the rules the match will play: the mode's own, or the player's choice.</summary>
    RuleSet Rules => fixedRules ?? RulePicks.Choice.Resolve();

    /// <summary>The RULES row: up goes back to the clubs, left and right pick a preset unless the mode fixes it.</summary>
    void RulesRow(IReadOnlySet<MenuButton> press)
    {
        int dir = press.Contains(MenuButton.Left) ? -1 : press.Contains(MenuButton.Right) || press.Contains(MenuButton.Accept) ? 1 : 0;
        if (press.Contains(MenuButton.Up)) onRules = false;
        else if (press.Contains(MenuButton.Back)) ScreenFlow.Pop();
        else if (dir != 0 && fixedRules == null)
        {
            var choice = ScreenFlow.Prefs.Rules;
            RulesScreen.Pick(choice.Custom ? choice.Preset : RulesScreen.Cycle(choice.Preset, dir));    // from CUSTOM the first press goes back to its preset
            ScreenPrefsFile.Write(ScreenFlow.Prefs);
            Console.WriteLine($"SAP: screens: team select rules {Rules.Name}");
        }
    }

    void DrawRules(ScreenArt art, int x, int y)
    {
        var ink = fixedRules != null ? ScreenArt.Muted : onRules ? art.Gold : art.Text;
        if (onRules)
        {
            DrawRectangle(x + 10, y - 2, PanelW - 20, RowH - 4, new Color(0x42, 0x32, 0x36, 0xff));
            art.Hud.DrawIcon("arrow_right", x + 12, y + 2);
        }
        art.Left("RULES", x + 24, y, ink);
        art.Right(onRules && fixedRules == null ? $"< {Rules.Name} >" : Rules.Name, x + PanelW - 14, y, ink);
    }

    /// <summary>W33: the selected club's strengths as bars in two columns, and how it plays.</summary>
    static void DrawStrengths(ScreenArt art, ClubData club, int x, int y)
    {
        var s = ClubStrengths.Of(club);
        DrawRectangle(x + 10, y - 1, PanelW - 20, 1, new Color(0x42, 0x32, 0x36, 0xff));
        (string Label, int Value)[] bars = [("ATTACK", s.Attack), ("DEFENCE", s.Defence), ("SPEED", s.Speed), ("GOALIE", s.Goalie)];
        for (int i = 0; i < bars.Length; i++)
        {
            int bx = x + 24 + ((i % 2) * ColW), by = y + 3 + ((i / 2) * 11);
            art.Left(bars[i].Label, bx, by, ScreenArt.Muted);
            art.Bar(bars[i].Value, bx + 52, by, 60, art.Gold);
        }
        art.Left($"OVERALL {s.Overall.ToString(System.Globalization.CultureInfo.InvariantCulture)}", x + 24, y + 26, art.Text);
        art.Right(art.Clip(s.Plays, PanelW - 110), x + PanelW - 14, y + 26, ScreenArt.Muted);
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
