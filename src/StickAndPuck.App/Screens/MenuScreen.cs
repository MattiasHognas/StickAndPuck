using StickAndPuck.App.Core;

namespace StickAndPuck.App.Screens
{
    /// <summary>A screen that is a list of rows: up and down move, enter picks, left and right change a value, back leaves.</summary>
    /// <param name="app">The game's state the screen shows and changes.</param>
    internal abstract class MenuScreen(AppState app) : Screen(app)
    {
        /// <summary>Gets the index of the selected row.</summary>
        protected int Selected { get; private set; }

        /// <summary>Gets the rows, built fresh each step so values and enabled states stay current.</summary>
        public abstract IReadOnlyList<MenuItem> Items();

        /// <summary>What back does; by default leaves the screen.</summary>
        protected virtual void Back()
        {
            Flow.Pop();
        }

        /// <inheritdoc/>
        public override void Update(World world, IReadOnlySet<MenuButton> press)
        {
            IReadOnlyList<MenuItem> items = Items();
            if (items.Count == 0)
            {
                return;
            }

            if (!items[Math.Clamp(Selected, 0, items.Count - 1)].Enabled)
            {
                Selected = Step(items, Selected, 1);
            }

            Selected = Math.Clamp(Selected, 0, items.Count - 1);
            if (press.Contains(MenuButton.Up))
            {
                Selected = Step(items, Selected, -1);
            }
            else if (press.Contains(MenuButton.Down))
            {
                Selected = Step(items, Selected, 1);
            }
            else if (press.Contains(MenuButton.Left))
            {
                items[Selected].Change(-1);
            }
            else if (press.Contains(MenuButton.Right))
            {
                items[Selected].Change(1);
            }
            else if (press.Contains(MenuButton.Accept))
            {
                items[Selected].Pick();
            }
            else if (press.Contains(MenuButton.Back))
            {
                Back();
            }
        }

        /// <summary>Selects the row with this label, if there is one and it can be picked.</summary>
        public void Select(string label)
        {
            IReadOnlyList<MenuItem> items = Items();
            for (int i = 0; i < items.Count; i++)
            {
                if (
                    items[i].Enabled
                    && string.Equals(items[i].Label, label, StringComparison.Ordinal)
                )
                {
                    Selected = i;
                }
            }
        }

        private static int Step(IReadOnlyList<MenuItem> items, int from, int dir)
        {
            for (int n = 1; n <= items.Count; n++)
            {
                int i = (((from + (dir * n)) % items.Count) + items.Count) % items.Count;
                if (items[i].Enabled)
                {
                    return i;
                }
            }
            return from;
        }
    }
}
