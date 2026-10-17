using Raylib_cs;
using StickAndPuck.App.Core;
using static Raylib_cs.Raylib;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens;

/// <summary>Team select: every club's crest and name in two columns, and a RULES row under them with
/// the rule preset the match plays. Arrows move, enter picks; down from a column's last club reaches
/// RULES, where left and right pick SHL, ARCADE or NHL (clearing changed rules) and the choice is
/// saved with the settings. A mode fixed to a preset (arcade) shows it greyed out.</summary>
internal sealed class TeamSelectScreen(AppState app, string heading, Maybe<ClubData> taken, Action<ClubData> picked, Maybe<RuleSet> fixedRules = default) : Screen(app)
{
    const int ColW = 150, RowH = 20, PanelW = (ColW * 2) + 24, StrengthH = 40;

    readonly Dictionary<string, ClubStrengths> strengths = new(StringComparer.Ordinal);    // worked out once per club
    int selected = -1;
    bool onRules;

    IReadOnlyList<ClubData> Clubs => Flow.Clubs;

    int Rows => (Clubs.Count + 1) / 2;

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
        if (press.Contains(MenuButton.Up))
        {
            Move(-1);
        }
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
            Flow.Pop();
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
            if (on)
                DrawRectangle(cx - 2, cy - 2, ColW - 4, RowH, new Color(0x42, 0x36, 0x36, 0xff));
            art.Hud.DrawCrest(club.Id, cx, cy);
            Color ink;
            if (club == taken)
                ink = ScreenArt.Muted;
            else if (on)
                ink = art.Gold;
            else
                ink = art.Text;
            art.Left(club.Name, cx + 22, cy + 3, ink);
        }
        DrawRules(art, x, top + (Rows * RowH) + 4);
        DrawStrengths(art, Clubs[selected], x, top + (Rows * RowH) + 4 + RowH);
        string hint;
        if (!onRules)
            hint = Text.Get(K.HintMenu);
        else if (fixedRules.HasValue)
            hint = Text.Get(K.HintOwnRules);
        else
            hint = Text.Get(K.HintPickRules);
        art.Hint(hint);
    }

    /// <summary>Gets the rules the match will play: the mode's own, or the player's choice.</summary>
    RuleSet Rules => fixedRules.TryGet(out var rules) ? rules : RulePicks.Choice(App).Resolve();

    /// <summary>The RULES row: up goes back to the clubs, left and right pick a preset unless the mode fixes it.</summary>
    void RulesRow(IReadOnlySet<MenuButton> press)
    {
        int dir = 0;
        if (press.Contains(MenuButton.Left))
            dir = -1;
        else if (press.Contains(MenuButton.Right) || press.Contains(MenuButton.Accept))
            dir = 1;
        if (press.Contains(MenuButton.Up))
        {
            onRules = false;
        }
        else if (press.Contains(MenuButton.Back))
        {
            Flow.Pop();
        }
        else if (dir != 0 && !fixedRules.HasValue)
        {
            var choice = Flow.Prefs.Rules;
            RulesScreen.Pick(Flow, choice.Custom ? choice.Preset : RulesScreen.Cycle(choice.Preset, dir));    // from CUSTOM the first press goes back to its preset
            ScreenPrefsFile.Write(Flow.Prefs);
            Console.WriteLine($"SAP: screens: team select rules {Rules.Name}");
        }
    }

    void DrawRules(ScreenArt art, int x, int y)
    {
        Color ink;
        if (fixedRules.HasValue)
            ink = ScreenArt.Muted;
        else if (onRules)
            ink = art.Gold;
        else
            ink = art.Text;
        if (onRules)
        {
            DrawRectangle(x + 10, y - 2, PanelW - 20, RowH - 4, new Color(0x42, 0x32, 0x36, 0xff));
            art.Hud.DrawIcon("arrow_right", x + 12, y + 2);
        }
        art.Left(Text.Get(K.Rules), x + 24, y, ink);
        art.Right(onRules && !fixedRules.HasValue ? $"< {Rules.Name} >" : Rules.Name, x + PanelW - 14, y, ink);
    }

    /// <summary>The selected club's strengths as bars in two columns, and how it plays.</summary>
    void DrawStrengths(ScreenArt art, ClubData club, int x, int y)
    {
        if (!strengths.TryGetValue(club.Id, out var s)) strengths[club.Id] = s = ClubStrengths.Of(club);
        DrawRectangle(x + 10, y - 1, PanelW - 20, 1, new Color(0x42, 0x32, 0x36, 0xff));
        (string Label, int Value)[] bars = [(K.Attack, s.Attack), (K.Defence, s.Defence), (K.Speed, s.Speed), (K.Goalie, s.Goalie)];
        for (int i = 0; i < bars.Length; i++)
        {
            int bx = x + 24 + ((i % 2) * ColW), by = y + 3 + ((i / 2) * 11);
            art.Left(Text.Get(bars[i].Label), bx, by, ScreenArt.Muted);
            art.Bar(bars[i].Value, bx + 52, by, 60, art.Gold);
        }
        art.Left(Text.Format(K.Overall, s.Overall), x + 24, y + 26, art.Text);
        art.Right(art.Clip(ClubStrengths.Describe(s.Plays), PanelW - 110), x + PanelW - 14, y + 26, ScreenArt.Muted);
    }

    int Start()
    {
        int last = Flow.Clubs.ToList().FindIndex(c => c != taken && string.Equals(c.Id, Scenes.DefaultSetup(Clubs).Home.Id, StringComparison.Ordinal));
        return last >= 0 ? last : Math.Max(0, Flow.Clubs.ToList().FindIndex(c => c != taken));
    }

    void Move(int dir)
    {
        int n = Clubs.Count, i = selected;
        do i = ((i + dir) % n + n) % n;
        while (Clubs[i] == taken && i != selected);
        selected = i;
    }
}
