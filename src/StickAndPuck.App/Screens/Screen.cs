using StickAndPuck.App.Core;

namespace StickAndPuck.App.Screens;

/// <summary>One menu row.</summary>
/// <param name="Label">The text.</param>
/// <param name="Pick">What enter does, or null.</param>
/// <param name="Value">The current value of a setting, shown on the right, or null.</param>
/// <param name="Change">What left (-1) and right (+1) do to the value, or null.</param>
/// <param name="Enabled">Whether the row can be picked.</param>
internal sealed record MenuItem(string Label, Action? Pick = null, string? Value = null, Action<int>? Change = null, bool Enabled = true);

/// <summary>One screen on <see cref="ScreenFlow"/>'s stack. Only the top screen gets the presses and draws.</summary>
internal abstract class Screen
{
    /// <summary>Gets a value indicating whether the rink behind is darkened.</summary>
    public virtual bool Dims => true;

    /// <summary>Handles this step's presses.</summary>
    /// <param name="world">The world on show.</param>
    /// <param name="press">The buttons pressed this step.</param>
    public abstract void Update(World world, IReadOnlySet<MenuButton> press);

    /// <summary>Draws the screen.</summary>
    /// <param name="art">The art.</param>
    /// <param name="world">The world on show.</param>
    public abstract void Draw(ScreenArt art, World world);
}

/// <summary>A screen that is a list of rows: up and down move, enter picks, left and right change a value, back leaves.</summary>
internal abstract class MenuScreen : Screen
{
    /// <summary>Gets the index of the selected row.</summary>
    protected int Selected { get; private set; }

    /// <summary>Gets the rows, built fresh each step so values and enabled states stay current.</summary>
    /// <returns>The rows.</returns>
    public abstract IReadOnlyList<MenuItem> Items();

    /// <summary>What back does; by default leaves the screen.</summary>
    protected virtual void Back()
    {
        ScreenFlow.Pop();
    }

    /// <inheritdoc/>
    public override void Update(World world, IReadOnlySet<MenuButton> press)
    {
        var items = Items();
        if (items.Count == 0) return;
        if (!items[Math.Clamp(Selected, 0, items.Count - 1)].Enabled) Selected = Step(items, Selected, 1);
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
            items[Selected].Change?.Invoke(-1);
        }
        else if (press.Contains(MenuButton.Right))
        {
            items[Selected].Change?.Invoke(1);
        }
        else if (press.Contains(MenuButton.Accept))
        {
            var item = items[Selected];
            if (item.Pick != null) item.Pick();
            else item.Change?.Invoke(1);
        }
        else if (press.Contains(MenuButton.Back))
        {
            Back();
        }
    }

    /// <summary>Selects the row with this label, if there is one and it can be picked.</summary>
    /// <param name="label">The label.</param>
    public void Select(string label)
    {
        var items = Items();
        for (int i = 0; i < items.Count; i++)
            if (items[i].Enabled && string.Equals(items[i].Label, label, StringComparison.Ordinal)) Selected = i;
    }

    static int Step(IReadOnlyList<MenuItem> items, int from, int dir)
    {
        for (int n = 1; n <= items.Count; n++)
        {
            int i = ((from + (dir * n)) % items.Count + items.Count) % items.Count;
            if (items[i].Enabled) return i;
        }
        return from;
    }
}
