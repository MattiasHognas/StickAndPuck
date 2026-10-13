using StickAndPuck.App.Core;

namespace StickAndPuck.App.AI;

/// <summary>Where a skater plays in the team's shape. Left and right are as seen skating toward the attacked net.</summary>
public enum Role
{
    /// <summary>Centre: the middle lane, high slot in attack, low slot in defence.</summary>
    Centre,

    /// <summary>Left wing: the left lane, covers the left point in defence.</summary>
    LeftWing,

    /// <summary>Right wing: the right lane, covers the right point in defence.</summary>
    RightWing,

    /// <summary>Left defence: the left point in attack, the left side of the net in defence.</summary>
    LeftDefence,

    /// <summary>Right defence: the right point in attack, the right side of the net in defence.</summary>
    RightDefence,
}

/// <summary>What an AI skater is doing this step, which decides where it skates.</summary>
public enum Job
{
    /// <summary>Play is stopped: glide to a halt.</summary>
    Idle,

    /// <summary>Holding the role's spot in the team shape.</summary>
    Shape,

    /// <summary>
    /// Off the puck with a teammate carrying it in attack: getting open for a pass around the
    /// shape spot or the cycle station (W22a), so on purpose away from the shape's ideal spot.
    /// </summary>
    Support,

    /// <summary>Lining up for a faceoff around the puck.</summary>
    Faceoff,

    /// <summary>Carrying the puck up ice (until decisions take over).</summary>
    Carry,

    /// <summary>Going for a loose puck: the team's quickest skater to it.</summary>
    Chase,

    /// <summary>Closing down the opposing carrier from the net side.</summary>
    Pressure,

    /// <summary>Skating to a spot asked for with <see cref="Positioning.Request"/>.</summary>
    Requested,

    /// <summary>A human controls this skater; it keeps its role but positioning leaves it alone.</summary>
    Human,
}

/// <summary>Gives each skater on the ice a role from their roster position, one of each per side.</summary>
public static class Roles
{
    /// <summary>The roles used for a number of skaters: all five at even strength, a box with four, a triangle with three.</summary>
    /// <param name="skaters">Skaters on the ice for one side.</param>
    /// <returns>The roles to hand out.</returns>
    public static IReadOnlyList<Role> For(int skaters)
    {
        return skaters switch
        {
            >= 5 => [Role.Centre, Role.LeftWing, Role.RightWing, Role.LeftDefence, Role.RightDefence],
            4 => [Role.LeftWing, Role.RightWing, Role.LeftDefence, Role.RightDefence],
            3 => [Role.Centre, Role.LeftDefence, Role.RightDefence],
            2 => [Role.Centre, Role.RightDefence],
            _ => [Role.Centre],
        };
    }

    /// <summary>
    /// Hands out roles to one side's skaters: each takes the role of their roster position when it
    /// is free (a defenceman takes the side he shoots from), then the nearest kind of role (forward
    /// or defence), then whatever is left. Skaters past the role count share the centre role.
    /// </summary>
    /// <param name="skaters">One side's skaters on the ice, goalies left out.</param>
    /// <returns>The role per player id.</returns>
    public static Dictionary<int, Role> Assign(IReadOnlyList<Player> skaters)
    {
        var free = For(skaters.Count).ToList();
        var result = new Dictionary<int, Role>();
        bool Take(Player p, Role r)
        {
            if (result.ContainsKey(p.Id) || !free.Remove(r)) return false;
            result[p.Id] = r;
            return true;
        }

        foreach (var p in skaters) Take(p, Preferred(p.Data));
        foreach (var p in skaters)
        {
            if (result.ContainsKey(p.Id)) continue;
            foreach (var r in Kin(p.Data.Position)) if (Take(p, r)) break;
        }
        foreach (var p in skaters)
            if (!result.ContainsKey(p.Id)) result[p.Id] = free.Count > 0 && Take(p, free[0]) ? result[p.Id] : Role.Centre;
        return result;
    }

    /// <summary>Gets whether a role is a defence role.</summary>
    /// <param name="role">The role.</param>
    /// <returns>True for left and right defence.</returns>
    public static bool IsDefence(Role role)
    {
        return role is Role.LeftDefence or Role.RightDefence;
    }

    static Role Preferred(PlayerData d)
    {
        return d.Position switch
        {
            PlayingPosition.LW => Role.LeftWing,
            PlayingPosition.RW => Role.RightWing,
            PlayingPosition.D => d.Shoots == Hand.Left ? Role.LeftDefence : Role.RightDefence,
            _ => Role.Centre,
        };
    }

    static Role[] Kin(PlayingPosition position)
    {
        return position == PlayingPosition.D
            ? [Role.LeftDefence, Role.RightDefence]
            : [Role.Centre, Role.LeftWing, Role.RightWing];
    }
}
