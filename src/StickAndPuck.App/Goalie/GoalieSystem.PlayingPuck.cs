using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Goalie
{
    internal sealed partial class GoalieSystem
    {
        /// <summary>Keeps a held puck in the glove; with no whistle after <see cref="GoalieTuning.HoldSeconds"/>,
        /// plays it out: a good puck handler passes to an open teammate, anyone else rims it to the nearer
        /// corner. Puck handling also sets how hard and how true the puck goes.</summary>
        private void Hold(World world, Player g, GoalieState state, int end)
        {
            Puck pk = world.Puck;
            pk.Position = g.Position + new Vector3(-end * 0.3f, 0, 0);
            pk.Velocity = Vector3.Zero;
            if (
                !pk.Live
                || world.Match.Phase != MatchPhase.Play
                || world.Time - state.HeldSince < GoalieTuning.HoldSeconds
            )
            {
                return;
            }

            float side = g.Position.Z < 0 ? -1 : 1;
            int handling = g.Data.Traits.GoaliePuckHandling;
            Maybe<Player> mate =
                Dice.NextSingle()
                < Traits.Scale(handling, GoalieTuning.PassOutPoor, GoalieTuning.PassOutGood)
                    ? OpenMate(world, g, end)
                    : Maybe.None<Player>();
            Vector3 start = g.Position + new Vector3(0, 0, side * 0.5f);
            Vector3 dir = mate.TryGet(out Player to)
                ? Flat(to.Position - start)
                : Vector3.Normalize(new Vector3(end * 0.35f, 0, side));
            pk.Carrier = -1;
            Release(world, g, state, start, dir, handling, pass: mate.HasValue);
            state.PassedOut = mate.HasValue;
        }

        /// <summary>Leaves the crease for a loose, slowish puck behind the net (inside the trapezoid under its rule) when the goalie gets there first,
        /// as often as its puck handling says (rolled once per loose puck), then moves it on to an open
        /// teammate, or up the boards with none. Gives up when an opponent closes in or it takes too long.</summary>
        private bool PlayPuck(World world, Player g, GoalieState state, int end, float dt)
        {
            Puck pk = world.Puck;
            if (!CanPlayPuck(world, state, end, pk))
            {
                state.Playing = false;
                state.Rolled = false;
                return false;
            }

            float mine = Vector3.Distance(Flat(g.Position), Flat(pk.Position));
            float theirs = NearestOpponentDistance(world, g, pk);
            if (!state.Playing && !TryStartPlaying(world, g, state, mine, theirs))
            {
                return false;
            }

            if (ShouldStopPlaying(world, state, theirs))
            {
                state.Playing = false;
                state.OutOfCrease = true;
                return false;
            }

            state.OutOfCrease = true;
            state.SeenAt = float.NaN;
            state.ReactZ = float.NaN;
            Move(g, Flat(pk.Position + (pk.Velocity * 0.15f)), GoalieTuning.PlaySkate, dt);
            Face(g, end, pk.Position);
            return mine > GoalieTuning.PlayTouch || ReleaseWhenTouching(world, g, state, end, pk);
        }

        private static bool CanPlayPuck(World world, GoalieState state, int end, Puck pk)
        {
            return pk.Carrier < 0
                && pk.Live
                && world.Match.Phase == MatchPhase.Play
                && pk.Position.X * end > RinkSpec.GoalLineX + 0.2f
                && pk.Position.Y < GoalieTuning.LowShot
                && new Vector2(pk.Position.X - (end * RinkSpec.GoalLineX), pk.Position.Z).Length()
                    < GoalieTuning.PlayReach
                && pk.Velocity.Length() < GoalieTuning.PlaySpeed
                && world.Time >= state.IgnoreUntil
                && InsideTrapezoid(world, pk);
        }

        /// <summary>Gets a value indicating whether the goalie may play a puck there: always, or under the trapezoid rule
        /// only with the puck a margin inside the trapezoid, so the AI goalie never takes the minor (W72). The goalie
        /// checks it every step it goes for the puck, so it gives up on one that slides out into the corner.</summary>
        private static bool InsideTrapezoid(World world, Puck pk)
        {
            return !world.Setup.Rules.Trapezoid
                || !RinkSpec.OutsideTrapezoid(pk.Position, GoalieTuning.TrapezoidMargin);
        }

        private static float NearestOpponentDistance(World world, Player g, Puck pk)
        {
            return world
                .Players.Where(o => o.OnIce && o.Side != g.Side && !o.IsGoalie)
                .Select(o => Vector3.Distance(Flat(o.Position), Flat(pk.Position)))
                .DefaultIfEmpty(99f)
                .Min();
        }

        private bool TryStartPlaying(
            World world,
            Player g,
            GoalieState state,
            float mine,
            float theirs
        )
        {
            if (state.Rolled || theirs < mine + 1.5f)
            {
                return false;
            }

            state.Rolled = true;
            int handling = g.Data.Traits.GoaliePuckHandling;
            float chance = Traits.Scale(
                handling,
                GoalieTuning.PlayChancePoor,
                GoalieTuning.PlayChanceGood
            );
            if (chance <= 0f || Dice.NextSingle() >= chance)
            {
                return false;
            }

            state.Playing = true;
            state.PlaySince = world.Time;
            return true;
        }

        private static bool ShouldStopPlaying(World world, GoalieState state, float theirs)
        {
            return theirs < GoalieTuning.PlayPressure
                || world.Time - state.PlaySince > GoalieTuning.PlaySeconds;
        }

        private bool ReleaseWhenTouching(World world, Player g, GoalieState state, int end, Puck pk)
        {
            Maybe<Player> mate = OpenMate(world, g, end);
            Vector3 dir = mate.TryGet(out Player to)
                ? Flat(to.Position - pk.Position)
                : Vector3.Normalize(new Vector3(-end * 0.6f, 0, pk.Position.Z < 0 ? -1 : 1));
            Release(
                world,
                g,
                state,
                Flat(pk.Position),
                dir,
                g.Data.Traits.GoaliePuckHandling,
                pass: mate.HasValue
            );
            state.Playing = false;
            state.PassedOut = mate.HasValue;
            return true;
        }

        /// <summary>Sends the puck off the goalie's stick, off line by up to the handling's error, and publishes a pass.</summary>
        private void Release(
            World world,
            Player g,
            GoalieState state,
            Vector3 start,
            Vector3 dir,
            int handling,
            bool pass
        )
        {
            Puck pk = world.Puck;
            float error =
                Traits.Scale(handling, GoalieTuning.PassErrorPoor, GoalieTuning.PassErrorGood)
                * ((Dice.NextSingle() * 2) - 1);
            float speed = pass
                ? Traits.Scale(handling, GoalieTuning.PassSpeedPoor, GoalieTuning.PassSpeedGood)
                : GoalieTuning.PlayOutSpeed;
            dir = Flat(dir);
            dir = dir.LengthSquared() < 1e-6f ? new Vector3(1, 0, 0) : Vector3.Normalize(dir);
            float c = SimMath.Cos(error),
                s = SimMath.Sin(error);
            dir = new Vector3((dir.X * c) - (dir.Z * s), 0, (dir.X * s) + (dir.Z * c));
            pk.Position = start with { Y = 0 };
            pk.Velocity = dir * speed;
            pk.LastTouch = g.Id;
            state.IgnoreUntil = world.Time + (GoalieTuning.IgnoreAfterSave * 2);
            state.OutOfCrease = true;
            if (pass)
            {
                world.Events.Publish(new PassMade(world.Time, g.Side, g.Id, start, speed));
            }
        }

        /// <summary>The teammate a goalie passes to: the nearest skater in front of the goal line with no opponent near the lane, or None.</summary>
        private static Maybe<Player> OpenMate(World world, Player g, int end)
        {
            Puck pk = world.Puck;
            Maybe<Player> best = Maybe.None<Player>();
            float bestDistance = float.MaxValue;
            foreach (Player m in world.Players)
            {
                if (
                    !m.OnIce
                    || m.Side != g.Side
                    || m.IsGoalie
                    || m.Action is PlayerAction.Fallen or PlayerAction.Fight
                )
                {
                    continue;
                }

                if (m.Position.X * end > RinkSpec.GoalLineX - 1f)
                {
                    continue;
                }

                float distance = Vector3.Distance(Flat(m.Position), Flat(pk.Position));
                if (distance < 3f || distance > 25f || distance >= bestDistance)
                {
                    continue;
                }

                bool open = world.Players.All(o =>
                    !o.OnIce
                    || o.Side == g.Side
                    || o.IsGoalie
                    || Lane(Flat(pk.Position), Flat(m.Position), Flat(o.Position)) > 1.2f
                );
                if (!open)
                {
                    continue;
                }

                best = m;
                bestDistance = distance;
            }
            return best;
        }

        /// <summary>The distance from a point to the segment from a to b.</summary>
        private static float Lane(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float t = Math.Clamp(
                Vector3.Dot(p - a, ab) / MathF.Max(ab.LengthSquared(), 1e-6f),
                0f,
                1f
            );
            return Vector3.Distance(a + (ab * t), p);
        }
    }
}
