using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.AI.Positioning;
using StickAndPuck.App.Camera;
using StickAndPuck.App.Core;
using StickAndPuck.App.Lines;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Screens;
using static Raylib_cs.Raylib;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Intermission
{
    /// <summary>Between periods the Zamboni comes out through the gate in the far boards, laps the ice
    /// spiralling in toward the centre and drives back out, and the next period's faceoff follows.
    /// While it drives, the AI skaters wait at their bench doors, the camera pans after the machine and
    /// enter, space or the A button skips it. The match's intermission pause is held open until the Zamboni is
    /// gone, the same way the goal replay holds the restart. Runs after the camera system, so its
    /// camera wins while the Zamboni is out and hands back smoothly before the faceoff. The rules'
    /// <see cref="RuleSet.Intermission"/> sets the length: Standard drives the whole route, Quick at double pace,
    /// Off sends no Zamboni. The same break runs before a playoff overtime. While it drives, a tag in the
    /// corner shows what follows, the time left and the skip button. A human club trailing after period 1 or 2
    /// gets the coach's one-choice talk in the break (<see cref="TrailingTalk"/>); its answer wins over the skip
    /// on the step it is given.</summary>
    /// <remarks>Initializes a new instance of the <see cref="IntermissionSystem"/> class with its own skip button, for the self test.</remarks>
    internal sealed class IntermissionSystem(Func<bool> skipPressed) : ISystem, IDisposable
    {
        /// <summary>Seconds of the intermission pause kept in hand while the Zamboni drives.</summary>
        private const float HoldSeconds = 0.5f;

        /// <summary>The pause between the Zamboni leaving (or the skip) and the faceoff set-up.</summary>
        private const float AfterSeconds = 1.2f;

        /// <summary>Skips are ignored for this long, so the button that ended the period does not skip it too.</summary>
        private const float SkipAfterSeconds = 0.5f;

        /// <summary>How long the camera takes to swing over to the Zamboni and back.</summary>
        private const float BlendSeconds = 1.2f;

        /// <summary>How far apart the waiting skaters stand along the boards.</summary>
        private const float WaitSpacing = 1.1f;

        private static readonly Color TagText = new(0xef, 0xe0, 0xc2, 255);
        private static readonly Color TagBack = new(0x18, 0x13, 0x1f, 255);

        private readonly Func<bool> skipPressed = skipPressed;
        private readonly ZamboniRenderer renderer = new();
        private Maybe<ScreenArt> art;
        private float cameraX,
            cameraVx,
            blend;

        /// <summary>Initializes a new instance of the <see cref="IntermissionSystem"/> class that reads the skip from the keyboard and gamepads.</summary>
        public IntermissionSystem()
            : this(SkipPressed) { }

        /// <summary>Gets the route every Zamboni drive follows.</summary>
        public static ZamboniRoute Route { get; } = new();

        /// <summary>Gets or sets the seconds into its drive the next Zamboni starts at. SAP_ZAMBONI_AT sets it for
        /// headless screenshots of a chosen moment; the scene sets it back to 0 once used.</summary>
        /// <summary>Gets the Zamboni's drive in this world while one is under way, or None.</summary>
        public Maybe<Drive> Current { get; private set; }

        public void Start(World world)
        {
            if (MatchFlow.IsRunning(world))
            {
                renderer.Load();
                art = ScreenArt.Load();
            }
        }

        public void Update(World world, float dt)
        {
            if (!MatchFlow.IsRunning(world))
            {
                return;
            }

            if (
                world.Match.Phase != MatchPhase.Intermission
                || world.Setup.Rules.Intermission == IntermissionLength.Off
            )
            {
                Current = Maybe.None<Drive>();
                blend = 0;
                TrailingTalk.Close(world);
                return;
            }

            FlowState flow = MatchFlow.State(world);
            if (!Current.TryGet(out Drive drive))
            {
                if (flow.Timer <= 0)
                {
                    return; // the next period is starting this step
                }

                drive = new Drive
                {
                    Seconds = world.Switches.ZamboniStartAt,
                    Period = world.Match.Period,
                    Rate = Pace(world.Setup.Rules.Intermission),
                    IntoOvertime = flow.IntoOvertime,
                };
                Current = drive;
                world.Switches.ZamboniStartAt = 0;
                if (
                    TrailingTalk
                        .Trailing(world, drive.Period, drive.IntoOvertime)
                        .TryGet(out Side behind)
                )
                {
                    TrailingTalk.Open(world, behind, drive.Period + 1);
                }

                cameraX = Route.At(Route.Distance(drive.Seconds)).X;
                cameraVx = 0;
            }

            bool answered = TrailingTalk.Update(world, dt);
            if (!drive.Finished)
            {
                drive.Seconds += dt * drive.Rate;
                drive.Shown += dt;
                if (!answered && drive.Shown >= SkipAfterSeconds && skipPressed())
                {
                    drive.Skipped = true;
                    drive.Finished = true;
                }
                else if (drive.Seconds >= Route.Duration)
                {
                    drive.Finished = true;
                }

                flow.Timer = drive.Finished ? AfterSeconds : Math.Max(flow.Timer, HoldSeconds);
                if (drive.Finished)
                {
                    TrailingTalk.Close(world); // the break is over: an unanswered talk goes unanswered
                }
            }

            if (!drive.Finished)
            {
                Wait(world);
            }

            Follow(world, drive, dt);
        }

        /// <summary>Gets how fast the Zamboni drives for an intermission length: Standard at its own speed, Quick at double.</summary>
        internal static float Pace(IntermissionLength length)
        {
            return length switch
            {
                IntermissionLength.Standard => 1f,
                IntermissionLength.Quick => 2f,
                IntermissionLength.Off => 1f,
                _ => throw new NotSupportedException("no Zamboni pace for " + length),
            };
        }

        /// <summary>Gets the real seconds an intermission of this length holds the next period back, skip aside:
        /// the whole drive at its pace and the pause after it, or the match flow's own short pause when Off.</summary>
        internal static float BreakSeconds(IntermissionLength length)
        {
            return length == IntermissionLength.Off
                ? MatchTuning.IntermissionSeconds
                : (Route.Duration / Pace(length)) + AfterSeconds;
        }

        /// <summary>Gets the real seconds left before the next faceoff set-up, while the drive is under way.</summary>
        internal static float Left(Drive drive)
        {
            return (Math.Max(0, Route.Duration - drive.Seconds) / drive.Rate) + AfterSeconds;
        }

        /// <summary>Gets the corner tag: what follows the break, the time left and the skip button.</summary>
        internal static string Prompt(Drive drive)
        {
            string left = MatchFlow.ClockText(Left(drive));
            string next = drive.IntoOvertime
                ? Text.Format(K.IntermissionOvertime, left)
                : Text.Format(K.IntermissionNext, drive.Period + 1, left);
            return next + "   " + Text.Get(K.Skip);
        }

        /// <summary>Keeps the AI skaters lined up along the boards at their bench doors, out of the Zamboni's way.</summary>
        private static void Wait(World world)
        {
            foreach ((Player p, Vector2 spot) in WaitSpots(world))
            {
                PositioningModel.Request(world, p, spot);
            }
        }

        /// <summary>Gets where each AI skater on the ice waits: spread along the boards either side of the bench door.</summary>
        internal static IEnumerable<(Player Player, Vector2 Spot)> WaitSpots(World world)
        {
            foreach (Side side in new[] { Side.Home, Side.Away })
            {
                Vector2 door = Squad.Door(side);
                List<Player> skaters =
                [
                    .. world.Players.Where(p =>
                        p.Side == side && p.OnIce && !p.IsGoalie && p.Slot < 0
                    ),
                ];
                for (int i = 0; i < skaters.Count; i++)
                {
                    yield return (
                        skaters[i],
                        new Vector2(
                            door.X + ((i - ((skaters.Count - 1) / 2f)) * WaitSpacing),
                            door.Y
                        )
                    );
                }
            }
        }

        /// <summary>Pans the gameplay camera after the Zamboni, swinging over from the follow camera and back.</summary>
        private void Follow(World world, Drive drive, float dt)
        {
            if (world.ReplayPlaying)
            {
                return;
            }

            Vector2 at = Route.At(Route.Distance(drive.Seconds));
            float wanted =
                drive.Finished
                || (
                    !Route.OnIce(Route.Distance(drive.Seconds))
                    && drive.Seconds > Route.Duration / 2
                )
                    ? 0
                    : 1;
            blend = Math.Clamp(blend + (Math.Sign(wanted - blend) * dt / BlendSeconds), 0, 1);
            if (wanted == 0 && blend == 0)
            {
                return;
            }

            Spring(ref cameraX, ref cameraVx, Math.Clamp(at.X, -MaxX(), MaxX()), dt);
            Camera3D own = BroadcastCamera.Straight(
                cameraX,
                BroadcastCamera.Back,
                BroadcastCamera.FovY
            );
            Camera3D follow = world.Camera;
            float w = blend * blend * (3 - (2 * blend));
            float x = Snap(follow.Position.X + ((own.Position.X - follow.Position.X) * w));
            world.Camera = follow with
            {
                Position = follow.Position with { X = x },
                Target = follow.Target with { X = x },
            };
        }

        public void DrawScene(World world, RenderContext render)
        {
            if (Showing().TryGet(out float d))
            {
                renderer.DrawScene(Route.At(d), Route.HeadingAt(d), render);
            }
        }

        public void DrawMask(World world, RenderContext render)
        {
            if (Showing().TryGet(out float d))
            {
                renderer.DrawMask(Route.At(d), Route.HeadingAt(d), render);
            }
        }

        public void DrawHud(World world, RenderContext render)
        {
            if (art.TryGet(out ScreenArt loaded))
            {
                TalkPanel.Draw(world, loaded);
            }

            if (
                !Current.TryGet(out Drive drive)
                || drive.Finished
                || drive.Shown < SkipAfterSeconds
            )
            {
                return;
            }

            string text = Prompt(drive);
            int w = MeasureText(text, 10) + 10;
            const int h = 13,
                y = Game.Height - h - 6;
            int x = Game.Width - w - 6;
            DrawRectangle(x, y, w, h, TagBack);
            DrawText(text, x + 5, y + 2, 10, TagText);
        }

        public void Dispose()
        {
            renderer.Dispose();
            if (art.TryGet(out ScreenArt loaded))
            {
                loaded.Unload();
                art = Maybe.None<ScreenArt>();
            }
        }

        /// <summary>Gets the distance along the route to draw at, or None while the Zamboni is out of sight behind the boards or gone.</summary>
        private Maybe<float> Showing()
        {
            if (!Current.TryGet(out Drive drive) || drive.Skipped)
            {
                return Maybe.None<float>();
            }

            float d = Route.Distance(drive.Seconds);
            return d > 0 && d < Route.Length ? d : Maybe.None<float>();
        }

        private static bool SkipPressed()
        {
            if (!IsWindowReady())
            {
                return false;
            }

            if (
                DeviceInput.KeyPressed(KeyboardKey.Enter)
                || DeviceInput.KeyPressed(KeyboardKey.KpEnter)
                || DeviceInput.KeyPressed(KeyboardKey.Space)
            )
            {
                return true;
            }

            for (int pad = 0; pad < 2; pad++)
            {
                if (
                    DeviceInput.PadAvailable(pad)
                    && DeviceInput.PadPressed(pad, GamepadButton.RightFaceDown)
                )
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The farthest the camera pans, as the follow camera keeps it: the end boards at the frame edge at centre ice, plus a margin.</summary>
        private static float MaxX()
        {
            Vector3 forward = BroadcastCamera.Forward;
            float depth = Vector3.Dot(
                new Vector3(0, -BroadcastCamera.Height, -BroadcastCamera.Back),
                forward
            );
            float half = depth * HalfTan();
            return Math.Max(0, RinkSpec.HalfLength + 1.5f - half);
        }

        /// <summary>Rounds a camera x to whole pixels at the far boards, like the follow camera, so the ad boards never shimmer.</summary>
        private static float Snap(float x)
        {
            Vector3 forward = BroadcastCamera.Forward;
            Vector3 board = new(0, RinkSpec.BoardHeight / 2, -RinkSpec.HalfWidth);
            float depth = Vector3.Dot(
                board - new Vector3(0, BroadcastCamera.Height, BroadcastCamera.Back),
                forward
            );
            float step = 2 * depth * HalfTan() / Game.Width;
            return MathF.Round(x / step, MidpointRounding.ToEven) * step;
        }

        private static float HalfTan()
        {
            return MathF.Tan(BroadcastCamera.FovY * MathF.PI / 360f) * Game.Width / Game.Height;
        }

        /// <summary>A critically damped spring that settles in about a second.</summary>
        private static void Spring(ref float value, ref float velocity, float target, float dt)
        {
            const float omega = 4f / 1.1f;
            float e = MathF.Exp(-omega * dt);
            float diff = value - target;
            float temp = (velocity + (omega * diff)) * dt;
            velocity = (velocity - (omega * temp)) * e;
            value = target + ((diff + temp) * e);
        }

        /// <summary>One Zamboni drive.</summary>
        internal sealed class Drive
        {
            /// <summary>Gets or sets the seconds since it set off.</summary>
            public float Seconds { get; set; }

            /// <summary>Gets or sets the seconds it has been on show, for the skip guard.</summary>
            public float Shown { get; set; }

            /// <summary>Gets the period that just ended.</summary>
            public int Period { get; init; }

            /// <summary>Gets how fast it drives, from the rules' intermission length: 1 for Standard, 2 for Quick.</summary>
            public float Rate { get; init; } = 1f;

            /// <summary>Gets a value indicating whether this is the break before a playoff overtime.</summary>
            public bool IntoOvertime { get; init; }

            /// <summary>Gets or sets a value indicating whether it is done, driven out or skipped.</summary>
            public bool Finished { get; set; }

            /// <summary>Gets or sets a value indicating whether it was skipped; it then disappears at once.</summary>
            public bool Skipped { get; set; }
        }
    }
}
