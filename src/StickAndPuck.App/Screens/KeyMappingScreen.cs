using System.Collections.Immutable;
using Raylib_cs;
using StickAndPuck.App.Core;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens
{
    /// <summary>Key mapping, opened from the settings screen: one row per <see cref="GameAction"/> with
    /// its keyboard keys and gamepad button. Enter on a row waits for the next key or pad button; a key
    /// replaces the row's keys, a button its pad button, and escape cancels. Reset to defaults clears
    /// every rebinding. Changes apply at once and are saved with the settings on leaving.</summary>
    internal sealed class KeyMappingScreen(AppState app) : MenuScreen(app)
    {
        private const int PanelW = 330,
            PanelTop = 8,
            Visible = 13;

        private static readonly ImmutableArray<KeyboardKey> AllKeys =
        [
            .. Enum.GetValues<KeyboardKey>().Where(k => k != KeyboardKey.Null),
        ];
        private static readonly ImmutableArray<GamepadButton> AllButtons =
        [
            .. Enum.GetValues<GamepadButton>().Where(b => b != GamepadButton.Unknown),
        ];

        private readonly HashSet<KeyboardKey> heldKeys = [];
        private readonly HashSet<(int Pad, GamepadButton Button)> heldButtons = [];
        private Maybe<World> world;
        private Maybe<GameAction> listening;
        private int first;

        private Settings Saved => Flow.Prefs.Settings;

        /// <inheritdoc/>
        public override IReadOnlyList<MenuItem> Items()
        {
            List<MenuItem> items = [];
            foreach (GameAction action in KeyBindings.All)
            {
                items.Add(new MenuItem(Label(action), () => Listen(action)));
            }

            items.Add(
                new MenuItem(Text.Get(K.ResetDefaults), Reset, enabled: Saved.Keys.Count > 0)
            );
            items.Add(new MenuItem(Text.Get(K.Back), Back));
            return items;
        }

        /// <inheritdoc/>
        public override void Update(World world, IReadOnlySet<MenuButton> press)
        {
            this.world = world;
            world.Match.Paused = true;
            if (listening.TryGet(out GameAction action))
            {
                Capture(action);
            }
            else
            {
                base.Update(world, press);
            }
        }

        /// <inheritdoc/>
        public override void Draw(ScreenArt art, World world)
        {
            IReadOnlyList<MenuItem> items = Items();
            int shown = Math.Min(Visible, items.Count);
            first = ClampFirst(Selected, first, shown, items.Count);
            (int x, int top) = art.Panel(
                Text.Get(K.KeyMapping),
                PanelW,
                ((shown + 2) * ScreenArt.Row) + 30,
                PanelTop
            );
            int keysX = x + 150,
                right = x + PanelW - 12;
            art.Left(Text.Get(K.Keyboard), keysX, top, ScreenArt.Muted);
            art.Right(Text.Get(K.Gamepad), right, top, ScreenArt.Muted);
            DrawRows(art, items, x, top + ScreenArt.Row, keysX, right, shown, first, Saved);
            if (first + shown < items.Count)
            {
                art.Centred(
                    Text.Get(K.More),
                    Game.Width / 2,
                    top + ScreenArt.Row + (shown * ScreenArt.Row) + 1,
                    ScreenArt.Muted
                );
            }

            art.Hint(Text.Get(listening.HasValue ? K.HintListening : K.HintKeys));
        }

        private static int ClampFirst(int selected, int first, int shown, int count)
        {
            if (selected < first)
            {
                first = selected;
            }

            if (selected >= first + shown)
            {
                first = selected - shown + 1;
            }

            return Math.Clamp(first, 0, Math.Max(0, count - shown));
        }

        private void DrawRows(
            ScreenArt art,
            IReadOnlyList<MenuItem> items,
            int x,
            int top,
            int keysX,
            int right,
            int shown,
            int first,
            Settings settings
        )
        {
            if (first > 0)
            {
                art.Centred(Text.Get(K.More), Game.Width / 2, top - 3, ScreenArt.Muted);
            }

            int rowTop = top + (ScreenArt.Row / 2);
            for (int i = first; i < first + shown; i++)
            {
                MenuItem item = items[i];
                int y = rowTop + ((i - first) * ScreenArt.Row);
                bool on = i == Selected;
                Color ink = item.Enabled ? art.Text : ScreenArt.Muted;
                if (on && item.Enabled)
                {
                    ink = art.Gold;
                }

                if (on)
                {
                    art.Hud.DrawIcon("arrow_right", x + 6, y + 2);
                }

                art.Left(item.Label, x + 18, y, ink);
                if (i >= KeyBindings.All.Count)
                {
                    continue;
                }

                KeyBinding binding = KeyBindings.Of(settings, KeyBindings.All[i]);
                if (on && listening.HasValue)
                {
                    art.Left(Text.Get(K.PressAKey), keysX, y, art.Gold);
                    art.Right(Text.Get(K.OrButton), right, y, art.Gold);
                    continue;
                }
                art.Left(KeyBindings.KeysName(binding), keysX, y, ink);
                art.Right(KeyBindings.ButtonName(binding.Button), right, y, ink);
            }
        }

        /// <inheritdoc/>
        protected override void Back()
        {
            if (world.TryGet(out World onShow))
            {
                Flow.ApplySettings(onShow);
            }

            Console.WriteLine($"SAP: screens: key mapping saved, {Saved.Keys.Count} rebound");
            Flow.Pop();
        }

        private static string Label(GameAction action)
        {
            return Text.Get(
                action switch
                {
                    GameAction.SkateUp => K.ActionSkateUp,
                    GameAction.SkateDown => K.ActionSkateDown,
                    GameAction.SkateLeft => K.ActionSkateLeft,
                    GameAction.SkateRight => K.ActionSkateRight,
                    GameAction.Sprint => K.ActionSprint,
                    GameAction.Shoot => K.ActionShoot,
                    GameAction.Pass => K.ActionPass,
                    GameAction.Check => K.ActionCheck,
                    GameAction.Deke => K.ActionDeke,
                    GameAction.Switch => K.ActionSwitch,
                    GameAction.LineChange => K.ActionLineChange,
                    GameAction.Block => K.ActionBlock,
                    GameAction.AcceptFight => K.ActionAcceptFight,
                    GameAction.Kit => K.ActionKit,
                    GameAction.Zoom => K.ActionZoom,
                    GameAction.TimeOut => K.ActionTimeOut,
                    GameAction.Pause => K.ActionPause,
                    _ => throw new NotSupportedException(
                        $"Unhandled game action {action} in key mapping"
                    ),
                }
            );
        }

        /// <summary>Starts waiting for a key, ignoring what is already held (the enter that picked the row).</summary>
        private void Listen(GameAction action)
        {
            listening = action;
            heldKeys.Clear();
            heldKeys.UnionWith(AllKeys.Where(DeviceInput.KeyDown));
            heldButtons.Clear();
            heldButtons.UnionWith(Buttons());
        }

        /// <summary>Binds the first key or pad button that goes down; escape cancels.</summary>
        private void Capture(GameAction action)
        {
            if (App.MenuScript.IsScripted)
            {
                listening = Maybe.None<GameAction>(); // a headless script cannot press a key, so the row only shows it waits
                return;
            }
            foreach (KeyboardKey key in AllKeys)
            {
                if (!DeviceInput.KeyDown(key))
                {
                    _ = heldKeys.Remove(key);
                    continue;
                }
                if (!heldKeys.Add(key))
                {
                    continue;
                }

                if (key != KeyboardKey.Escape)
                {
                    Set(KeyBindings.WithKey(Saved, action, key));
                }

                Done();
                return;
            }
            List<(int Pad, GamepadButton Button)> now = Buttons();
            heldButtons.IntersectWith(now);
            foreach ((int Pad, GamepadButton Button) b in now)
            {
                if (!heldButtons.Add(b))
                {
                    continue;
                }

                Set(KeyBindings.WithButton(Saved, action, b.Button));
                Done();
                return;
            }
        }

        /// <summary>Stops waiting. The menu input saw the key go down on this same step, so it is not taken as a press later.</summary>
        private void Done()
        {
            listening = Maybe.None<GameAction>();
        }

        private static List<(int Pad, GamepadButton Button)> Buttons()
        {
            List<(int, GamepadButton)> down = [];
            for (int pad = 0; pad < 2; pad++)
            {
                if (!DeviceInput.PadAvailable(pad))
                {
                    continue;
                }

                down.AddRange(
                    AllButtons.Where(b => DeviceInput.PadDown(pad, b)).Select(b => (pad, b))
                );
            }
            return down;
        }

        private void Reset()
        {
            Set(Saved with { Keys = new Dictionary<GameAction, KeyBinding>() });
            Console.WriteLine("SAP: screens: key mapping reset to defaults");
        }

        private void Set(Settings settings)
        {
            Flow.Prefs = Flow.Prefs with { Settings = settings };
            if (world.TryGet(out World onShow))
            {
                ScreenFlow.Apply(onShow, Flow.Settings);
            }
        }
    }
}
