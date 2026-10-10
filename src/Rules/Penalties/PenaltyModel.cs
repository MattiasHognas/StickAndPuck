using System.Numerics;
using StickAndPuck.Core;
using StickAndPuck.Players;

namespace StickAndPuck.Rules;

/// <summary>What the referee might call for one check or one chase, and how likely it is.</summary>
/// <param name="PlayerId">The offending skater.</param>
/// <param name="Infraction">One of <see cref="Infraction"/>.</param>
/// <param name="Chance">The chance the referee calls it, before <see cref="Penalties.Strictness"/>.</param>
public readonly record struct Foul(int PlayerId, string Infraction, float Chance);

/// <summary>
/// W14's judgement, pure so tests can ask it directly. A check (W4c's Check event, Side and PlayerId
/// the checker, Position the target) is judged by what it did: a knockdown of a skater without the
/// puck or from behind is tripping; a hit from behind that leaves the carrier standing is hooking; a
/// hard hit away from the puck is slashing; any other check has a small chance of a slash. A
/// defender trailing close behind a fast carrier can be called for hooking a little every second.
/// A skater with a high checking trait hits cleaner: the chance runs from 125 % at 0 to 75 % at 100.
/// </summary>
public static class PenaltyModel
{
    /// <summary>Judges one check event.</summary>
    /// <param name="world">The match, in the step the check landed (after <see cref="CheckingSystem"/>).</param>
    /// <param name="check">The Check event.</param>
    /// <returns>The foul it may be, or null for a clean hit or an unknown target.</returns>
    public static Foul? Judge(World world, MatchEvent check)
    {
        if (check.PlayerId < 0 || check.PlayerId >= world.Players.Count || check.Side is not { } side) return null;
        var checker = world.Players[check.PlayerId];
        var target = world.Players
            .Where(p => p.Side != side && p.OnIce && !p.IsGoalie)
            .MinBy(p => Vector3.DistanceSquared(p.Position, check.Position));
        if (target == null || Vector3.Distance(target.Position, check.Position) > 1f) return null;

        var pk = world.Puck;
        bool downed = target.Action == PlayerAction.Fallen && target.ActionTime < 0.1f;
        bool carrying = pk.Carrier == target.Id || (downed && pk.Carrier < 0 && pk.LastTouch == target.Id);
        bool behind = FromBehind(checker.Position, target);

        string infraction;
        float chance;
        if (downed && (behind || !carrying)) (infraction, chance) = (Infraction.Tripping, PenaltyTuning.TripChance);
        else if (!downed && carrying && behind) (infraction, chance) = (Infraction.Hooking, PenaltyTuning.HookCheckChance);
        else if (check.Strength >= CheckingModel.HardHit && !carrying) (infraction, chance) = (Infraction.Slashing, PenaltyTuning.SlashChance);
        else (infraction, chance) = (Infraction.Slashing, PenaltyTuning.StickChance);
        return new Foul(checker.Id, infraction, chance * Discipline(checker));
    }

    /// <summary>Finds defenders trailing the carrier close enough to hook him this step.</summary>
    /// <param name="world">The match.</param>
    /// <param name="dt">The step length.</param>
    /// <returns>Hooking fouls with this step's chance.</returns>
    public static IEnumerable<Foul> Chases(World world, float dt)
    {
        var pk = world.Puck;
        if (pk.Carrier < 0 || pk.Carrier >= world.Players.Count) yield break;
        var carrier = world.Players[pk.Carrier];
        if (carrier.IsGoalie || carrier.Speed < PenaltyTuning.HookMinSpeed) yield break;
        var ahead = Vector3.Normalize(carrier.Velocity with { Y = 0 });
        foreach (var d in world.Players)
        {
            if (d.Side == carrier.Side || !d.OnIce || d.IsGoalie || d.Action != PlayerAction.None) continue;
            var off = (d.Position - carrier.Position) with { Y = 0 };
            float dist = off.Length();
            if (dist > PenaltyTuning.HookReach || dist < 0.01f) continue;
            if (Vector3.Dot(off / dist, ahead) > -0.4f) continue;
            yield return new Foul(d.Id, Infraction.Hooking, PenaltyTuning.HookRate * dt * Discipline(d));
        }
    }

    /// <summary>Gets whether a hit from <paramref name="from"/> lands on the target's back.</summary>
    /// <param name="from">Where the checker stands.</param>
    /// <param name="target">The skater hit.</param>
    /// <returns>True within <see cref="PenaltyTuning.BehindDegrees"/> of straight behind.</returns>
    public static bool FromBehind(Vector3 from, Player target)
    {
        var push = new Vector2(target.Position.X - from.X, target.Position.Z - from.Z);
        if (push.LengthSquared() < 1e-6f) return false;
        // heading 0 faces +z; the push of a hit from behind goes the way the target faces
        var facing = new Vector2(MathF.Sin(target.Heading), MathF.Cos(target.Heading));
        return Vector2.Dot(Vector2.Normalize(push), facing) > MathF.Cos(PenaltyTuning.BehindDegrees * MathF.PI / 180f);
    }

    /// <summary>Gets how much a skater's checking trait scales his chance of a foul: 1.25 at 0 down to 0.75 at 100.</summary>
    /// <param name="p">The skater.</param>
    /// <returns>The factor.</returns>
    public static float Discipline(Player p)
    {
        return 1.25f - (0.5f * Traits.Unit(p.Data.Traits.Checking));
    }
}
