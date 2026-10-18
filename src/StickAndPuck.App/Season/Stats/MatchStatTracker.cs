using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.Stats
{
    /// <summary>Follows one match step by step: who was on the ice, the shots, hits and saves from the event
    /// log, the goals, and the assists worked out from the puck's touches, since the event log carries
    /// no assist of its own: the last one or two teammates to touch the puck before the scorer, with
    /// no opponent touch in between and within <see cref="AssistWindow"/> seconds, get the assists.
    /// Shootout goals go to the score but to nobody's line, as in the SHL.</summary>
    public sealed class MatchStatTracker
    {
        /// <summary>Seconds of game time within which a teammate's touch before a goal counts as an assist.</summary>
        public const float AssistWindow = 12f;

        /// <summary>Seconds within which the last attacker's touch before an own goal or a deflection counts as the goal.</summary>
        private const float DeflectionWindow = 2.5f;

        private const int TouchHistory = 12;

        private readonly Dictionary<int, MatchStatLine> lines = [];
        private readonly List<(int Id, Side Side, float Time)> touches = [];
        private int lastTouch = -1;
        private bool shootout;

        /// <summary>Gets the sheet so far, with no player of the game until <see cref="Finish"/>.</summary>
        public MatchStatSheet Sheet { get; } = new();

        /// <summary>Gets the world being followed, or None before the first step.</summary>
        public Maybe<World> World { get; private set; }

        /// <summary>Follows one step: call after the systems that publish the step's events have run.</summary>
        public void Observe(World w)
        {
            if (!World.TryGet(out World followed) || !ReferenceEquals(w, followed))
            {
                Begin(w);
            }

            UpdateMatchState(w);
            UpdateTouchHistory(w);
            ObserveEvents(w);
        }

        private void UpdateMatchState(World w)
        {
            if (w.Match.Phase == MatchPhase.Shootout)
            {
                shootout = true;
            }

            foreach (Player p in w.Players)
            {
                if (p.OnIce && lines.TryGetValue(p.Id, out MatchStatLine l))
                {
                    l.Played = true;
                }
            }
        }

        private void UpdateTouchHistory(World w)
        {
            int t = w.Puck.LastTouch;
            if (t != lastTouch && t >= 0 && t < w.Players.Count)
            {
                touches.Add((t, w.Players[t].Side, w.Time));
                if (touches.Count > TouchHistory)
                {
                    touches.RemoveAt(0);
                }
            }
            lastTouch = t;
        }

        private void ObserveEvents(World w)
        {
            foreach (MatchEvent e in w.Events.Current)
            {
                switch (e)
                {
                    case ShotTaken shot when !shootout:
                        HandleShotTaken(w, shot);
                        break;
                    case CheckLanded check:
                        HandleCheckLanded(check);
                        break;
                    case ShotSaved save:
                        HandleShotSaved(save);
                        break;
                    case GoalScored goal when !shootout:
                        Goal(w, goal.Side, goal.ScorerId, goal.Time);
                        break;
                    case PuckDropped:
                        touches.Clear();
                        break;
                    case ShotTaken
                    or GoalScored
                    or PassMade
                    or MatchEnded
                    or PeriodEnded
                    or WhistleBlown
                    or PuckHitPost
                    or PuckHitBoards
                    or LineChanged
                    or FightStarted
                    or PenaltyCalled
                    or IcingCalled
                    or OffsideCalled
                    or FaceoffViolated:
                        break;
                    default:
                        throw new NotSupportedException(
                            $"Unhandled match event {e.GetType().Name}"
                        );
                }
            }
        }

        private void HandleShotTaken(World w, ShotTaken shot)
        {
            if (Line(shot.ShooterId).TryGet(out MatchStatLine shooter))
            {
                if (PenaltyKillClear.IsClear(w, shot))
                {
                    shooter.Clears++;
                }
                else
                {
                    shooter.Shots++;
                }
            }
        }

        private void HandleCheckLanded(CheckLanded check)
        {
            if (Line(check.CheckerId).TryGet(out MatchStatLine checker))
            {
                checker.Hits++;
            }
        }

        private void HandleShotSaved(ShotSaved save)
        {
            if (Line(save.GoalieId).TryGet(out MatchStatLine goalie))
            {
                goalie.Saves++;
            }
        }

        /// <summary>Closes the sheet with the final score and picks the player of the game.</summary>
        public MatchStatSheet Finish()
        {
            if (World.TryGet(out World w))
            {
                Sheet.HomeGoals = w.Match.Score[(int)Side.Home];
                Sheet.AwayGoals = w.Match.Score[(int)Side.Away];
            }
            Sheet.PickPlayerOfGame();
            return Sheet;
        }

        private void Begin(World w)
        {
            World = w;
            lines.Clear();
            touches.Clear();
            lastTouch = -1;
            shootout = false;
            Sheet.Home = w.Setup.Home.Id;
            Sheet.Away = w.Setup.Away.Id;
            Sheet.Lines.Clear();
            Sheet.PlayerOfGame = Maybe.None<MatchStatLine>();
            foreach (Player p in w.Players)
            {
                MatchStatLine line = MatchStatLine.For(w.Setup.Club(p.Side).Id, p.Side, p.Data);
                lines[p.Id] = line;
                Sheet.Lines.Add(line);
            }
        }

        private Maybe<MatchStatLine> Line(int id)
        {
            return lines.TryGetValue(id, out MatchStatLine l) ? l : Maybe.None<MatchStatLine>();
        }

        private void Goal(World w, Side side, int playerId, float time)
        {
            int scorer =
                playerId >= 0 && playerId < w.Players.Count && w.Players[playerId].Side == side
                    ? playerId
                    : -1;
            if (scorer < 0)
            {
                // an own goal or a deflection: the last attacker to touch the puck just before gets it
                for (int i = touches.Count - 1; i >= 0; i--)
                {
                    if (time - touches[i].Time > DeflectionWindow)
                    {
                        break;
                    }

                    if (touches[i].Side == side)
                    {
                        scorer = touches[i].Id;
                        break;
                    }
                }
            }
            if (scorer < 0 || !Line(scorer).TryGet(out MatchStatLine line))
            {
                touches.Clear();
                return;
            }
            line.Goals++;

            List<int> assists = [];
            int at = touches.Count - 1;
            while (at >= 0 && touches[at].Id == scorer)
            {
                at--; // the scorer's own touches at the end
            }

            for (; at >= 0 && assists.Count < 2; at--)
            {
                (int id, Side s, float t) = touches[at];
                if (s != side || id == scorer || time - t > AssistWindow)
                {
                    break;
                }

                if (!assists.Contains(id))
                {
                    assists.Add(id);
                }
            }
            foreach (int id in assists)
            {
                if (Line(id).TryGet(out MatchStatLine helper))
                {
                    helper.Assists++;
                }
            }

            touches.Clear();
        }
    }
}
