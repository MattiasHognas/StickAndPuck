using Raylib_cs;
using StickAndPuck.App.Core;
using static Raylib_cs.Raylib;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens
{
    /// <summary>Team select: every club of the league picked, crest and name in columns (two for the 14 SHL
    /// clubs, four for the 32 NHL clubs), then a LEAGUE row and a RULES row under them. Arrows move, enter
    /// picks; down from a column's last club reaches LEAGUE, where left and right pick SHL or NHL (the club
    /// list, the saves the mode plays and the rules preset follow), and down again reaches RULES, where left
    /// and right pick SHL, ARCADE or NHL rules (clearing changed rules). Both choices are saved with the
    /// settings. A mode fixed to a preset (arcade) shows the rules greyed out; the second pick of a match
    /// shows the league greyed out, so both clubs come from one league.</summary>
    internal sealed class TeamSelectScreen(
        AppState app,
        string heading,
        Maybe<ClubData> taken,
        Action<ClubData> picked,
        Maybe<RuleSet> fixedRules = default
    ) : Screen(app)
    {
        private const int StrengthColW = 150,
            StrengthH = 40;

        /// <summary>First words of the two-word cities in club names (New York, Los Angeles, San Jose, St. Louis, Tampa Bay).</summary>
        private static readonly string[] TwoWordCities = ["New", "Los", "San", "St.", "Tampa"];

        private readonly Dictionary<string, ClubStrengths> strengths =
        [
            with(StringComparer.Ordinal),
        ]; // worked out once per club
        private int selected = -1;
        private Focus focus;

        /// <summary>Which part of the screen the arrows move in.</summary>
        private enum Focus
        {
            Clubs,
            League,
            Rules,
        }

        private IReadOnlyList<ClubData> Clubs => Flow.Clubs;

        /// <summary>Gets a value indicating whether the clubs need the dense four-column grid (the NHL).</summary>
        private bool Dense => Clubs.Count > 16;

        private int Cols => Dense ? 4 : 2;

        private int ColW => Dense ? 113 : 150;

        private int RowH => Dense ? 17 : 20;

        private int OptionH => Dense ? 16 : 18;

        private int PanelW => (ColW * Cols) + 24;

        private int Rows => (Clubs.Count + Cols - 1) / Cols;

        /// <summary>Gets a value indicating whether the league is fixed: the second pick plays the first pick's league.</summary>
        private bool LeagueFixed => taken.HasValue;

        /// <inheritdoc/>
        public override void Update(World world, IReadOnlySet<MenuButton> press)
        {
            if (selected < 0)
            {
                selected = Start();
            }

            if (focus == Focus.League)
            {
                LeagueRow(press);
                return;
            }

            if (focus == Focus.Rules)
            {
                RulesRow(press);
                return;
            }
            int col = selected / Rows,
                row = selected % Rows;
            if (press.Contains(MenuButton.Up))
            {
                Move(-1);
            }
            else if (press.Contains(MenuButton.Down))
            {
                if (row == Rows - 1 || selected + 1 >= Clubs.Count)
                {
                    focus = Focus.League;
                }
                else
                {
                    Move(1);
                }
            }
            else if (press.Contains(MenuButton.Left) || press.Contains(MenuButton.Right))
            {
                int dir = press.Contains(MenuButton.Left) ? -1 : 1;
                for (int step = 1; step < Cols; step++)
                {
                    int other = ((((col + (dir * step)) % Cols) + Cols) % Cols * Rows) + row;
                    if (other < Clubs.Count && Clubs[other] != taken)
                    {
                        selected = other;
                        break;
                    }
                }
            }
            else if (press.Contains(MenuButton.Accept))
            {
                Console.WriteLine(
                    $"SAP: screens: picked {Clubs[selected].Id} ({heading.ToLowerInvariant()}), {Clubs[selected].League}, {Rules.Name} rules"
                );
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
            if (selected < 0)
            {
                selected = Start();
            }

            int h = 5 + ScreenArt.Row + 4 + (Rows * RowH) + 4 + (OptionH * 2) + StrengthH;
            (int x, int top) = art.Panel(
                heading,
                PanelW,
                h,
                Math.Max(4, (Game.Height - h - 18) / 2)
            );
            for (int i = 0; i < Clubs.Count; i++)
            {
                ClubData club = Clubs[i];
                int cx = x + 12 + (i / Rows * ColW),
                    cy = top + (i % Rows * RowH);
                bool on = focus == Focus.Clubs && i == selected;
                if (on)
                {
                    DrawRectangle(
                        cx - 2,
                        cy - 2,
                        ColW - 4,
                        RowH,
                        new Color(0x42, 0x36, 0x36, 0xff)
                    );
                }

                art.Hud.DrawCrest(club.Id, cx, cy);
                Color ink = (club == taken) ? ScreenArt.Muted : (on ? art.Gold : art.Text);
                int textY = cy + 3 - (Dense ? 1 : 0);
                art.Left(art.Clip(GridName(club), ColW - 26), cx + 20, textY, ink);
            }
            int options = top + (Rows * RowH) + 4;
            DrawOption(
                art,
                x,
                options,
                Text.Get(K.League),
                Leagues.Name(Flow.League),
                focus == Focus.League,
                LeagueFixed
            );
            DrawOption(
                art,
                x,
                options + OptionH,
                Text.Get(K.Rules),
                Rules.Name,
                focus == Focus.Rules,
                fixedRules.HasValue
            );
            DrawStrengths(art, Clubs[selected], x, options + (OptionH * 2));
            string hint = focus switch
            {
                Focus.League => Text.Get(LeagueFixed ? K.HintOneLeague : K.HintPickLeague),
                Focus.Rules => fixedRules.HasValue
                    ? Text.Get(K.HintOwnRules)
                    : Text.Get(K.HintPickRules),
                Focus.Clubs => Text.Get(K.HintMenu),
                _ => Text.Get(K.HintMenu),
            };
            art.Hint(hint);
        }

        /// <summary>The name in the grid: the full name in two columns; in four, the name without its
        /// city ("Toronto Maple Leafs" shows MAPLE LEAFS), the full name showing for the selected club.</summary>
        internal static string Nickname(string name)
        {
            string[] words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 2)
            {
                return name;
            }

            int city =
                words.Length > 2 && TwoWordCities.Contains(words[0], StringComparer.Ordinal)
                    ? 2
                    : 1;
            return string.Join(' ', words[city..]);
        }

        private string GridName(ClubData club)
        {
            return Dense ? Nickname(club.Name) : club.Name;
        }

        /// <summary>Gets the rules the match will play: the mode's own, or the player's choice.</summary>
        private RuleSet Rules =>
            fixedRules.TryGet(out RuleSet rules) ? rules : RulePicks.Choice(App).Resolve();

        /// <summary>The LEAGUE row: up goes back to the clubs, down to RULES, left and right pick the league
        /// unless the first pick fixed it.</summary>
        private void LeagueRow(IReadOnlySet<MenuButton> press)
        {
            int dir = Direction(press);
            if (press.Contains(MenuButton.Up))
            {
                focus = Focus.Clubs;
            }
            else if (press.Contains(MenuButton.Down))
            {
                focus = Focus.Rules;
            }
            else if (press.Contains(MenuButton.Back))
            {
                Flow.Pop();
            }
            else if (dir != 0 && !LeagueFixed)
            {
                Flow.PickLeague(Leagues.Next(Flow.League, dir));
                ScreenPrefsFile.Write(Flow.Prefs);
                strengths.Clear();
                selected = Start();
                Console.WriteLine(
                    $"SAP: screens: team select league {Flow.League}, {Clubs.Count} clubs, {Rules.Name} rules"
                );
            }
        }

        /// <summary>The RULES row: up goes back to LEAGUE, left and right pick a preset unless the mode fixes it.</summary>
        private void RulesRow(IReadOnlySet<MenuButton> press)
        {
            int dir = Direction(press);
            if (press.Contains(MenuButton.Up))
            {
                focus = Focus.League;
            }
            else if (press.Contains(MenuButton.Back))
            {
                Flow.Pop();
            }
            else if (dir != 0 && !fixedRules.HasValue)
            {
                RuleChoice choice = Flow.Prefs.Rules;
                RulesScreen.Pick(
                    Flow,
                    choice.Custom ? choice.Preset : RulesScreen.Cycle(choice.Preset, dir)
                ); // from CUSTOM the first press goes back to its preset
                ScreenPrefsFile.Write(Flow.Prefs);
                Console.WriteLine($"SAP: screens: team select rules {Rules.Name}");
            }
        }

        /// <summary>Left is -1; right and enter are +1; anything else 0.</summary>
        private static int Direction(IReadOnlySet<MenuButton> press)
        {
            return press.Contains(MenuButton.Left) ? -1
                : press.Contains(MenuButton.Right) || press.Contains(MenuButton.Accept) ? 1
                : 0;
        }

        private void DrawOption(
            ScreenArt art,
            int x,
            int y,
            string label,
            string value,
            bool on,
            bool locked
        )
        {
            Color ink = locked ? ScreenArt.Muted : (on ? art.Gold : art.Text);
            if (on)
            {
                DrawRectangle(
                    x + 10,
                    y - 2,
                    PanelW - 20,
                    OptionH - 4,
                    new Color(0x42, 0x32, 0x36, 0xff)
                );
                art.Hud.DrawIcon("arrow_right", x + 12, y + 2);
            }
            art.Left(label, x + 24, y, ink);
            art.Right(on && !locked ? $"< {value} >" : value, x + PanelW - 14, y, ink);
        }

        /// <summary>The selected club's strengths as bars in two columns, and how it plays.</summary>
        private void DrawStrengths(ScreenArt art, ClubData club, int x, int y)
        {
            if (!strengths.TryGetValue(club.Id, out ClubStrengths s))
            {
                strengths[club.Id] = s = ClubStrengths.Of(club);
            }

            DrawRectangle(x + 10, y - 1, PanelW - 20, 1, new Color(0x42, 0x32, 0x36, 0xff));
            (string Label, int Value)[] bars =
            [
                (K.Attack, s.Attack),
                (K.Defence, s.Defence),
                (K.Speed, s.Speed),
                (K.Goalie, s.Goalie),
            ];
            for (int i = 0; i < bars.Length; i++)
            {
                int bx = x + 24 + (i % 2 * StrengthColW),
                    by = y + 3 + (i / 2 * 11);
                art.Left(Text.Get(bars[i].Label), bx, by, ScreenArt.Muted);
                art.Bar(bars[i].Value, bx + 52, by, 60, art.Gold);
            }
            if (Dense)
            {
                art.Right(
                    art.Clip(club.Name, PanelW - (StrengthColW * 2) - 40),
                    x + PanelW - 14,
                    y + 3,
                    art.Gold
                ); // the grid shows nicknames; the selected club's full name goes here
            }

            art.Left(Text.Format(K.Overall, s.Overall), x + 24, y + 26, art.Text);
            art.Right(
                art.Clip(ClubStrengths.Describe(s.Plays), PanelW - 110),
                x + PanelW - 14,
                y + 26,
                ScreenArt.Muted
            );
        }

        private int Start()
        {
            int last = Clubs
                .ToList()
                .FindIndex(c =>
                    c != taken
                    && string.Equals(
                        c.Id,
                        Scenes.DefaultSetup(Clubs).Home.Id,
                        StringComparison.Ordinal
                    )
                );
            return last >= 0 ? last : Math.Max(0, Clubs.ToList().FindIndex(c => c != taken));
        }

        private void Move(int dir)
        {
            int n = Clubs.Count,
                i = selected;
            do
            {
                i = (((i + dir) % n) + n) % n;
            } while (Clubs[i] == taken && i != selected);
            selected = i;
        }
    }
}
