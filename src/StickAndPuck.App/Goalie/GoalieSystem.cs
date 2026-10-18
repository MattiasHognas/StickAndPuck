using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Goalie.Dev;
using StickAndPuck.App.Players.Puck;

namespace StickAndPuck.App.Goalie
{
    /// <summary>Goalies hold the crease, react to shots from goalie reflexes, and catch or leave rebounds.
    /// Runs before puck physics: physics lets the puck pass through goalies, so this system decides
    /// every save. A goalie squares up to the puck and cuts the angle (<see cref="GoalieModel.Spot(int, Vector3, int)"/>);
    /// when a shot comes it reacts after its reaction time and pushes or drops toward where the puck
    /// will cross. A puck that meets a covered part of the goalie is stopped, except a share that
    /// squeezes through, more for slow reflexes and hard shots. A stopped puck is caught (the goalie
    /// carries it: <see cref="Puck.Carrier"/> is the goalie, which rules read as a freeze to whistle)
    /// or left as a rebound. Every stop publishes <see cref="ShotSaved"/> with the goalie's
    /// side and id and the shot speed as Strength. With no whistle, a held puck is played out.
    /// Goalie traits: positioning (the goalie's hockey IQ) sets how far out and how square the
    /// goalie stands and how fast it shuffles; glove and blocker set the reach and the clean-stop chance
    /// on each side of the body, so a weak hand is a weak side; rebound control sets how often a shot
    /// is held and how hard and how central the rebounds are; puck handling sets whether a held puck goes
    /// to a teammate or the corner, how true that pass is, and whether the goalie leaves the crease to
    /// stop a loose puck behind the net and move it on.</summary>
    internal sealed partial class GoalieSystem : ISystem, IDisposable
    {
        private readonly Dictionary<int, GoalieState> states = [];
        private readonly GoalieRenderer renderer = new();

        /// <summary>The dice seed without a match seed.</summary>
        internal const int Fallback = 5;

        /// <summary>Gets or sets the dice for squeezes and rebounds; tests seed it.</summary>
        public Random Dice { get; set; } = Seeds.Dice(0, "goalie", Fallback);

        public void Start(World world)
        {
            renderer.Load();
        }

        public void Update(World world, float dt)
        {
            GoalieDemo.Update(world);
            foreach (Player g in world.Players)
            {
                if (!g.OnIce || !g.IsGoalie)
                {
                    continue;
                }

                if (!states.TryGetValue(g.Id, out GoalieState state))
                {
                    states[g.Id] = state = new GoalieState();
                }

                Step(world, g, state, dt);
                renderer.Animate(
                    g,
                    state,
                    world.Time,
                    dt,
                    Rules.Match.Celebration.Slumps(world, g)
                );
            }
        }

        private void Step(World world, Player g, GoalieState state, float dt)
        {
            int end = GoalieModel.End(world, g);
            Puck pk = world.Puck;
            state.Rise(world.Time);
            int positioning = g.Data.Traits.HockeyIq;
            float shuffle =
                GoalieTuning.ShuffleSpeed
                * Traits.Scale(positioning, GoalieTuning.ShuffleWorst, GoalieTuning.ShuffleBest);
            if (pk.Carrier == g.Id)
            {
                state.Playing = false;
                Hold(world, g, state, end);
                Move(
                    g,
                    GoalieModel.Spot(end, pk.Position with { X = -end * 10 }, positioning),
                    shuffle,
                    dt
                );
                return;
            }
            if (PlayPuck(world, g, state, end, dt))
            {
                return;
            }

            bool watching = pk.Carrier < 0 && pk.Live && world.Time >= state.IgnoreUntil;
            if (watching)
            {
                Track(world, g, state, end);
            }
            else
            {
                state.SeenAt = float.NaN;
            }

            Vector3 spot = GoalieModel.Spot(end, pk.Position, positioning);
            if (state.Reacting)
            {
                Move(g, spot with { Z = state.ReactZ }, GoalieModel.SaveSpeed(g.Data.Traits), dt);
            }
            else
            {
                Move(g, spot, state.OutOfCrease ? GoalieTuning.PlaySkate : shuffle, dt);
            }

            if (state.OutOfCrease && Vector3.Distance(g.Position, spot) < 0.2f)
            {
                state.OutOfCrease = false;
            }

            Face(g, end, pk.Position);

            if (watching)
            {
                Contact(world, g, state, end, dt);
            }
        }

        /// <summary>Watches for a shot; after the reaction time, aims the push at where it will cross the goalie's plane.</summary>
        private void Track(World world, Player g, GoalieState state, int end)
        {
            Puck pk = world.Puck;
            if (!GoalieModel.IsShot(end, pk.Position, pk.Velocity))
            {
                state.SeenAt = float.NaN;
                state.ShotFrom = float.NaN;
                if (!state.Down)
                {
                    state.ReactZ = float.NaN;
                }

                return;
            }
            if (float.IsNaN(state.SeenAt))
            {
                state.SeenAt = world.Time;
                state.ShotFrom = new Vector2(
                    pk.Position.X - (end * RinkSpec.GoalLineX),
                    pk.Position.Z
                ).Length();
                state.Misread =
                    GoalieModel.ReadError(g.Data.Traits, pk.Velocity.Length())
                    * PuckHandling.Gauss(Dice);
            }
            if (world.Time - state.SeenAt < GoalieModel.ReactionTime(g.Data.Traits))
            {
                return;
            }

            ShotLine at = GoalieModel.Cross(pk.Position, pk.Velocity, g.Position.X);
            if (float.IsInfinity(at.Offset))
            {
                return;
            }

            float limit = RinkSpec.NetWidth / 2;
            state.ReactZ = Math.Clamp(at.Offset + state.Misread, -limit, limit);
            if (at.Height < GoalieTuning.LowShot)
            {
                state.Drop(world.Time);
            }
        }

        /// <summary>Checks the puck's path this step against the goalie's plane, or smothers a slow puck at its feet.</summary>
        private void Contact(World world, Player g, GoalieState state, int end, float dt)
        {
            Puck pk = world.Puck;
            float from = pk.Position.X * end,
                to = (pk.Position.X + (pk.Velocity.X * dt)) * end,
                plane = g.Position.X * end;
            if (from < plane && to >= plane)
            {
                ShotLine at = GoalieModel.Cross(pk.Position, pk.Velocity, g.Position.X);
                float offset = at.Offset - g.Position.Z;
                int hand = GoalieModel.HandTrait(g.Data, end, offset < 0 ? -1 : 1);
                bool hands = GoalieModel.HandHeight(at.Height);
                float cover =
                    GoalieModel.Coverage(offset, at.Height, state.Down, GoalieModel.HandScale(hand))
                    / GoalieModel.CloseRead(state.ShotFrom, g.Data.Traits.HockeyIq);
                if (
                    cover <= 1f
                    && Dice.NextSingle()
                        < CleanChance(g, pk.Velocity.Length(), cover, hands ? hand : 50)
                )
                {
                    if (hands)
                    {
                        state.Pose(
                            world.Time,
                            offset < 0 ? -1 : 1,
                            GoalieModel.GloveSide(g.Data, end)
                        );
                    }

                    Save(world, g, state, end, new Vector3(g.Position.X, at.Height, at.Offset));
                }
                return;
            }
            Vector2 flat = new(pk.Position.X - g.Position.X, pk.Position.Z - g.Position.Z);
            if (
                pk.Velocity.Length() < GoalieTuning.SmotherSpeed
                && flat.Length() < GoalieTuning.SmotherReach
                && pk.Position.Y < GoalieTuning.LowShot
            )
            {
                state.Drop(world.Time);
                Catch(world, g, state, pk.Velocity.Length(), pk.Position);
            }
        }

        /// <summary>The chance a covered shot is stopped. Reflexes and the shot's speed set how many squeeze through,
        /// a share that grows from <see cref="GoalieTuning.CentreMiss"/> times as many for a shot into the middle of the
        /// goalie to <see cref="GoalieTuning.EdgeMiss"/> times for one at the edge of the reach; the hand on that side
        /// then adds or takes off its share wherever the shot meets it.</summary>
        private static float CleanChance(Player g, float speed, float cover, int hand)
        {
            float miss =
                1f
                - GoalieModel.ByReflexes(
                    g.Data.Traits,
                    GoalieTuning.CleanSlow,
                    GoalieTuning.CleanFast
                );
            miss +=
                GoalieTuning.HardShotPenalty
                * Math.Clamp(
                    (speed - GoalieTuning.FastShot) / (45f - GoalieTuning.FastShot),
                    0f,
                    1f
                );
            float chance =
                1f
                - (
                    MathF.Max(0f, miss)
                    * (
                        GoalieTuning.CentreMiss
                        + ((GoalieTuning.EdgeMiss - GoalieTuning.CentreMiss) * cover)
                    )
                );
            return chance + Traits.Scale(hand, -GoalieTuning.HandClean, GoalieTuning.HandClean);
        }

        private void Save(World world, Player g, GoalieState state, int end, Vector3 at)
        {
            Puck pk = world.Puck;
            float speed = pk.Velocity.Length();
            Traits t = g.Data.Traits;
            ShotLine line = GoalieModel.Cross(pk.Position, pk.Velocity, end * RinkSpec.GoalLineX);
            Vector3 heading = new(end * RinkSpec.GoalLineX, line.Height, line.Offset);
            int control = t.GoalieReboundControl;
            float catchChance = Traits.Scale(
                control,
                GoalieTuning.CatchSlow,
                GoalieTuning.CatchFast
            );
            if (at.Y < GoalieTuning.LowShot)
            {
                catchChance *= 0.5f; // pads kick out rebounds
            }

            if (speed > GoalieTuning.HardShot)
            {
                catchChance *= 0.6f;
            }

            if (speed < 12f)
            {
                catchChance = MathF.Max(catchChance, 0.8f); // soft shots are easy to hold
            }

            pk.Position = at;
            if (Dice.NextSingle() < catchChance)
            {
                Catch(world, g, state, speed, heading);
                return;
            }

            // rebound: back out, slower, off to one side; poor rebound control leaves it hard and in the
            // slot, good control kills it and steers it wide to the corners
            float keep = Traits.Scale(control, GoalieTuning.ReboundSlow, GoalieTuning.ReboundFast);
            float wide = Traits.Scale(control, 0.1f, 0.75f);
            float angle =
                (wide + (Dice.NextSingle() * (1.2f - wide))) * (Dice.Next(2) == 0 ? -1 : 1);
            Vector3 dir = new(-end * SimMath.Cos(angle), 0, SimMath.Sin(angle));
            pk.Position = at with { X = at.X - (end * 0.12f) };
            pk.Velocity =
                (dir * speed * keep) + new Vector3(0, MathF.Min(pk.Velocity.Y, 0) * 0.2f, 0);
            pk.LastTouch = g.Id;
            state.IgnoreUntil = world.Time + GoalieTuning.IgnoreAfterSave;
            state.SeenAt = float.NaN;
            world.Events.Publish(new ShotSaved(world.Time, g.Side, g.Id, heading, speed));
            GoalieDemo.Saved(world, caught: false);
        }

        /// <summary>Holds the puck; the save is published at <paramref name="heading"/>, where the shot was going to cross the goal line.</summary>
        private static void Catch(
            World world,
            Player g,
            GoalieState state,
            float speed,
            Vector3 heading
        )
        {
            Puck pk = world.Puck;
            pk.Carrier = g.Id;
            pk.LastTouch = g.Id;
            pk.Velocity = Vector3.Zero;
            state.HeldSince = world.Time;
            state.SeenAt = float.NaN;
            world.Events.Publish(new ShotSaved(world.Time, g.Side, g.Id, heading, speed));
            GoalieDemo.Saved(world, caught: true);
        }

        private static Vector3 Flat(Vector3 v)
        {
            return v with { Y = 0 };
        }

        private static void Move(Player g, Vector3 target, float speed, float dt)
        {
            Vector3 delta = target - g.Position;
            delta.Y = 0;
            float distance = delta.Length();
            float step = MathF.Min(distance, speed * dt);
            Vector3 velocity = distance > 1e-4f ? delta / distance * (step / dt) : Vector3.Zero;
            g.Velocity = velocity;
            g.Position += velocity * dt;
        }

        /// <summary>Turns to the puck, but never more than 70 degrees off square to the ice.</summary>
        private static void Face(Player g, int end, Vector3 puck)
        {
            float square = -end * MathF.PI / 2;
            Vector3 d = puck - g.Position;
            float want = d.LengthSquared() < 0.01f ? square : SimMath.Atan2(d.X, d.Z);
            float off = MathF.IEEERemainder(want - square, 2 * MathF.PI);
            g.Heading = square + Math.Clamp(off, -1.22f, 1.22f);
        }

        public void DrawScene(World world, RenderContext render)
        {
            renderer.DrawScene(world, render);
        }

        public void DrawMask(World world, RenderContext render)
        {
            renderer.DrawMask(world, render);
        }

        public void DrawHud(World world, RenderContext render)
        {
            GoalieDemo.DrawHud(world);
        }

        public void Dispose()
        {
            renderer.Dispose();
        }
    }
}
