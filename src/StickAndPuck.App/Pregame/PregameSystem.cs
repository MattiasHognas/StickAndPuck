using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.AI.Positioning;
using StickAndPuck.App.Core;
using StickAndPuck.App.Hud;
using StickAndPuck.App.Rules.Match;
using static Raylib_cs.Raylib;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Pregame
{
    /// <summary>The pre-game warm-up. While a match that someone will play is in its intro, this holds the
    /// intro open and skates both squads round their own ends on <see cref="WarmupRoute"/>s, using
    /// the skating and the AI's steering so everyone moves at their own pace with the skate clip. A
    /// human's skater joins the loops until the human moves the stick. Space, enter, or A or start on a
    /// pad first skips the arena flyover (<see cref="ArenaFlyover"/>) if it is still playing, then ends
    /// the warm-up (<see cref="PregameFlow.Start"/>): the skaters gather inside their blue line
    /// and the opening faceoff follows. Runs after the AI and before skating, so its commands win.</summary>
    internal sealed class PregameSystem : ISystem
    {
        private const float GatherSeconds = 2.5f; // from start pressed to the faceoff, enough to skate to the blue line
        private const float StickDead = 0.15f; // a human stick shorter than this leaves the skater on its loop
        private const float Lead = 1f; // the chased point runs this many seconds ahead, so nobody eases off onto it

        private Maybe<IntroCardArt> introCard;

        public void Update(World world, float dt)
        {
            if (world.Match.Phase != MatchPhase.Intro || !PregameFlow.Wanted(world))
            {
                return;
            }

            WarmupState s = PregameFlow.State(world);
            FlowState flow = MatchFlow.State(world);
            if (!s.Running)
            {
                s.Running = true;
                Console.WriteLine("SAP: warm-up: both squads skate until start is pressed");
            }
            s.Elapsed += dt;

            if (!s.Started)
            {
                flow.Timer = GatherSeconds; // hold the intro
                if (s.Elapsed >= PregameFlow.AutoStartSeconds)
                {
                    _ = PregameFlow.Start(world);
                }
                else if (StartPressed(s))
                {
                    s.Armed = false; // a press that skips the flyover must be let go before the next one starts the game
                    _ = PregameFlow.Press(world);
                }
            }

            foreach (Side side in new[] { Side.Home, Side.Away })
            {
                int end = FaceoffFormation.DefendedEnd(world.Match, side);
                int i = 0;
                foreach (Player p in world.OnIce(side).Where(p => !p.IsGoalie))
                {
                    if (!s.Routes.TryGetValue(p.Id, out WarmupRoute route))
                    {
                        s.Routes[p.Id] = route = WarmupRoute.For(i, end);
                    }

                    Vector2 target = s.Started
                        ? WarmupRoute.Gather(i, end)
                        : route.At(s.Elapsed + Lead);
                    i++;
                    if (p.Slot >= 0 && p.Command.Move.Length() >= StickDead)
                    {
                        continue; // the human skates on its own
                    }

                    p.Command = Steering.Toward(p, target, s.Started ? 2f : 1.2f);
                }
            }
        }

        public void DrawHud(World world, RenderContext render)
        {
            if (!PregameFlow.IsWarmingUp(world))
            {
                return;
            }

            if (world.Switches.IntroCardPreview)
            {
                // SAP_SCENE=introcard: the A22 card art with made-up numbers, loaded on first use
                if (!introCard.HasValue)
                {
                    introCard = IntroCardArt.Load(HudArt.Load());
                }

                if (introCard.TryGet(out IntroCardArt card))
                {
                    Dev.IntroCardPreview.Draw(world, card);
                }

                return;
            }

            // a blinking prompt under the intro card
            if ((int)(world.Time * 2) % 2 == 0)
            {
                return;
            }

            string text = Text.Get(K.WarmUp);
            int w = MeasureText(text, 10);
            DrawRectangle(((Game.Width - w) / 2) - 4, 48, w + 8, 14, new Color(24, 19, 31, 255));
            DrawText(text, (Game.Width - w) / 2, 50, 10, new Color(239, 224, 194, 255));
        }

        /// <summary>Whether start was pressed this step: space, enter, or A or start on either pad, once every one of them has been seen up.</summary>
        private static bool StartPressed(WarmupState s)
        {
            if (!IsWindowReady())
            {
                return false;
            }

            bool down =
                DeviceInput.KeyDown(KeyboardKey.Space)
                || DeviceInput.KeyDown(KeyboardKey.Enter)
                || DeviceInput.KeyDown(KeyboardKey.KpEnter);
            for (int pad = 0; pad < 2 && !down; pad++)
            {
                down =
                    DeviceInput.PadAvailable(pad)
                    && (
                        DeviceInput.PadDown(pad, GamepadButton.RightFaceDown)
                        || DeviceInput.PadDown(pad, GamepadButton.MiddleRight)
                    );
            }

            if (!down)
            {
                s.Armed = true;
                return false;
            }
            return s.Armed;
        }
    }
}
