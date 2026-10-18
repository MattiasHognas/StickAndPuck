using StickAndPuck.App.Core;

namespace StickAndPuck.App.AI.Positioning
{
    /// <summary>Gives each skater on the ice a role from their roster position, one of each per side.</summary>
    public static class Roles
    {
        /// <summary>The roles used for a number of skaters: all five at even strength, a box with four, a triangle with three.</summary>
        public static IReadOnlyList<Role> For(int skaters)
        {
            return skaters switch
            {
                >= 5 =>
                [
                    Role.Centre,
                    Role.LeftWing,
                    Role.RightWing,
                    Role.LeftDefence,
                    Role.RightDefence,
                ],
                4 => [Role.LeftWing, Role.RightWing, Role.LeftDefence, Role.RightDefence],
                3 => [Role.Centre, Role.LeftDefence, Role.RightDefence],
                2 => [Role.Centre, Role.RightDefence],
                _ => [Role.Centre],
            };
        }

        /// <summary>Hands out roles to one side's skaters: each takes the role of their roster position when it
        /// is free (a defenceman takes the side he shoots from), then the nearest kind of role (forward
        /// or defence), then whatever is left. Skaters past the role count share the centre role.</summary>
        public static IReadOnlyDictionary<int, Role> Assign(IReadOnlyList<Player> skaters)
        {
            List<Role> free = [.. For(skaters.Count)];
            Dictionary<int, Role> result = [];
            bool Take(Player p, Role r)
            {
                if (result.ContainsKey(p.Id) || !free.Remove(r))
                {
                    return false;
                }

                result[p.Id] = r;
                return true;
            }

            foreach (Player p in skaters)
            {
                _ = Take(p, Preferred(p.Data));
            }

            foreach (Player p in skaters.Where(p => !result.ContainsKey(p.Id)).Select(p => p))
            {
                Role[] roles = Kin(p.Data.Position);
                _ = roles.Any(r => Take(p, r));
            }
            foreach (Player p in skaters.Where(p => !result.ContainsKey(p.Id)))
            {
                result[p.Id] = free.Count > 0 && Take(p, free[0]) ? result[p.Id] : Role.Centre;
            }

            return result;
        }

        /// <summary>Gets whether a role is a defence role.</summary>
        public static bool IsDefence(Role role)
        {
            return role is Role.LeftDefence or Role.RightDefence;
        }

        private static Role Preferred(PlayerData d)
        {
            return d.Position switch
            {
                PlayingPosition.LW => Role.LeftWing,
                PlayingPosition.RW => Role.RightWing,
                PlayingPosition.D => d.Shoots == Hand.Left ? Role.LeftDefence : Role.RightDefence,
                PlayingPosition.C or PlayingPosition.G => Role.Centre,
                _ => throw new NotSupportedException($"No preferred role for {d.Position}"),
            };
        }

        private static Role[] Kin(PlayingPosition position)
        {
            return position == PlayingPosition.D
                ? [Role.LeftDefence, Role.RightDefence]
                : [Role.Centre, Role.LeftWing, Role.RightWing];
        }
    }
}
