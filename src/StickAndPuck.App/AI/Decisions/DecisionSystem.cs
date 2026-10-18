using System.Numerics;
using StickAndPuck.App.AI.Decisions.Dev;
using StickAndPuck.App.AI.Positioning;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players.Puck;
using StickAndPuck.App.Rules.Offside;
using StickAndPuck.App.Rules.Penalties;

namespace StickAndPuck.App.AI.Decisions
{
    /// <summary>What each AI skater does with the puck and around it, added to the move positioning
    /// chose. It writes PlayerCommand like a human's input, so puck handling, checking and
    /// skating never know who is in control.
    /// <list type="bullet">
    /// <item>Carrier: every look (quicker for a smart passer) weighs carrying on, shooting and each
    /// pass on one scale (see <see cref="Choices"/>), and does the best. Smart passes sets how far
    /// the carrier looks, how well they read the defenders and how much their judgement wobbles, so a
    /// smart club finds open teammates up the ice and a poor one passes into sticks.</item>
    /// <item>Shots: a wrist shot in close or under pressure, a slap shot from the point with time,
    /// aimed away from where the goalie leans. Shot accuracy makes a skater shoot from further out.</item>
    /// <item>Dekes and one-timers: a carrier skating at a defender may deke him, better timed with more
    /// hockey IQ; the teammate a pass is meant for may one-time it from a good shooting spot.</item>
    /// <item>Chase: the teammate a pass is meant for skates onto its line to meet it, and after a
    /// shot the forward nearest the net crashes it for the rebound. Loose pucks are chased by
    /// positioning's chaser.</item>
    /// <item>Checks: the defender pressing the carrier decides once per approach whether to finish the
    /// check, likelier with a better checking trait, then skates through the carrier and checks.</item>
    /// <item>Every look, pass and shot is rolled with <see cref="Jitter"/>: a fumbled look is
    /// read like a much weaker player, a fumbled pass goes a beat late, a fumbled shot picks the
    /// goalie's side; a lucky roll plays it like a star. In the attacking-zone cycle the carrier
    /// values keeping the puck and fears a turnover more, so the club passes it round until a lane
    /// to a good shot opens; passes never go through a net's cage.</item>
    /// <item>A carrier pinned in his own zone with no safe pass sometimes fires the puck out, hard
    /// up the boards: untouched from his own half, that is icing. A poor or rushed skater does it far
    /// more often than a smart one. A carrier coming up to the
    /// attacking blue line with a teammate already over it may hold up short of the line so he can
    /// tag up; a smart, unhurried one usually does, a poor or pressed one carries on in, offside.
    /// Neither care applies with the rule switched off (Arcade).</item>
    /// <item>Every write goes through <see cref="AiCommand"/>, which says who owns each field. Per
    /// skater, one branch of <see cref="Update"/> runs, in this order: a committed shot is held; a
    /// delayed pass goes; a carrier holds up at the line (unless special teams or tactics claimed him
    /// this step) and looks at the play; a receiver meets the pass; the chaser pounces; the crasher
    /// goes to the net; a presser hits. A carrier may deke before his look, and the receiver may hold shoot for a one-timer. Candidate passes are weighed without touching anyone.</item>
    /// <item>After a human's pass, control follows the puck to a receiver who would otherwise
    /// skate off on the stick still held for the pass. Until he or anyone touches the puck, or the
    /// pass dies, his Move and Sprint are replaced with a skate to where his blade meets the puck's
    /// slide (see Receive; once the puck has slowed, a skate onto it), until the player gives fresh stick input: the stick let go
    /// to rest and pushed again, or turned more than <see cref="FreshTurn"/> from where it was held.
    /// His buttons stay his, so a held shoot still arms a one-timer. This is the one write the AI
    /// makes to a human's command, done here rather than through <see cref="AiCommand"/>.</item>
    /// </list></summary>
    /// <remarks>Initializes the system with its own randomness, so tests repeat.</remarks>
    internal sealed partial class DecisionSystem(Random rng) : ISystem
    {
        private const float CheckFrom = 3.2f; // distance at which a presser decides whether to hit
        private const float ShootFrom = 0.64f; // a shot worth less than this is not taken, unless the carrier has stood still with the puck
        private const float SafePass = 0.55f; // a pass the carrier gives at least this chance is a way out of his zone; with none he may clear it
        private const float HoldFrom = 4f; // metres short of the attacking blue line at which a carrier looks for teammates already over it
        private const float HoldFor = 1.6f; // longest a carrier holds up at the line before carrying on in
        private const float Pushed = 0.3f; // a stick pushed less than this is at rest, as puck handling reads it

        /// <summary>The stick turned this far from where it was held for a human's pass, in radians, steers the receiver.</summary>
        private const float FreshTurn = 45f * MathF.PI / 180f;

        /// <summary>The dice seed without a match seed.</summary>
        internal const int Fallback = 5;

        private readonly Random rng = rng;

        public DecisionSystem()
            : this(Seeds.Dice(Seeds.Environment, "decisions", Fallback)) { }

        public void Update(World world, float dt)
        {
            DecisionBoard board = DecisionState.Board(world);
            MovesDrill.Restart(world, board);
            Track(world, board);
            if (!PositioningModel.Drives(world))
            {
                return;
            }

            Puck pk = world.Puck;
            if (world.Match.Phase is not (MatchPhase.Play or MatchPhase.Overtime) || !pk.Live)
            {
                board.ForgetShots();
                return;
            }

            MeetForHuman(world, board);
            Decide(world, board, Crasher(world, board));
            MovesDrill.Pass(world, board);
        }

        private void Decide(World world, DecisionBoard board, int crasher)
        {
            Puck pk = world.Puck;
            foreach (Player p in world.Players)
            {
                if (
                    !p.OnIce
                    || p.IsGoalie
                    || p.Slot >= 0
                    || p.Action is PlayerAction.Fallen or PlayerAction.Fight
                )
                {
                    continue;
                }

                Brain brain = board.For(p.Id);
                if (brain.ShootSteps > 0 || (p.Action == PlayerAction.Shoot && pk.Carrier == p.Id))
                {
                    HoldShot(world, p, brain);
                    continue;
                }
                if (pk.Carrier == p.Id && brain.LatePass.HasValue)
                {
                    LatePass(world, p, brain);
                    continue;
                }
                if (pk.Carrier == p.Id)
                {
                    if (!AiCommand.Held(world, p))
                    {
                        HoldLine(world, p, brain); // a dump taken wide stays wide
                    }

                    if (!AiCommand.Held(world, p) && Deke(world, p, brain))
                    {
                        continue;
                    }

                    Carry(world, p, brain);
                    continue;
                }
                brain.LatePass = Maybe.None<PassOption>();
                if (OneTimer(world, board, p, brain))
                {
                    continue;
                }

                if (PositioningModel.JobOf(world, p) == Job.Chase && pk.Carrier < 0)
                {
                    Pounce(world, p);
                    continue;
                }
                if (p.Id == crasher)
                {
                    CrashNet(world, p);
                    continue;
                }
                Hit(world, board, p, brain);
            }
        }

        public void DrawHud(World world, RenderContext render)
        {
            DecisionScenes.DrawLine(world);
        }

        /// <summary>Reads what happened last step: new carriers, passes and shots made, and how the pass in flight ended.</summary>
        private static void Track(World world, DecisionBoard board)
        {
            Puck pk = world.Puck;
            int last = board.LastCarrier;
            if (pk.Carrier >= 0 && pk.Carrier != last && pk.Carrier < world.Players.Count)
            {
                Brain brain = board.For(pk.Carrier);
                brain.GotAt = world.Time;
                brain.NextThink = world.Time + 0.12f;
            }
            if (
                last >= 0
                && last < world.Players.Count
                && pk.Carrier < 0
                && pk.LastTouch == last
                && pk.Live
            )
            {
                Player p = world.Players[last];
                DecisionTally tally = board.Tally[(int)p.Side];
                if (p.Action == PlayerAction.Pass)
                {
                    tally.Passes++;
                    Brain passer = board.For(p.Id);
                    if (passer.PassTo >= 0 && passer.PassTo < world.Players.Count)
                    {
                        Aimed(world, tally, world.Players[passer.PassTo]);
                    }

                    int to = passer.PassTo;
                    board.Meet = Maybe.None<HumanMeet>();
                    if (
                        to < 0
                        && PuckHandlingSystem.LastHumanPass(world).TryGet(out HumanPass human)
                        && human.Passer == p.Id
                        && world.Time - human.At < 0.1f
                    )
                    {
                        // A human's pass has its receiver from puck handling
                        to = human.Receiver;
                        board.Meet = new HumanMeet(to, human.Stick)
                        {
                            Released = human.Stick.Length() < Pushed,
                        };
                    }
                    board.Pass = new InFlight(p.Id, to, p.Side, world.Time);
                    passer.PassTo = -1; // a human's pass later has no receiver in mind
                }
                else if (p.Action == PlayerAction.Shoot)
                {
                    tally.Shots++;
                    board.Shot = (p.Side, world.Time);
                }
            }
            board.LastCarrier = pk.Carrier;
            EndPass(world, board);
        }

        /// <summary>Reads how the pass in flight ended: taken by a teammate, one-timed, intercepted, or dead.</summary>
        private static void EndPass(World world, DecisionBoard board)
        {
            Puck pk = world.Puck;
            if (!board.Pass.TryGet(out InFlight pass))
            {
                board.Meet = Maybe.None<HumanMeet>();
                return;
            }
            if (pk.Carrier < 0 && OneTimed(world, pass).TryGet(out Player shooter))
            {
                // struck on arrival without being carried: the pass reached him and was shot
                DecisionTally tally = board.Tally[(int)pass.Side];
                tally.Completed++;
                tally.Shots++;
                board.Shot = (shooter.Side, world.Time);
                board.Pass = Maybe.None<InFlight>();
                board.Meet = Maybe.None<HumanMeet>();
            }
            else if (pk.Carrier >= 0 && pk.Carrier < world.Players.Count)
            {
                DecisionTally tally = board.Tally[(int)pass.Side];
                if (world.Players[pk.Carrier].Side == pass.Side)
                {
                    tally.Completed++;
                }
                else
                {
                    tally.Intercepted++;
                }

                board.Pass = Maybe.None<InFlight>();
                board.Meet = Maybe.None<HumanMeet>();
            }
            else if (
                !pk.Live
                || world.Time - pass.At > 3f
                || world.Match.Phase is not (MatchPhase.Play or MatchPhase.Overtime)
            )
            {
                board.Pass = Maybe.None<InFlight>();
                board.Meet = Maybe.None<HumanMeet>();
            }
        }

        /// <summary>The teammate who one-timed the pass in flight: the loose puck last touched by a teammate of the passer, shooting.</summary>
        private static Maybe<Player> OneTimed(World world, InFlight pass)
        {
            int last = world.Puck.LastTouch;
            if (last == pass.Passer || last < 0 || last >= world.Players.Count)
            {
                return Maybe.None<Player>();
            }

            Player p = world.Players[last];
            return p.Side == pass.Side && p.Action == PlayerAction.Shoot ? p : Maybe.None<Player>();
        }

        /// <summary>Counts how open and how much on the move the teammate an AI pass was aimed at was.</summary>
        private static void Aimed(World world, DecisionTally tally, Player receiver)
        {
            tally.Aimed++;
            if (receiver.Speed > 2f)
            {
                tally.ToMoving++;
            }

            float nearest = world
                .OnIce(receiver.Side == Side.Home ? Side.Away : Side.Home)
                .Where(o => !o.IsGoalie)
                .Select(o => Vector3.Distance(o.Position, receiver.Position))
                .DefaultIfEmpty(99f)
                .Min();
            if (nearest > 3f)
            {
                tally.ToOpen++;
            }
        }

        /// <summary>Keeps shoot held through a slap shot's wind-up, then lets go, with the stick on the aim until the puck leaves.</summary>
        private static void HoldShot(World world, Player p, Brain brain)
        {
            if (world.Puck.Carrier != p.Id)
            {
                brain.ShootSteps = 0;
                return;
            }
            bool hold = brain.ShootSteps > 0;
            if (hold)
            {
                brain.ShootSteps--;
            }

            AiCommand.Act(
                world,
                p,
                p.Command with
                {
                    Move = brain.ShootAim,
                    Shoot = hold,
                    Sprint = false,
                },
                "shot held"
            );
        }

        /// <summary>The carrier's look at the play: carry on, shoot or pass, whichever is worth most.</summary>
        private void Carry(World world, Player p, Brain brain)
        {
            if (p.Action != PlayerAction.None || world.Time < brain.NextThink)
            {
                return;
            }

            brain.NextThink =
                world.Time
                + (Choices.ThinkEvery(p.Data.Traits.HockeyIq) * (0.8f + (0.4f * rng.NextSingle())));

            // This look is rolled against hockey IQ; a fumble reads it like a much weaker player, luck like a star
            int smart = Jitter.Roll(world, p, rng).Iq;
            (
                int End,
                Vector2 At,
                List<(Vector2 At, float Speed)> Seen,
                float Wobble,
                float Nearest,
                bool Pressed,
                float Held,
                bool Cycling,
                bool Camping,
                float Carry,
                float Lane,
                float Shoot
            ) state = BuildCarryState(world, p, brain, smart);
            Maybe<PassOption> pass = BestPass(
                world,
                p,
                state.End,
                state.At,
                state.Seen,
                smart,
                state.Cycling
            );
            if (Clear(world, p, brain, state.End, state.At, state.Nearest, pass))
            {
                return;
            }

            DecideCarryAction(world, p, brain, state, pass);
        }

        private void DecideCarryAction(
            World world,
            Player p,
            Brain brain,
            (
                int End,
                Vector2 At,
                List<(Vector2 At, float Speed)> Seen,
                float Wobble,
                float Nearest,
                bool Pressed,
                float Held,
                bool Cycling,
                bool Camping,
                float Carry,
                float Lane,
                float Shoot
            ) state,
            Maybe<PassOption> pass
        )
        {
            float noise = state.Wobble * PuckHandling.Gauss(rng); // drawn with or without a pass
            float passValue = pass.TryGet(out PassOption option)
                ? option.Value + noise
                : float.MinValue;

            if (ShouldShoot(state, passValue))
            {
                Shoot(world, p, state.End, state.At, state.Pressed);
            }
            else if (pass.TryGet(out PassOption best) && ShouldPass(state, passValue, best))
            {
                Pass(world, p, brain, best, state.At);
            }
            else if (
                state.Camping
                && state.Held > 6f
                && Choices.ShotQuality(state.End, state.At) > (state.Cycling ? 0.2f : 0f)
            )
            {
                Shoot(world, p, state.End, state.At, state.Pressed); // pinned too long: get rid of it (in the cycle only from a real chance; else skate on)
            }
        }

        private (
            int End,
            Vector2 At,
            List<(Vector2 At, float Speed)> Seen,
            float Wobble,
            float Nearest,
            bool Pressed,
            float Held,
            bool Cycling,
            bool Camping,
            float Carry,
            float Lane,
            float Shoot
        ) BuildCarryState(World world, Player p, Brain brain, int smart)
        {
            int end = PuckHandling.AttackEnd(world, p.Side);
            Vector2 at = Flat(world.Puck.Position);
            List<(Vector2 At, float Speed)> seen = Defenders(world, p, smart);
            float wobble = Choices.Wobble(smart);
            float nearest = seen.Where(d => d.Speed > 5f)
                .Select(d => Vector2.Distance(d.At, Flat(p.Position)))
                .DefaultIfEmpty(99f)
                .Min();
            bool pressed = nearest < 2.6f;
            float held = world.Time - brain.GotAt;
            bool cycling = PositioningModel.StationOf(world, p).HasValue;
            bool camping = held > (cycling ? 5f : 3f) && p.Speed < 1.2f; // stood still with the puck: time to let it go

            float carry =
                Choices.SpotValue(end, at)
                * (1f - Math.Min(0.35f, held / 12f))
                * (camping ? 0.5f : 1f);
            if (cycling && !camping)
            {
                carry += Choices.CycleKeep;
            }

            IEnumerable<Vector2> blockers = world
                .Players.Where(o => o.OnIce && o.Side != p.Side && !o.IsGoalie)
                .Select(o => Flat(o.Position));
            float lane = Choices.ShotLane(end, at, blockers);
            float shoot =
                Choices.ShotQuality(end, at)
                * (0.75f + (0.5f * Traits.Unit(p.Data.Traits.ShotAccuracy)))
                * lane
                * (pressed ? 0.7f : 1f);
            shoot += wobble * PuckHandling.Gauss(rng);

            return (
                end,
                at,
                seen,
                wobble,
                nearest,
                pressed,
                held,
                cycling,
                camping,
                carry,
                lane,
                shoot
            );
        }

        private bool ShouldShoot(
            (
                int End,
                Vector2 At,
                List<(Vector2 At, float Speed)> Seen,
                float Wobble,
                float Nearest,
                bool Pressed,
                float Held,
                bool Cycling,
                bool Camping,
                float Carry,
                float Lane,
                float Shoot
            ) state,
            float passValue
        )
        {
            return (
                    state.Shoot > state.Carry
                    && state.Shoot > passValue
                    && state.Shoot > (state.Camping ? ShootFrom * 0.6f : ShootFrom)
                )
                || (
                    PointShot(state.End, state.At, state.Lane, state.Pressed, state.Held)
                    && passValue < state.Carry
                );
        }

        private static bool ShouldPass(
            (
                int End,
                Vector2 At,
                List<(Vector2 At, float Speed)> Seen,
                float Wobble,
                float Nearest,
                bool Pressed,
                float Held,
                bool Cycling,
                bool Camping,
                float Carry,
                float Lane,
                float Shoot
            ) state,
            float passValue,
            PassOption best
        )
        {
            return passValue > (state.Carry * 1.05f) + 0.01f
                || (state.Camping && state.Held > 6f && (!state.Cycling || best.Success > 0.35f));
        }

        /// <summary>A carrier in his own zone with a forechecker on him and no pass he trusts weighs, once
        /// while pinned, firing the puck out. Hockey IQ sets the odds (a poor skater panics far more often
        /// than a smart one, who would rather skate it out or wait for a lane), a fumbled read doubles
        /// them, and a tighter check makes it likelier. The clear goes hard up the boards on his side:
        /// untouched from behind the red line it goes the length, icing. With icing off (Arcade) there is
        /// nothing to fear, so the odds are higher still. The penalty kill clears its own way.</summary>
        private bool Clear(
            World world,
            Player p,
            Brain brain,
            int end,
            Vector2 at,
            float nearest,
            Maybe<PassOption> pass
        )
        {
            bool pinned =
                end * at.X < -RinkSpec.BlueLineX
                && nearest < 2.6f
                && (!pass.TryGet(out PassOption option) || option.Success < SafePass);
            if (!pinned || Same(brain.ClearRun, brain.GotAt) || p.Slot >= 0)
            {
                return false;
            }

            brain.ClearRun = brain.GotAt;
            if (PenaltyCalls.PowerPlay(world).TryGetSide(out Side pp) && pp != p.Side)
            {
                return false; // a killer clears with special teams
            }

            (ReadOutcome outcome, int iq) = Jitter.Roll(world, p, rng);
            float odds =
                Traits.Scale(iq, 0.12f, 0.015f)
                * (outcome == ReadOutcome.Fumble ? 2f : 1f)
                * (nearest < 1.4f ? 1.4f : 1f)
                * (world.Setup.Rules.Icing == IcingCall.Off ? 1.5f : 1f);
            if (rng.NextSingle() >= odds)
            {
                return false;
            }

            // up the boards on his side, a little off them so the puck runs along the wall, never at the far net
            float side =
                (MathF.Abs(at.Y) > 1f)
                    ? MathF.Sign(at.Y)
                    : ((rng.NextSingle() < 0.5f) ? (-1f) : 1f);
            Vector2 aim = Vector2.Normalize(
                new Vector2(end, side * (0.12f + (0.2f * rng.NextSingle())))
            );
            AiCommand.Shoot(world, p, AiLayer.Decisions, aim, 3, "clear"); // a quick snap, a wrist shot's speed: plenty to go the length
            DecisionTally tally = DecisionState.Tally(world, p.Side);
            tally.Clears++;
            return true;
        }

        /// <summary>A carrier coming up to the attacking blue line with a teammate already wholly over it
        /// decides once whether to hold up short of the line, so the teammate can tag up. A smart skater
        /// with time usually does; a poor one, a fumbled read or a defender on him and he carries on in,
        /// offside. Holding, he curls across the ice short of the line until everyone is back onside or
        /// his patience runs out. With offside off (Arcade) nobody holds up.</summary>
        private void HoldLine(World world, Player p, Brain brain)
        {
            if (!CanHoldLine(world, p, brain, out int end, out Vector2 at, out float _))
            {
                return;
            }

            if (!Same(brain.HoldRun, brain.GotAt) && !StartHold(world, p, brain, end))
            {
                return;
            }
            if (world.Time > brain.HoldUntil)
            {
                return;
            }

            AiCommand.Steer(
                world,
                p,
                AiLayer.Decisions,
                new PlayerCommand { Move = Steering.Toward(p, HoldSpot(end, at), 1f).Move },
                "hold up at the line"
            );
        }

        private static bool CanHoldLine(
            World world,
            Player p,
            Brain brain,
            out int end,
            out Vector2 at,
            out float toLine
        )
        {
            end = PuckHandling.AttackEnd(world, p.Side);
            at = Flat(world.Puck.Position);
            toLine = RinkSpec.BlueLineX - (end * at.X);
            int holdEnd = end;
            return world.Setup.Rules.Offside
                && p.Action == PlayerAction.None
                && brain.ShootSteps <= 0
                && toLine is not < 0.2f and not > HoldFrom
                && world
                    .OnIce(p.Side)
                    .Any(m =>
                        m != p && !m.IsGoalie && OffsideRule.PlayerInZone(m.Position, holdEnd)
                    );
        }

        private bool StartHold(World world, Player p, Brain brain, int end)
        {
            if (end * p.Velocity.X < 0.5f)
            {
                return false; // not going in: nothing to hold up for yet
            }

            brain.HoldRun = brain.GotAt;
            brain.HoldUntil = float.NegativeInfinity;
            DecisionState.Tally(world, p.Side).HoldLooks++;
            (ReadOutcome outcome, int iq) = Jitter.Roll(world, p, rng);
            bool pressed = world
                .OnIce(p.Side == Side.Home ? Side.Away : Side.Home)
                .Any(o => !o.IsGoalie && Vector3.Distance(o.Position, p.Position) < 2.6f);
            float odds = Traits.Scale(iq, 0.3f, 0.98f) * (pressed ? 0.6f : 1f);
            if (outcome == ReadOutcome.Fumble || rng.NextSingle() >= odds)
            {
                return false;
            }

            brain.HoldUntil = world.Time + HoldFor;
            DecisionState.Tally(world, p.Side).HoldUps++;
            return true;
        }

        private static Vector2 HoldSpot(int end, Vector2 at)
        {
            // curl across the ice a couple of metres short of the line
            float across =
                (MathF.Abs(at.Y) > 6f) ? (-MathF.Sign(at.Y)) : ((at.Y >= 0f) ? 1f : (-1f));
            return TeamShape.Inside(
                new Vector2(end * (RinkSpec.BlueLineX - 2.2f), at.Y + (across * 4f)),
                1.5f
            );
        }

        /// <summary>Gets whether a remembered possession is the current one (both are its <see cref="Brain.GotAt"/>).</summary>
        private static bool Same(float run, float gotAt)
        {
            return MathF.Abs(run - gotAt) < 1e-4f;
        }

        private static Vector2 Flat(Vector3 v)
        {
            return new Vector2(v.X, v.Z);
        }
    }
}
