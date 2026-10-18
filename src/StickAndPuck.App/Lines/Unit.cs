using System.Globalization;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Lines
{
    /// <summary>A forward line (left wing, centre, right wing) or a defence pair.</summary>
    /// <param name="Group">Forwards or defence.</param>
    /// <param name="Number">1 for the first line or pair.</param>
    /// <param name="Members">The players, in the order they fill spots: C, LW, RW for a line; left, right for a pair.</param>
    public sealed record Unit(Group Group, int Number, IReadOnlyList<Player> Members)
    {
        /// <summary>Gets the unit's name as a bench boss says it: L2 or P1.</summary>
        public string Name =>
            string.Create(
                CultureInfo.InvariantCulture,
                $"{(Group == Group.Forwards ? "L" : "P")}{Number}"
            );

        /// <summary>Gets the members' mean energy.</summary>
        public float Energy => Members.Count == 0 ? 0 : Members.Average(p => p.Energy);
    }
}
