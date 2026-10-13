using Raylib_cs;
using StickAndPuck.App.AI;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules;
using static Raylib_cs.Raylib;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Pregame;

/// <summary>The pre-game warm-up. While a match that someone will play is in its intro, this holds the
/// intro open and skates both squads round their own ends on <see cref="WarmupRoute"/>s, using
/// the skating and the AI's steering so everyone moves at their own pace with the skate clip. A
/// human's skater joins the loops until the human moves the stick. Space, enter, or A or start on a
/// pad ends the warm-up (<see cref="Pregame.Start"/>): the skaters gather inside their blue line
/// and the opening faceoff follows. Runs after the AI and before skating, so its commands win.</summary>
internal sealed class PregameSystem : ISystem
{
    const float GatherSeconds = 2.5f;     // from start pressed to the faceoff, enough to skate to the blue line
    const float StickDead = 0.15f;        // a human stick shorter than this leaves the skater on its loop
    const float Lead = 1f;                // the chased point runs this many seconds ahead, so nobody eases off onto it

    public void Update(World world, float dt)
    {
        if (world.Match.Phase != MatchPhase.Intro || !Pregame.Wanted(world)) return;
        var s = Pregame.State(world);
        var flow = MatchFlow.State(world);
        if (!s.Running)
        {
            s.Running = true;
            Console.WriteLine("SAP: warm-up: both squads skate until start is pressed");
        }
        s.Elapsed += dt;

        if (!s.Started)
        {
            flow.Timer = GatherSeconds;    // hold the intro
            if (StartPressed(s) || s.Elapsed >= Pregame.AutoStartSeconds) Pregame.Start(world);
        }

        foreach (var side in new[] { Side.Home, Side.Away })
        {
            int end = FaceoffFormation.DefendedEnd(world.Match, side);
            int i = 0;
            foreach (var p in world.OnIce(side).Where(p => !p.IsGoalie))
            {
                if (!s.Routes.TryGetValue(p.Id, out var route)) s.Routes[p.Id] = route = WarmupRoute.For(i, end);
                var target = s.Started ? WarmupRoute.Gather(i, end) : route.At(s.Elapsed + Lead);
                i++;
                if (p.Slot >= 0 && p.Command.Move.Length() >= StickDead) continue;   // the human skates on its own
                p.Command = Steering.Toward(p, target, s.Started ? 2f : 1.2f);
            }
        }
    }

    public void DrawHud(World world, RenderContext render)
    {
        if (!Pregame.IsWarmingUp(world)) return;
        // a blinking prompt under the intro card
        if ((int)(world.Time * 2) % 2 == 0) return;
        string text = Text.Get(K.WarmUp);
        int w = MeasureText(text, 10);
        DrawRectangle((Game.Width - w) / 2 - 4, 48, w + 8, 14, new Color(24, 19, 31, 255));
        DrawText(text, (Game.Width - w) / 2, 50, 10, new Color(239, 224, 194, 255));
    }

    /// <summary>Whether start was pressed this step: space, enter, or A or start on either pad, once every one of them has been seen up.</summary>
    static bool StartPressed(WarmupState s)
    {
        if (!IsWindowReady()) return false;
        bool down = DeviceInput.KeyDown(KeyboardKey.Space) || DeviceInput.KeyDown(KeyboardKey.Enter) || DeviceInput.KeyDown(KeyboardKey.KpEnter);
        for (int pad = 0; pad < 2 && !down; pad++)
            down = DeviceInput.PadAvailable(pad) && (DeviceInput.PadDown(pad, GamepadButton.RightFaceDown) || DeviceInput.PadDown(pad, GamepadButton.MiddleRight));
        if (!down) { s.Armed = true; return false; }
        return s.Armed;
    }
}
