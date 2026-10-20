using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players.Puck;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Rules.Overtime;

namespace StickAndPuck.App.Rules.Penalties
{
    /// <summary>The penalty shot (<see cref="RuleSet.PenaltyShot"/>): a trip, hook or slash from behind on a skater on a
    /// clean breakaway (<see cref="Breakaway"/>) is not a minor. The referee blows the whistle at once and awards the
    /// shot (<see cref="PenaltyShotAwarded"/>): the other skaters leave the ice and the fouled skater takes it alone
    /// from centre against the goalie, the shootout's attempt (<see cref="ShootoutAttempt"/>), a human of his club
    /// shooting for him. The clock stands still throughout. A goal counts like any other and the faceoff after it is
    /// at centre; after a miss the skaters come back and play resumes with a faceoff at centre too. No penalty is
    /// served either way. <see cref="PenaltyRules"/> runs it each step while play is stopped for it.</summary>
    public static class PenaltyShot
    {
        /// <summary>Gets whether a penalty shot is under way.</summary>
        public static bool Running(World world)
        {
            return world.Penalties.TryGet(out PenaltyBook book) && book.Shot.HasValue;
        }

        /// <summary>Gets the skater taking the penalty shot under way, or None.</summary>
        public static Maybe<Player> Shooter(World world)
        {
            return
                world.Penalties.TryGet(out PenaltyBook book)
                && book.Shot.TryGet(out PenaltyShotState shot)
                ? world.Players[shot.ShooterId]
                : Maybe.None<Player>();
        }

        /// <summary>Gets whether a skater is on a clean breakaway: on the ice in his attacking half, short of the goal
        /// line, with no opposing skater between him and the goal, only the goalie. An opponent at most
        /// <see cref="PenaltyTuning.BreakawayLevel"/> nearer the goal is level with him, not in the way.</summary>
        public static bool Breakaway(World world, Player carrier)
        {
            if (carrier.IsGoalie || !carrier.OnIce)
            {
                return false;
            }

            int attack = PuckHandling.AttackEnd(world, carrier.Side);
            float ahead = attack * carrier.Position.X;
            return ahead is > 0 and < RinkSpec.GoalLineX
                && !world.Players.Any(p =>
                    p.Side != carrier.Side
                    && p.OnIce
                    && !p.IsGoalie
                    && (attack * p.Position.X) > ahead + PenaltyTuning.BreakawayLevel
                );
        }

        /// <summary>Gets whether a foul by <paramref name="offender"/> on <paramref name="victim"/> came from behind: from
        /// his own end of the ice (behind the victim on the way to the goal) or on the victim's back.</summary>
        public static bool FromBehind(World world, Player offender, Player victim)
        {
            int attack = PuckHandling.AttackEnd(world, victim.Side);
            return attack * (victim.Position.X - offender.Position.X) > 0
                || PenaltyModel.FromBehind(offender.Position, victim);
        }

        /// <summary>Gets the skater a call on <paramref name="offenderId"/> gives a penalty shot to, or None when it stays a
        /// call: the rule and penalties on, play running, a minor for tripping, hooking or slashing by a skater, from
        /// behind on the puck carrier (or the skater just tripped off it) on a clean breakaway.</summary>
        public static Maybe<Player> Awarded(
            World world,
            int offenderId,
            string infraction,
            Severity severity = Severity.Minor
        )
        {
            if (
                !world.Setup.Rules.PenaltyShot
                || !world.Settings.Penalties
                || !MatchFlow.IsRunning(world)
                || world.Match.Phase != MatchPhase.Play
                || Running(world)
                || severity != Severity.Minor
                || infraction
                    is not (Infraction.Tripping or Infraction.Hooking or Infraction.Slashing)
                || offenderId < 0
                || offenderId >= world.Players.Count
            )
            {
                return Maybe.None<Player>();
            }

            Player offender = world.Players[offenderId];
            return
                !offender.IsGoalie
                && Victim(world, offender).TryGet(out Player victim)
                && Breakaway(world, victim)
                && FromBehind(world, offender, victim)
                ? victim
                : Maybe.None<Player>();
        }

        /// <summary>Awards a penalty shot for the call instead of signalling it, when <see cref="Awarded"/> says so: the
        /// whistle, the referee's signal, the <see cref="PenaltyShotAwarded"/> event; false when it stays a call.</summary>
        public static bool Award(
            World world,
            int offenderId,
            string infraction,
            Severity severity = Severity.Minor
        )
        {
            if (
                !Awarded(world, offenderId, infraction, severity).TryGet(out Player shooter)
                || !MatchFlow.Whistle(world, RinkSpec.CentreDot)
            )
            {
                return false;
            }

            PenaltyBook book = PenaltyCalls.Book(world);
            Player offender = world.Players[offenderId];
            MatchFlow.State(world).Timer = float.PositiveInfinity; // the shot ends the stoppage, not the clock
            book.Shot = new PenaltyShotState
            {
                ShooterId = shooter.Id,
                OffenderId = offenderId,
                Stage = PenaltyShotStage.Whistle,
                Timer = PenaltyTuning.ShotWhistleSeconds,
            };
            Referee.RefereeState.Announce(world, infraction, offenderId);
            world.Events.Publish(
                new PenaltyShotAwarded(
                    world.Time,
                    shooter.Side,
                    shooter.Id,
                    offenderId,
                    shooter.Position
                )
            );
            PenaltyBook.Log(
                world,
                $"{infraction} on a breakaway by {PenaltyCalls.Name(world, offender)}: penalty shot for {PenaltyCalls.Name(world, shooter)}"
            );
            return true;
        }

        /// <summary>Runs the penalty shot under way one step; nothing without one.</summary>
        internal static void Step(World world, PenaltyBook book, float dt)
        {
            if (!book.Shot.TryGet(out PenaltyShotState shot))
            {
                return;
            }

            FlowState flow = MatchFlow.State(world);
            if (world.Match.Phase != MatchPhase.Stopped)
            {
                // something else took over the stoppage (the match ended): bring the skaters back
                Restore(world, book, shot);
                return;
            }

            flow.Timer = float.PositiveInfinity;
            Player shooter = world.Players[shot.ShooterId];
            switch (shot.Stage)
            {
                case PenaltyShotStage.Whistle:
                    if (Wait(shot, dt))
                    {
                        ClearIce(world, shot, shooter);
                        shot.Stage = PenaltyShotStage.Ready;
                        shot.Timer = OvertimeTuning.ReadySeconds;
                    }

                    break;
                case PenaltyShotStage.Ready:
                    ShootoutAttempt.Hold(world, shooter);
                    if (Wait(shot, dt))
                    {
                        shot.Stage = PenaltyShotStage.Attempt;
                        shot.Attempt.Go(world, shooter, book.ShotRng);
                    }

                    break;
                case PenaltyShotStage.Attempt:
                    Attempt(world, book, shot, shooter, dt);
                    break;
                case PenaltyShotStage.Result:
                    if (Wait(shot, dt))
                    {
                        Restore(world, book, shot);
                        flow.Dot = RinkSpec.CentreDot;
                        flow.Timer = MatchTuning.StoppageSeconds;
                    }

                    break;
                default:
                    break;
            }
        }

        /// <summary>Watches the shot: saves on net count as shots; a goal counts at once and play restarts at centre after it.</summary>
        private static void Attempt(
            World world,
            PenaltyBook book,
            PenaltyShotState shot,
            Player shooter,
            float dt
        )
        {
            MatchState m = world.Match;
            foreach (ShotSaved save in world.Events.Of<ShotSaved>().Where(Goalie.GoalieModel.OnNet))
            {
                if (save.Side != shooter.Side)
                {
                    m.Shots[(int)shooter.Side]++;
                }
            }

            if (!shot.Attempt.Step(world, shooter, book.ShotRng, dt).TryGet(out bool scored))
            {
                return;
            }

            world.Puck.Live = false;
            PenaltyBook.Log(
                world,
                $"penalty shot {(scored ? "scores" : "misses")}, {PenaltyCalls.Name(world, shooter)}"
            );
            if (!scored)
            {
                shot.Stage = PenaltyShotStage.Result;
                shot.Timer = PenaltyTuning.ShotResultSeconds;
                return;
            }

            // a goal like any other: the goal pause, then the faceoff at centre; sudden death after regulation
            Restore(world, book, shot);
            FlowState flow = MatchFlow.State(world);
            m.Score[(int)shooter.Side]++;
            m.Shots[(int)shooter.Side]++;
            m.Phase = MatchPhase.Goal;
            flow.Dot = RinkSpec.CentreDot;
            flow.Timer = MatchTuning.GoalSeconds;
            if (m.Period > world.Setup.Periods)
            {
                MatchRules.EndMatch(world, flow);
            }
        }

        /// <summary>Gets the skater the foul was on: the puck carrier, or the one who just lost it with the puck still near him.</summary>
        private static Maybe<Player> Victim(World world, Player offender)
        {
            Puck pk = world.Puck;
            int id = pk.Carrier >= 0 ? pk.Carrier : pk.LastTouch;
            if (id < 0 || id >= world.Players.Count)
            {
                return Maybe.None<Player>();
            }

            Player victim = world.Players[id];
            bool near =
                pk.Carrier == id
                || Vector3.Distance(pk.Position with { Y = 0 }, victim.Position with { Y = 0 })
                    <= PenaltyTuning.BreakawayPuckReach;
            return victim.Side != offender.Side && !victim.IsGoalie && near
                ? victim
                : Maybe.None<Player>();
        }

        /// <summary>Sends every skater but the shooter off the ice; a human of the shooter's club takes him over.</summary>
        private static void ClearIce(World world, PenaltyShotState shot, Player shooter)
        {
            shot.ShooterSlot = shooter.Slot;
            foreach (
                Player p in world.Players.Where(p => p.OnIce && !p.IsGoalie && p.Id != shooter.Id)
            )
            {
                shot.Benched.Add((p.Id, p.Slot));
                if (p.Slot >= 0 && p.Side == shooter.Side && shooter.Slot < 0)
                {
                    shooter.Slot = p.Slot;
                }

                p.OnIce = false;
                p.Slot = -1;
                p.Velocity = Vector3.Zero;
                p.Command = default;
                p.Begin(PlayerAction.None);
            }

            shooter.Velocity = Vector3.Zero;
            shooter.Command = default;
            shooter.Begin(PlayerAction.None);
            ShootoutAttempt.Hold(world, shooter);
        }

        /// <summary>Brings the skaters back on the ice (not those sent to the box meanwhile) with their humans, and ends the shot.</summary>
        private static void Restore(World world, PenaltyBook book, PenaltyShotState shot)
        {
            Player shooter = world.Players[shot.ShooterId];
            if (shot.Stage != PenaltyShotStage.Whistle)
            {
                shooter.Slot = shot.ShooterSlot;
            }

            foreach ((int id, int slot) in shot.Benched)
            {
                Player p = world.Players[id];
                p.Slot = slot;
                if (!PenaltyCalls.InBox(world, id))
                {
                    p.OnIce = true;
                }
            }

            shooter.Command = default;
            book.Shot = Maybe.None<PenaltyShotState>();
        }

        private static bool Wait(PenaltyShotState shot, float dt)
        {
            shot.Timer -= dt;
            return shot.Timer <= 0;
        }
    }
}
