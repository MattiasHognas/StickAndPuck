using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Fights;
using StickAndPuck.App.Rules.Match;

namespace StickAndPuck.App.Camera
{
    /// <summary>The close-up cameras. Four moments leave the broadcast view and move in close at a lower
    /// angle: a fight (the two fighters side by side, a jolt on every punch that lands), a goal (a
    /// push-in on the scorer before the replay cuts in), the warm-up (the skater a person controls, or
    /// the home squad) and the final horn (the winners' pile). The camera eases in from the gameplay
    /// camera and back out to it, with its target at the players' depth, so the camera system can pick
    /// the render scale: the fight and the goal land on 160x90, the warm-up and the pile on
    /// 240x135, and in every one a kit texel stays about one pixel.</summary>
    internal sealed class CloseUp
    {
        /// <summary>How far each shot sits from what it frames, in metres along its view (the gameplay camera is about 37).</summary>
        private const float FightDistance = 8.5f,
            GoalDistance = 8f,
            WarmupDistance = 11f,
            PileDistance = 13f;

        /// <summary>The close-ups look this many degrees down, lower than the gameplay camera's 28.2, so faces and kits show.</summary>
        private const float TiltDegrees = 15f;

        /// <summary>The close-ups aim at chest height.</summary>
        private const float Chest = 1.05f;

        /// <summary>The pile is framed from above the players' heads, so it sits under the FINAL banner and the raised sticks fit.</summary>
        private const float PileAim = 1.6f;

        /// <summary>How long a close-up takes to come in and to go back out, in seconds.</summary>
        private const float InSeconds = 0.7f,
            OutSeconds = 0.6f;

        /// <summary>The goal push-in waits out the hit-stop, then comes in over the rest of the celebration before the replay.</summary>
        private const float GoalDelay = 0.15f,
            GoalInSeconds = 1.1f;

        /// <summary>The final-horn close-up waits for the winners to reach their goalie.</summary>
        private const float PileDelay = 1.2f;

        /// <summary>How quickly a close-up follows a moving subject: the settle time of its easing, in seconds.</summary>
        private const float FollowSeconds = 0.5f;

        /// <summary>A landed punch jolts the fight camera this far toward the fighters, in metres, easing back over <see cref="JoltSeconds"/>.</summary>
        private const float Jolt = 0.6f,
            JoltSeconds = 0.25f;

        private Shot shot;
        private float goalSince = float.NaN,
            jolt,
            lastLanded = float.NaN;
        private int scorer = -1;
        private Vector3 subject,
            goalSpot;

        /// <summary>The moment a close-up frames.</summary>
        private enum Shot
        {
            None,
            Fight,
            Goal,
            Warmup,
            Pile,
        }

        /// <summary>Gets how far the close-up has come in, 0 (the gameplay camera) to 1.</summary>
        public float Weight { get; private set; }

        /// <summary>Moves the close-up one step: picks the shot, follows its subject and eases in or out.</summary>
        public void Update(World world, float dt, float replayWaiting)
        {
            if (world.ReplayPlaying)
            {
                Weight = 0;
                shot = Shot.None;
                goalSince = float.NaN;
                return;
            }

            NoteGoal(world, replayWaiting);
            Shot want = Pick(world, replayWaiting, out Vector3 spot);
            if (want != Shot.None && (shot == Shot.None || Weight <= 0 || want == shot))
            {
                if (shot != want)
                {
                    subject = spot;
                }

                shot = want;
            }

            bool on = want != Shot.None && want == shot;
            if (on)
            {
                subject += (spot - subject) * (1 - MathF.Exp(-dt * 4f / FollowSeconds));
            }

            float rate =
                (shot == Shot.Goal && on)
                    ? (dt / GoalInSeconds)
                    : (on ? (dt / InSeconds) : (dt / OutSeconds));
            Weight = on ? Math.Min(1, Weight + rate) : Math.Max(0, Weight - rate);
            if (Weight <= 0)
            {
                shot = Shot.None;
            }

            Punches(world, dt);
        }

        /// <summary>Blends the gameplay camera toward the close-up.</summary>
        public Camera3D Apply(Camera3D camera)
        {
            if (Weight <= 0)
            {
                return camera;
            }

            float t = Weight * Weight * (3 - (2 * Weight));
            Vector3 look = Forward();
            float distance = Distance(shot) - (jolt * Jolt);
            Vector3 closePos = subject - (look * distance);

            Vector3 dir = Vector3.Normalize(
                Vector3.Lerp(Vector3.Normalize(camera.Target - camera.Position), look, t)
            );
            Vector3 pos = Vector3.Lerp(camera.Position, closePos, t);
            camera.Position = pos;
            camera.Target = pos + (dir * Math.Max(1f, Vector3.Dot(subject - pos, dir)));
            return camera;
        }

        /// <summary>The close-up's view: straight at the far boards like the gameplay camera, so the fighters stand side by side, but lower.</summary>
        private static Vector3 Forward()
        {
            float down = MathF.PI * TiltDegrees / 180f;
            return new Vector3(0, -MathF.Sin(down), -MathF.Cos(down));
        }

        private static float Distance(Shot s)
        {
            return s switch
            {
                Shot.Fight => FightDistance,
                Shot.Goal => GoalDistance,
                Shot.Warmup => WarmupDistance,
                Shot.None or Shot.Pile => PileDistance,
                _ => throw new NotSupportedException($"No distance for shot {s}"),
            };
        }

        /// <summary>The shot this step wants and where it looks, in order: a fight, a goal, the final horn, the warm-up.</summary>
        private Shot Pick(World world, float replayWaiting, out Vector3 spot)
        {
            spot = default;
            if (Fighting.Current(world).TryGet(out Fight fight))
            {
                spot = fight.Spot with { Y = Chest };
                return Shot.Fight;
            }
            if (!float.IsNaN(goalSince) && replayWaiting >= GoalDelay)
            {
                spot = (
                    scorer >= 0 && scorer < world.Players.Count
                        ? world.Players[scorer].Position
                        : goalSpot
                ) with
                {
                    Y = Chest,
                };
                return Shot.Goal;
            }
            if (
                MatchFlow.State(world).Celebration.TryGet(out Celebration.CelebrationState c)
                && c.Elapsed >= PileDelay
            )
            {
                spot = c.Pile with { Y = PileAim };
                return Shot.Pile;
            }
            if (
                Pregame.PregameFlow.IsWarmingUp(world) && WarmupSubject(world).TryGet(out Vector3 p)
            )
            {
                spot = p with { Y = Chest };
                return Shot.Warmup;
            }
            return Shot.None;
        }

        /// <summary>Remembers who scored, so the push-in can find him; forgets once the replay is no longer waiting.</summary>
        private void NoteGoal(World world, float replayWaiting)
        {
            if (float.IsNaN(replayWaiting))
            {
                goalSince = float.NaN;
                return;
            }
            if (!float.IsNaN(goalSince))
            {
                return;
            }

            goalSince = replayWaiting;
            scorer = -1;
            goalSpot = world.Puck.Position;
            foreach (GoalScored e in world.Events.Of<GoalScored>())
            {
                scorer = e.ScorerId;
                goalSpot = e.Position;
            }
        }

        /// <summary>The warm-up frames the skater a person controls, else the middle of the home squad.</summary>
        private static Maybe<Vector3> WarmupSubject(World world)
        {
            if (world.Players.Find(p => p.OnIce && p.Slot >= 0) is { } human)
            {
                return human.Position;
            }

            List<Player> home = [.. world.OnIce(Side.Home).Where(p => !p.IsGoalie)];
            if (home.Count == 0)
            {
                return Maybe.None<Vector3>();
            }

            Vector3 sum = Vector3.Zero;
            foreach (Player p in home)
            {
                sum += p.Position;
            }

            return sum / home.Count;
        }

        /// <summary>A punch that lands (a hook in the fight) jolts the camera in, and it eases back.</summary>
        private void Punches(World world, float dt)
        {
            jolt = Math.Max(0, jolt - (dt / JoltSeconds));
            if (shot != Shot.Fight || !Fighting.Current(world).TryGet(out Fight fight))
            {
                lastLanded = float.NaN;
                return;
            }
            if (
                fight.LandedAt > lastLanded
                || (float.IsNaN(lastLanded) && !float.IsNaN(fight.LandedAt))
            )
            {
                lastLanded = fight.LandedAt;
                jolt = 1;
            }
        }
    }
}
