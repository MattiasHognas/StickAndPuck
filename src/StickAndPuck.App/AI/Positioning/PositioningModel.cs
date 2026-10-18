using System.Numerics;
using StickAndPuck.App.AI.Positioning.Dev;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match;

namespace StickAndPuck.App.AI.Positioning
{
    /// <summary>What positioning decided this step, per skater, and the way for decisions to ask
    /// for a spot. <see cref="PositioningSystem"/> runs before <see cref="Decisions.DecisionSystem"/> each step:
    /// <list type="number">
    /// <item>Positioning gives every skater a <see cref="Role"/>, a <see cref="Job"/> and a target, and
    /// writes Move and Sprint into each AI skater's command to take it there.</item>
    /// <item>Decisions read <see cref="JobOf"/>, <see cref="TargetOf"/> and so on, then add Pass, Shoot
    /// or Check to the command. To send a skater elsewhere they either rewrite Move right away with
    /// <see cref="Steering.Toward"/>, or call <see cref="Request"/>, which positioning follows from the
    /// next step for as long as the request is renewed every step.</item>
    /// </list></summary>
    public static class PositioningModel
    {
        /// <summary>Gets a skater's role.</summary>
        public static Role RoleOf(World world, Player p)
        {
            return Board(world).Get(p.Id).TryGet(out SkaterPlan plan) ? plan.Role : Role.Centre;
        }

        /// <summary>Gets what a skater is doing this step.</summary>
        public static Job JobOf(World world, Player p)
        {
            return Board(world).Get(p.Id).TryGet(out SkaterPlan plan) ? plan.Job : Job.Idle;
        }

        /// <summary>Gets where a skater is heading this step, after the hockey IQ read and spacing.</summary>
        public static Vector2 TargetOf(World world, Player p)
        {
            return Board(world).Get(p.Id).TryGet(out SkaterPlan plan)
                ? plan.Target
                : new Vector2(p.Position.X, p.Position.Z);
        }

        /// <summary>Gets the ideal spot for a skater's role this step, from where the puck is now, before hockey IQ reads or blurs it.</summary>
        public static Vector2 IdealOf(World world, Player p)
        {
            return Board(world).Get(p.Id).TryGet(out SkaterPlan plan)
                ? plan.Ideal
                : new Vector2(p.Position.X, p.Position.Z);
        }

        /// <summary>Gets a skater's station in his side's attacking-zone cycle.</summary>
        public static Maybe<Station> StationOf(World world, Player p)
        {
            return Board(world).Get(p.Id).TryGet(out SkaterPlan plan)
                ? plan.Station
                : Maybe.None<Station>();
        }

        /// <summary>Gets whether a side is cycling the puck in the attacking zone this step.</summary>
        public static bool Cycling(World world, Side side)
        {
            return Board(world).Cycle[(int)side].Active;
        }

        /// <summary>Gets what a side's cycle came to this match.</summary>
        public static CycleTally CycleOf(World world, Side side)
        {
            return Board(world).Cycle[(int)side].Tally;
        }

        /// <summary>Gets whether a side is attacking or defending this step.</summary>
        public static TeamPhase PhaseOf(World world, Side side)
        {
            return Board(world).Phase[(int)side];
        }

        /// <summary>Asks positioning to send an AI skater to a spot instead of its shape spot. Positioning
        /// follows the request on its next update; renew it every step to keep it, since a request
        /// not renewed for a step lapses and the skater goes back to its job.</summary>
        public static void Request(World world, Player p, Vector2 target, bool sprint = false)
        {
            SkaterPlan plan = Board(world).For(p.Id);
            plan.Request = target;
            plan.RequestSprint = sprint;
            plan.RequestTick = world.Tick;
        }

        /// <summary>Gets whether the AI runs in this world: in a match under the match flow, or under the
        /// positioning scenes' stand-in. Test scenes that script their own skaters are left alone.</summary>
        public static bool Drives(World world)
        {
            return MatchFlow.IsRunning(world) || StandInPlay.Running(world);
        }

        internal static PositioningBoard Board(World world)
        {
            return world.Positioning.Get(() => new PositioningBoard());
        }
    }
}
