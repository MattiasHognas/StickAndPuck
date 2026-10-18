using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match;

namespace StickAndPuck.App.Referee
{
    /// <summary>The referee skates to the faceoff dot at every stoppage, stands set beside it and drops the
    /// puck (the match flow waits for it: <see cref="MatchFlow.SetRefereeDrops"/>), then follows play
    /// along the far boards, trailing the puck on the centre-ice side and keeping clear of players.
    /// Puck physics and checks never see the referee, so it cannot touch the play. Runs only in a world
    /// that runs the match flow. Penalty calls are the penalty rules': see <see cref="RefereeState.Signal"/>.
    /// Once set at the dot the referee holds the puck a random moment, rolled per faceoff from
    /// its dice, so a faceoff cannot be timed by habit.</summary>
    /// <remarks>Initializes the system with the given dice (the self test seeds them).</remarks>
    internal sealed class RefereeSystem(Random dice) : ISystem, IDisposable
    {
        private readonly RefereeRenderer renderer = new();
        private readonly Random dice = dice;

        /// <summary>The dice seed without a match seed.</summary>
        internal const int Fallback = 8;

        /// <summary>Initializes the system with its own dice: SAP_SEED's referee stream, or the fixed one, so every run repeats.</summary>
        public RefereeSystem()
            : this(Seeds.Dice(Seeds.Environment, "referee", Fallback)) { }

        public void Start(World world)
        {
            if (MatchFlow.IsRunning(world))
            {
                renderer.Load();
            }
        }

        public void Update(World world, float dt)
        {
            if (!MatchFlow.IsRunning(world))
            {
                return;
            }

            MatchFlow.SetRefereeDrops(world, referee: true);
            RefereeState r = RefereeState.Of(world);
            if (world.Events.Of<WhistleBlown>().Any())
            {
                r.ClearCall();
            }

            // a faceoff violation: the new man at the dot gets set again before the drop
            if (world.Events.Of<FaceoffViolated>().Any())
            {
                r.SetTime = 0;
                r.Waited = 0;
            }

            Step(world, r, dice, dt);
            renderer.Animate(r, dt, world.Time);
        }

        /// <summary>Moves the referee one step and drops the puck when it has stood set at the dot for this faceoff's hold.</summary>
        internal static void Step(World world, RefereeState r, Random dice, float dt)
        {
            MatchState m = world.Match;
            bool faceoff = m.Phase == MatchPhase.Faceoff;
            Vector3 target =
                world.Time - r.DroppedAt < RefereeTuning.BackOffSeconds
                    ? BackOff(r)
                    : Target(world);
            bool atDot =
                m.Phase
                is not (
                    MatchPhase.Play
                    or MatchPhase.Final
                    or MatchPhase.Overtime
                    or MatchPhase.Shootout
                );
            // at its own dot the referee stands next to the centres; everywhere else it keeps its distance
            Vector3 push =
                !atDot || Flat(target - r.Position).Length() > 2f ? Avoid(world, r) : Vector3.Zero;
            Move(r, target, push, dt);
            if (atDot && Flat(target - r.Position).Length() <= RefereeTuning.Arrived * 3)
            {
                Turn(r, 0, dt); // face the dot and the camera
            }

            if (!faceoff)
            {
                r.SetTime = 0;
                r.Waited = 0;
                r.Hold = 0;
                return;
            }
            // one roll per faceoff, made as it begins, so the hold is the same whether the referee is already there or still skating in
            if (r.Hold <= 0)
            {
                r.Hold = RollHold(r.LastHold, dice);
            }

            r.Waited += dt;
            bool set =
                Flat(target - r.Position).Length() <= RefereeTuning.Arrived && r.Speed < 0.3f;
            r.SetTime = set ? r.SetTime + dt : 0;
            bool late = r.Waited >= RefereeTuning.MaxWaitSeconds;
            if ((r.SetTime >= r.Hold || late) && MatchFlow.Drop(world))
            {
                if (late && r.SetTime < r.Hold)
                {
                    r.LateDrops++;
                }

                r.LastHold = r.SetTime;
                r.ClearCall();
                r.DroppedAt = world.Time;
                r.DropPoint = r.Position;
                r.SetTime = 0;
                r.Waited = 0;
            }
        }

        /// <summary>Rolls how long the referee holds the puck once set: uniform over the span, but never within <see cref="RefereeTuning.MinHoldChange"/> of the last hold.</summary>
        private static float RollHold(float last, Random dice)
        {
            float hold = 0;
            for (int tries = 0; tries < 8; tries++)
            {
                hold =
                    RefereeTuning.SetSeconds
                    + (
                        (RefereeTuning.MaxSetSeconds - RefereeTuning.SetSeconds) * dice.NextSingle()
                    );
                if (MathF.Abs(hold - last) >= RefereeTuning.MinHoldChange)
                {
                    break;
                }
            }
            return hold;
        }

        /// <summary>Where the referee wants to be: the coming faceoff's drop spot during a stoppage, otherwise trailing the play.</summary>
        private static Vector3 Target(World world)
        {
            switch (world.Match.Phase)
            {
                case MatchPhase.Faceoff:
                case MatchPhase.Stopped:
                    return RefereeState.DropSpot(MatchFlow.FaceoffDot(world));
                case MatchPhase.Intro:
                case MatchPhase.Goal:
                case MatchPhase.Intermission:
                    // each of these restarts at centre ice
                    return RefereeState.DropSpot(RinkSpec.CentreDot);
                case MatchPhase.Play:
                case MatchPhase.Overtime:
                case MatchPhase.Shootout:
                case MatchPhase.Final:
                default:
                    Vector3 pk = world.Puck.Position;
                    float x = pk.X + (pk.X >= 0 ? -RefereeTuning.TrailX : RefereeTuning.TrailX);
                    return Clear(world, Math.Clamp(x, -RefereeTuning.MaxX, RefereeTuning.MaxX));
            }
        }

        /// <summary>Right after the drop the referee backs straight away from the dot toward the far boards, between the wingers.</summary>
        private static Vector3 BackOff(RefereeState r)
        {
            return new Vector3(r.DropPoint.X, 0, r.DropPoint.Z - (2 * RefereeTuning.AvoidRadius));
        }

        /// <summary>Gets the free spot along the far boards nearest <paramref name="x"/>, with room around it from every player and the puck.</summary>
        private static Vector3 Clear(World world, float x)
        {
            for (int k = 0; k <= 40; k++)
            {
                // 0, +0.5, -0.5, +1, -1, ...
                float dx = (k + 1) / 2 * 0.5f * (k % 2 == 1 ? 1 : -1);
                Vector3 spot = new(x + dx, 0, RefereeTuning.FollowZ);
                if (Math.Abs(spot.X) > RefereeTuning.MaxX)
                {
                    continue;
                }

                bool free =
                    Flat(spot - world.Puck.Position).Length() >= RefereeTuning.TrailX - 1
                    && world.Players.All(p =>
                        !p.OnIce || Flat(spot - p.Position).Length() >= RefereeTuning.AvoidRadius
                    );
                if (free)
                {
                    return spot;
                }
            }
            return new Vector3(x, 0, RefereeTuning.FollowZ);
        }

        /// <summary>Gets a push away from every player and the puck within <see cref="RefereeTuning.AvoidRadius"/> of the referee, much stronger the closer they are.</summary>
        private static Vector3 Avoid(World world, RefereeState r)
        {
            // look a moment ahead, so the referee turns before it gets close rather than after
            Vector3 ahead = r.Position + (r.Velocity * 0.25f);
            Vector3 push = Vector3.Zero;
            foreach (Player p in world.Players)
            {
                if (p.OnIce)
                {
                    push += Away(ahead, p.Position);
                }
            }
            if (world.Match.Phase == MatchPhase.Play)
            {
                push += Away(ahead, world.Puck.Position);
            }

            return push;
        }

        private static Vector3 Away(Vector3 from, Vector3 other)
        {
            Vector3 d = Flat(from - other);
            float len = d.Length();
            if (len >= RefereeTuning.AvoidRadius)
            {
                return Vector3.Zero;
            }

            if (len < 0.01f)
            {
                d = new Vector3(0, 0, -1);
            }
            // grows steeply up close, so a player in the way always wins over the pull of the target
            return Vector3.Normalize(d) * (RefereeTuning.AvoidRadius - len) / MathF.Max(len, 0.3f);
        }

        /// <summary>Skates toward a point, braking to arrive on it, steered by <paramref name="push"/>, turning toward the way it goes.</summary>
        private static void Move(RefereeState r, Vector3 target, Vector3 push, float dt)
        {
            Vector3 to = Flat(target - r.Position);
            float dist = to.Length();
            // the fastest speed that can still stop on the target
            float want = MathF.Min(
                RefereeTuning.TopSpeed,
                MathF.Sqrt(2 * RefereeTuning.Acceleration * dist)
            );
            Vector3 desired =
                (dist > 0.001f ? to / dist * want : Vector3.Zero)
                + (push * RefereeTuning.AvoidGain);
            if (desired.Length() > RefereeTuning.TopSpeed)
            {
                desired = Vector3.Normalize(desired) * RefereeTuning.TopSpeed;
            }

            Vector3 change = desired - r.Velocity;
            float step = RefereeTuning.Acceleration * dt;
            r.Velocity += change.Length() > step ? Vector3.Normalize(change) * step : change;
            if (dist <= RefereeTuning.Arrived * 0.5f && r.Velocity.Length() < 0.3f)
            {
                r.Velocity = Vector3.Zero;
            }

            r.Position = Clamp(r.Position + (r.Velocity * dt));
            if (r.Speed > 0.5f)
            {
                Turn(r, SimMath.Atan2(r.Velocity.X, r.Velocity.Z), dt);
            }
        }

        private static void Turn(RefereeState r, float heading, float dt)
        {
            float delta = MathF.IEEERemainder(heading - r.Heading, MathF.Tau);
            float most = RefereeTuning.TurnRate * dt;
            r.Heading += Math.Clamp(delta, -most, most);
        }

        private static Vector3 Clamp(Vector3 v)
        {
            const float margin = 0.6f;
            return new Vector3(
                Math.Clamp(v.X, -(RinkSpec.GoalLineX - 2), RinkSpec.GoalLineX - 2),
                0,
                Math.Clamp(v.Z, -RinkSpec.HalfWidth + margin, RinkSpec.HalfWidth - margin)
            );
        }

        private static Vector3 Flat(Vector3 v)
        {
            return v with { Y = 0 };
        }

        public void DrawScene(World world, RenderContext render)
        {
            if (MatchFlow.IsRunning(world))
            {
                renderer.DrawScene(RefereeState.Of(world), render);
            }
        }

        public void DrawMask(World world, RenderContext render)
        {
            if (MatchFlow.IsRunning(world))
            {
                renderer.DrawMask(RefereeState.Of(world), render);
            }
        }

        public void Dispose()
        {
            renderer.Dispose();
        }
    }
}
