using System.Collections.Immutable;
using StickAndPuck.App.Core;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens
{
    /// <summary>The rules page, opened from RULES on the settings screen: PRESET first (SHL, ARCADE, NHL,
    /// or CUSTOM once a rule differs), then every rule the game plays in its group, then RESET TO
    /// PRESET. Left and right change a value; picking a preset clears the changed rules. Changes are
    /// saved with the settings on leaving and play from the next match: a running match keeps the rules
    /// it started with, and a season keeps the rules in its save.</summary>
    internal sealed class RulesScreen(AppState app) : MenuScreen(app)
    {
        private const int PanelW = 300,
            PanelTop = 8,
            Visible = 14;

        private static readonly ImmutableArray<RulePreset> Presets =
        [
            .. Enum.GetValues<RulePreset>(),
        ];

        private int first;

        private RuleChoice Choice => Flow.Prefs.Rules;

        /// <inheritdoc/>
        public override IReadOnlyList<MenuItem> Items()
        {
            RuleSet rules = Choice.Resolve();
            RuleSet preset = RuleSet.Of(Choice.Preset);
            List<MenuItem> items =
            [
                new(
                    Text.Get(K.Preset),
                    value: rules.Name,
                    change: d => Pick(Flow, Cycle(Choice.Preset, d))
                ),
            ];
            Maybe<string> group = Maybe.None<string>();
            foreach (RuleRow row in RuleRows.Shown)
            {
                if (!group.Is(g => string.Equals(row.Group, g, StringComparison.Ordinal)))
                {
                    group = row.Group;
                    items.Add(
                        new MenuItem(
                            Text.Format(K.RuleGroupHeading, Text.Get(row.Group)),
                            enabled: false
                        )
                    );
                }
                string value = row.Get(rules);
                bool changed = !string.Equals(value, row.Get(preset), StringComparison.Ordinal);
                items.Add(
                    new MenuItem(
                        Text.Get(row.Label),
                        value: RuleRow.Show(value) + (changed ? " *" : ""),
                        change: d => Change(row, d)
                    )
                );
            }
            items.Add(
                new MenuItem(
                    Text.Get(K.ResetToPreset),
                    () => Pick(Flow, Choice.Preset),
                    enabled: rules.Custom
                )
            );
            items.Add(new MenuItem(Text.Get(K.Back), Back));
            return items;
        }

        /// <inheritdoc/>
        public override void Update(World world, IReadOnlySet<MenuButton> press)
        {
            world.Match.Paused = true;
            base.Update(world, press);
        }

        /// <inheritdoc/>
        public override void Draw(ScreenArt art, World world)
        {
            IReadOnlyList<MenuItem> items = Items();
            int shown = Math.Min(Visible, items.Count);
            if (Selected < first)
            {
                first = Selected;
            }

            if (Selected >= first + shown)
            {
                first = Selected - shown + 1;
            }

            first = Math.Clamp(first, 0, Math.Max(0, items.Count - shown));

            (int x, int top) = art.Panel(
                Text.Get(K.Rules),
                PanelW,
                ((shown + 1) * ScreenArt.Row) + 30,
                PanelTop
            );
            if (first > 0)
            {
                art.Centred(Text.Get(K.More), Game.Width / 2, top - 3, ScreenArt.Muted);
            }

            top += ScreenArt.Row / 2;
            art.Menu([.. items.Skip(first).Take(shown)], Selected - first, x, PanelW, top);
            if (first + shown < items.Count)
            {
                art.Centred(
                    Text.Get(K.More),
                    Game.Width / 2,
                    top + (shown * ScreenArt.Row) + 1,
                    ScreenArt.Muted
                );
            }

            art.Hint(Text.Get(Flow.Playing.HasValue ? K.HintRulesNextMatch : K.HintRules));
        }

        /// <inheritdoc/>
        protected override void Back()
        {
            ScreenPrefsFile.Write(Flow.Prefs);
            Console.WriteLine($"SAP: screens: rules saved, {Choice.Resolve()}");
            Flow.Pop();
        }

        /// <summary>The preset one step along, wrapping.</summary>
        public static RulePreset Cycle(RulePreset preset, int dir)
        {
            int i = Presets.IndexOf(preset);
            return Presets[(((i + dir) % Presets.Length) + Presets.Length) % Presets.Length];
        }

        /// <summary>Chooses a preset as it stands, clearing every changed rule.</summary>
        public static void Pick(ScreenFlow flow, RulePreset preset)
        {
            flow.Prefs = flow.Prefs with { Rules = RuleChoice.Of(preset) };
        }

        private void Change(RuleRow row, int dir)
        {
            Flow.Prefs = Flow.Prefs with
            {
                Rules = Choice.With(row, row.Next(Choice.Resolve(), dir)),
            };
        }
    }
}
