using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Hud;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Rules.Overtime;
using static Raylib_cs.Raylib;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Screens
{
    /// <summary>Start screen, settings, team select, pause, results and standings. Owns
    /// <see cref="MatchState.Paused"/>. Runs only when the start scene is picked (the default), so
    /// every other SAP_SCENE behaves as before. In a match it pauses on escape, P or start, notes the
    /// goals, and a few seconds after the final whistle hands the result to the results screen.</summary>
    internal sealed class ScreenSystem : ISystem, IDisposable
    {
        /// <summary>Seconds the FINAL banner shows before the results screen.</summary>
        private const float ResultsDelay = 3;

        private readonly MenuInput input = new();
        private readonly List<GoalLine> goals = [];
        private readonly int[] score = new int[2];
        private Maybe<ScreenArt> art;
        private Maybe<Screen> lastTop;
        private bool sawShootout;
        private float finalAt = float.PositiveInfinity;

        public bool RunsWhilePaused => true;

        public void Start(World world)
        {
            if (!world.App.Screens.Active)
            {
                return;
            }

            SetExitKey(KeyboardKey.Null); // escape pauses and goes back; Quit on the start screen ends the game
            art = ScreenArt.Load();
            input.Swallow(world.App.MenuScript, world.App.Screens.Settings);
        }

        public void Update(World world, float dt)
        {
            if (!world.App.Screens.Active)
            {
                return;
            }

            Maybe<Screen> top = world.App.Screens.Top;
            UpdateTopScreen(world, top);

            bool inMatch = world.App.Screens.Playing.HasValue && MatchFlow.IsRunning(world);
            world.Match.Paused = !inMatch || top.HasValue;
            IReadOnlySet<MenuButton> press = input.Next(
                world.App.MenuScript,
                world.App.Screens.Settings,
                top.HasValue,
                dt
            );
            if (top.TryGet(out Screen screen))
            {
                screen.Update(world, press);
                if (!world.App.Screens.Top.HasValue && inMatch)
                {
                    Resumed(world);
                }

                return;
            }
            if (!inMatch)
            {
                return;
            }

            UpdateMatch(world, press);
        }

        private void UpdateTopScreen(World world, Maybe<Screen> top)
        {
            bool same = top.TryGet(out Screen shown)
                ? lastTop.Is(last => ReferenceEquals(last, shown))
                : !lastTop.HasValue;
            if (!same)
            {
                input.Swallow(world.App.MenuScript, world.App.Screens.Settings); // a button still held from the last screen does not press here
            }

            lastTop = top;
        }

        private void UpdateMatch(World world, IReadOnlySet<MenuButton> press)
        {
            Watch(world);
            // The replay flag is shared, so the screens never leave a match or pause while a replay
            // plays (a winning goal's replay plays out first); a stale flag would hold the next faceoff
            if (world.ReplayPlaying)
            {
                return;
            }

            if (world.Match.Phase == MatchPhase.Final)
            {
                HandleFinal(world);
                return;
            }

            if (press.Contains(MenuButton.Pause))
            {
                Console.WriteLine("SAP: screens: paused");
                world.App.Screens.Push(new PauseScreen(world.App));
                world.Match.Paused = true;
            }
        }

        private void HandleFinal(World world)
        {
            if (float.IsPositiveInfinity(finalAt))
            {
                finalAt = world.Time;
            }

            if (world.Time - finalAt >= ResultsDelay)
            {
                world.App.Screens.ShowResults(Result(world));
            }
        }

        public void DrawHud(World world, RenderContext render)
        {
            if (!art.TryGet(out ScreenArt loaded) || !world.App.Screens.Top.TryGet(out Screen top))
            {
                return;
            }

            if (top.Dims)
            {
                ScreenArt.Dim();
            }

            top.Draw(loaded, world);
        }

        public void Dispose()
        {
            if (art.TryGet(out ScreenArt loaded))
            {
                loaded.Unload();
            }

            art = Maybe.None<ScreenArt>();
        }

        /// <summary>Back to play: nobody's buttons from the pause screen carry into the match.</summary>
        private static void Resumed(World world)
        {
            Console.WriteLine("SAP: screens: resumed");
            world.Match.Paused = false;
            foreach (Player p in world.Players)
            {
                p.Command = default;
            }
        }

        /// <summary>Notes the goals for the results screen. This system runs first in each step, before that
        /// step's events exist and after the last step's are cleared, so it watches the score instead:
        /// a goal is in when the score went up, and the puck's last touch is still the scorer's.</summary>
        private void Watch(World world)
        {
            MatchState m = world.Match;
            if (m.Phase == MatchPhase.Shootout)
            {
                sawShootout = true;
            }

            foreach (Side side in new[] { Side.Home, Side.Away })
            {
                int i = (int)side;
                if (m.Score[i] <= score[i])
                {
                    continue;
                }

                score[i] = m.Score[i];
                if (sawShootout)
                {
                    continue; // the shootout winner's goal is added at the end
                }

                string scorer = world.Setup.Club(side).Name;
                int touch = world.Puck.LastTouch;
                if (
                    touch >= 0
                    && touch < world.Players.Count
                    && world.Players[touch] is { } p
                    && p.Side == side
                )
                {
                    scorer = Text.Format(K.PlayerTag, p.Data.Number, p.Data.LastName);
                }

                float elapsed =
                    (
                        m.Period > world.Setup.Periods
                            ? OvertimeRules.PeriodLength(world)
                            : world.Setup.PeriodSeconds
                    ) - m.Clock;
                goals.Add(
                    new GoalLine(
                        side,
                        PeriodLabel(world),
                        MatchFlow.ClockText(Math.Max(0, elapsed)),
                        scorer
                    )
                );
            }
        }

        /// <summary>The period a goal went in: "1" to "3", "OT", then "2OT", "3OT" in a long playoff sudden death.</summary>
        private static string PeriodLabel(World world)
        {
            int overtime = world.Match.Period - world.Setup.Periods;
            return overtime <= 1
                ? HudSystem.PeriodText(world)
                : Text.Format(K.OvertimeNumber, overtime);
        }

        private MatchResult Result(World world)
        {
            MatchState m = world.Match;
            List<GoalLine> lines = [with(goals)];
            string decided = "";
            if (sawShootout)
            {
                decided = Text.Get(K.Shootout);
                Side winner =
                    m.Score[(int)Side.Home] > m.Score[(int)Side.Away] ? Side.Home : Side.Away;
                lines.Add(
                    new GoalLine(winner, Text.Get(K.PeriodSo), "", Text.Get(K.ShootoutWinner))
                );
            }
            else if (m.Period > world.Setup.Periods)
            {
                decided = PeriodLabel(world);
            }
            return new MatchResult(
                world.Setup,
                m.Score[(int)Side.Home],
                m.Score[(int)Side.Away],
                m.Shots[(int)Side.Home],
                m.Shots[(int)Side.Away],
                decided,
                lines,
                world.App.Screens.Playing.TryGet(out MatchPlan plan)
                    ? plan
                    : throw new InvalidOperationException(
                        "the results of a match that was not started"
                    )
            );
        }
    }
}
