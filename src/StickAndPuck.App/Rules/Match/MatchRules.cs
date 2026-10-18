using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match.Dev;
using StickAndPuck.App.Rules.Overtime;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Rules.Match
{
    /// <summary>Faceoffs, the clock, three periods, goals and restarts. Owns <see cref="World.Match"/> and
    /// publishes Faceoff, PeriodEnd and MatchEnd; counts the Goal events physics publishes. Runs only
    /// in a world started with <see cref="MatchFlow.Begin"/>. See <see cref="MatchFlow"/> for the rules it keeps.</summary>
    internal sealed class MatchRules : ISystem
    {
        public void Update(World world, float dt)
        {
            if (!MatchFlow.IsRunning(world))
            {
                return;
            }

            MatchDemo.Update(world);
            FlowState s = MatchFlow.State(world);
            MatchState m = world.Match;
            // From the horn the celebration poses every player, then hands over to the final
            if (Celebration.Update(world, s, dt))
            {
                return;
            }

            UpdatePhase(world, s, m, dt);
        }

        private static void UpdatePhase(World world, FlowState s, MatchState m, float dt)
        {
            switch (m.Phase)
            {
                case MatchPhase.Intro:
                    if (Wait(s, dt))
                    {
                        MatchFlow.SetUpFaceoff(world, RinkSpec.CentreDot);
                    }

                    break;
                case MatchPhase.Faceoff:
                    // players hold their spots until the puck drops
                    FaceoffFormation.Place(world, s.Dot);
                    FaceoffDraw.Step(world, s, dt);
                    if (!s.Draw.Dropped && !s.RefereeDrops && Wait(s, dt))
                    {
                        _ = MatchFlow.Drop(world);
                    }

                    break;
                case MatchPhase.Play:
                    Play(world, s, dt);
                    break;
                case MatchPhase.Stopped:
                    if (Wait(s, dt))
                    {
                        MatchFlow.SetUpFaceoff(world, s.Dot);
                    }

                    break;
                case MatchPhase.Goal:
                    // The goal replay holds the restart; the faceoff follows a second after it ends
                    if (world.ReplayPlaying)
                    {
                        s.Timer = 1f;
                    }
                    else if (Wait(s, dt))
                    {
                        MatchFlow.SetUpFaceoff(world, RinkSpec.CentreDot);
                    }

                    break;
                case MatchPhase.Intermission:
                    if (Wait(s, dt))
                    {
                        MatchFlow.StartPeriod(world, m.Period + 1, world.Setup.PeriodSeconds);
                    }

                    break;
                case MatchPhase.Overtime:
                    break;
                case MatchPhase.Shootout:
                    break;
                case MatchPhase.Final:
                    break;
                default:
                    // Overtime and Shootout belong to the overtime rules until they call StartPeriod; Final is the end
                    break;
            }
        }

        public void DrawHud(World world, RenderContext render)
        {
            // a plain readout for checking the flow; SAP_MATCH_DEBUG=1 turns it on
            if (!world.Switches.MatchLine || !MatchFlow.IsRunning(world))
            {
                return;
            }

            MatchState m = world.Match;
            string text = string.Create(
                CultureInfo.InvariantCulture,
                $"{world.Setup.Home.ShortName} {m.Score[0]} - {m.Score[1]} {world.Setup.Away.ShortName}   P{m.Period} {MatchFlow.ClockText(m.Clock)}   {m.Phase}"
            );
            DrawRectangle(
                4,
                252,
                MeasureText(text, 10) + 8,
                14,
                new Raylib_cs.Color(24, 19, 31, 255)
            );
            DrawText(text, 8, 254, 10, new Raylib_cs.Color(239, 224, 194, 255));
        }

        private static bool Wait(FlowState s, float dt)
        {
            s.Timer -= dt;
            return s.Timer <= 0;
        }

        private static void Play(World world, FlowState s, float dt)
        {
            MatchState m = world.Match;
            // goals first: physics ran earlier this step, so a puck over the line before the clock ran out counts
            foreach (ShotSaved save in world.Events.Of<ShotSaved>().Where(Goalie.GoalieModel.OnNet))
            {
                m.Shots[(int)Other(save.Side)]++;
            }

            if (world.Events.Of<GoalScored>().FirstOrDefault() is { } goal)
            {
                Side scorer = goal.Side;
                m.Score[(int)scorer]++;
                m.Shots[(int)scorer]++;
                m.Phase = MatchPhase.Goal;
                world.Puck.Live = false;
                s.Timer = MatchTuning.GoalSeconds;
                Log(
                    world,
                    s,
                    $"goal {world.Setup.Club(scorer).ShortName} by #{Number(world, goal.ScorerId)}"
                );
                // sudden death after regulation
                if (m.Period > world.Setup.Periods)
                {
                    EndMatch(world, s);
                }

                return;
            }

            if (Frozen(world, s, dt))
            {
                return;
            }

            m.Clock -= dt;
            if (m.Clock > 0)
            {
                return;
            }

            m.Clock = 0;
            EndPeriod(world, s);
        }

        /// <summary>A goalie holding the puck long enough freezes it: whistle, faceoff at the nearest end-zone dot.</summary>
        private static bool Frozen(World world, FlowState s, float dt)
        {
            Puck pk = world.Puck;
            bool held =
                pk.Carrier >= 0
                && pk.Carrier < world.Players.Count
                && world.Players[pk.Carrier].IsGoalie;
            s.Held = held ? s.Held + dt : 0;
            if (s.Held < MatchTuning.FreezeSeconds)
            {
                return false;
            }

            s.Held = 0;
            return MatchFlow.Whistle(world, MatchFlow.NearestDot(pk.Position));
        }

        private static void EndPeriod(World world, FlowState s)
        {
            MatchState m = world.Match;
            world.Puck.Live = false;
            world.Events.Publish(new PeriodEnded(world.Time, m.Period));
            Log(world, s, string.Create(CultureInfo.InvariantCulture, $"end of period {m.Period}"));
            bool tied = m.Score[0] == m.Score[1];
            if (m.Period < world.Setup.Periods)
            {
                m.Phase = MatchPhase.Intermission;
                s.Timer = MatchTuning.IntermissionSeconds;
            }
            else if (
                tied
                && OvertimeFormat
                    .Of(world.Setup)
                    .AfterTie(m.Period - world.Setup.Periods)
                    .TryGet(out MatchPhase next)
            )
            {
                // a tie: the overtime rules set up the overtime skaters and calls StartPeriod, or runs the shootout, as the mode's format says
                m.Phase = next;
            }
            else
            {
                EndMatch(world, s);
            }
        }

        /// <summary>The horn: MatchWon or MatchTied, then the celebration holds the final for a while, or the final at once after a tie.</summary>
        internal static void EndMatch(World world, FlowState s)
        {
            MatchState m = world.Match;
            world.Puck.Live = false;
            SideOrNone winner =
                (m.Score[0] > m.Score[1])
                    ? SideOrNone.Home
                    : ((m.Score[1] > m.Score[0]) ? SideOrNone.Away : SideOrNone.None);
            m.Phase = Celebration.Begin(world, s, winner) ? MatchPhase.Stopped : MatchPhase.Final;
            world.Events.Publish(
                winner.TryGetSide(out Side w)
                    ? new MatchWon(world.Time, w)
                    : new MatchTied(world.Time)
            );
            Log(world, s, "final");
        }

        private static void Log(World world, FlowState s, string what)
        {
            if (!s.Log)
            {
                return;
            }

            MatchState m = world.Match;
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"SAP: {what}: {world.Setup.Home.ShortName} {m.Score[0]}-{m.Score[1]} {world.Setup.Away.ShortName}, P{m.Period} {MatchFlow.ClockText(m.Clock)}"
                )
            );
        }

        private static string Number(World world, int playerId)
        {
            return playerId >= 0 && playerId < world.Players.Count
                ? world.Players[playerId].Data.Number.ToString(CultureInfo.InvariantCulture)
                : "?";
        }

        private static Side Other(Side side)
        {
            return side == Side.Home ? Side.Away : Side.Home;
        }
    }
}
