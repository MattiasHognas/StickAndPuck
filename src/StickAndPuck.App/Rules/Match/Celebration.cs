using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Lines;

namespace StickAndPuck.App.Rules.Match
{
    /// <summary>The end-of-game celebration. At the final horn of a decided game (regulation, overtime or
    /// the shootout) the winners swarm their goalie and celebrate, the losers drop to the ice where
    /// they stand or trudge off to their bench and bury their heads, both goalies included, and the
    /// camera holds on the pile. The match stays in <see cref="MatchPhase.Stopped"/> for
    /// <see cref="MatchTuning.CelebrationSeconds"/> and only then becomes <see cref="MatchPhase.Final"/>,
    /// so the result summary follows the celebration as it followed the horn before. The players are
    /// posed from here after the movement systems ran, so input and the AI cannot move them. A tie has
    /// no celebration. The skater renderer asks <see cref="Pose"/> for the clip to show.</summary>
    public static class Celebration
    {
        private const float PileRadius = 1.3f;
        private const float SkateSpeed = 7f;
        private const float TrudgeSpeed = 3.2f;
        private const float DropSpeed = 5f;
        private const float BrakeSeconds = 0.5f;
        private const float PileX = 10f;

        /// <summary>Gets a value indicating whether a celebration is on or has played in this match, so the final poses hold until the summary.</summary>
        public static bool Playing(World world)
        {
            return MatchFlow.State(world).Celebration.HasValue;
        }

        /// <summary>Gets the winners of the celebration under way.</summary>
        public static SideOrNone Winner(World world)
        {
            return MatchFlow.State(world).Celebration.TryGet(out CelebrationState c)
                ? c.Won.OrNone()
                : SideOrNone.None;
        }

        /// <summary>Gets the clip a skater shows in the celebration: "celebrate" for a winner at the pile, "fall"
        /// for a loser who dropped to the ice, "block" (head in the hands) for one who reached the bench,
        /// with the seconds since it started and whether it loops. None while the skater is still skating
        /// or standing as usual, and for a skater outside any celebration.</summary>
        public static Maybe<(string Clip, float Seconds, bool Loop)> Pose(World world, Player p)
        {
            if (
                !MatchFlow.State(world).Celebration.TryGet(out CelebrationState c)
                || !c.Parts.TryGetValue(p.Id, out Part part)
                || float.IsNaN(part.ClipSince)
            )
            {
                return Maybe.None<(string Clip, float Seconds, bool Loop)>();
            }

            float since = Math.Max(0, c.Elapsed - part.ClipSince);
            return part.Mood switch
            {
                Mood.Winner => ("celebrate", since, true),
                Mood.Drop => ("fall", since, false),
                Mood.SkateOff => ("block", since, false),
                Mood.Goalie => Maybe.None<(string Clip, float Seconds, bool Loop)>(),
                _ => throw new NotSupportedException("Unhandled celebration mood " + part.Mood),
            };
        }

        /// <summary>Gets a value indicating whether a goalie has slumped in the celebration: the losing goalie drops into the butterfly and stays down.</summary>
        public static bool Slumps(World world, Player goalie)
        {
            return MatchFlow.State(world).Celebration.TryGet(out CelebrationState c)
                && goalie.IsGoalie
                && goalie.Side != c.Won
                && c.Elapsed >= BrakeSeconds;
        }

        /// <summary>Starts the celebration at the horn if the game was decided and both clubs have players on the ice.</summary>
        internal static bool Begin(World world, FlowState s, SideOrNone winner)
        {
            if (
                !world.Switches.Celebrations
                || !winner.TryGetSide(out Side win)
                || s.Celebration.HasValue
            )
            {
                return false;
            }

            Pour(world, Side.Home);
            Pour(world, Side.Away);
            List<Player> skaters = [.. world.Players.Where(p => p.OnIce && !p.IsGoalie)];
            if (!skaters.Exists(p => p.Side == win) || !skaters.Exists(p => p.Side != win))
            {
                return false;
            }

            CelebrationState c = CreateState(world, win);
            AssignWinnerParts(c, world, win);
            AssignLoserParts(c, world, win);
            ResetCommands(world, win);
            s.Celebration = c;
            s.Dot = RinkSpec.CentreDot; // the referee heads to centre ice and stays out of the way
            return true;
        }

        private static CelebrationState CreateState(World world, Side win)
        {
            CelebrationState c = new(win, MatchTuning.CelebrationSeconds);
            MatchState m = world.Match;
            int end = FaceoffFormation.DefendedEnd(m, win);
            // the pile sits inside the winners' own blue line, so the camera holds the pile, the losers and their bench in one view
            c.Pile = new Vector3(end * PileX, 0, 0.4f);
            c.Anchor = new Vector3(end * PileX * 0.45f, 0, 0);
            return c;
        }

        private static void AssignWinnerParts(CelebrationState c, World world, Side win)
        {
            // the winners ring the pile, their goalie on the net side of it
            List<Player> winners =
            [
                .. world
                    .OnIce(win)
                    .Where(p => !p.IsGoalie)
                    .OrderBy(p => Vector3.Distance(p.Position, c.Pile)),
            ];
            int slots = winners.Count + 1;
            for (int i = 0; i < winners.Count; i++)
            {
                float a =
                    SimMath.Atan2(0, FaceoffFormation.DefendedEnd(world.Match, win))
                    + ((i + 1) * MathF.PI * 2 / slots);
                c.Parts[winners[i].Id] = new Part(
                    Mood.Winner,
                    c.Pile + (new Vector3(SimMath.Cos(a), 0, SimMath.Sin(a)) * PileRadius)
                );
            }
            foreach (Player g in world.OnIce(win).Where(p => p.IsGoalie))
            {
                c.Parts[g.Id] = new Part(
                    Mood.Goalie,
                    c.Pile
                        + new Vector3(
                            FaceoffFormation.DefendedEnd(world.Match, win) * PileRadius,
                            0,
                            0
                        )
                );
            }
        }

        private static void AssignLoserParts(CelebrationState c, World world, Side win)
        {
            // the losers nearest the pile drop where they stand, the rest trudge to the bench; their goalie slumps in the crease
            List<Player> losers =
            [
                .. world
                    .Players.Where(p => p.OnIce && !p.IsGoalie && p.Side != win)
                    .OrderBy(p => Vector3.Distance(p.Position, c.Pile)),
            ];
            int end = FaceoffFormation.DefendedEnd(world.Match, win);
            for (int i = 0; i < losers.Count; i++)
            {
                Player p = losers[i];
                if (i < 2)
                {
                    // a few metres from the pile, between it and centre ice: down on the knees, then flat on the ice
                    c.Parts[p.Id] = new Part(
                        Mood.Drop,
                        new Vector3(c.Anchor.X - (end * 1.5f), 0, i == 0 ? 3.2f : -2.6f)
                    );
                }
                else
                {
                    Vector2 door = Squad.Door(p.Side);
                    float spread = (i - (losers.Count / 2f)) * 1.1f;
                    c.Parts[p.Id] = new Part(
                        Mood.SkateOff,
                        new Vector3(door.X + spread, 0, door.Y + 1.2f)
                    );
                }
            }
            foreach (Player g in world.Players.Where(p => p.OnIce && p.IsGoalie && p.Side != win))
            {
                c.Parts[g.Id] = new Part(Mood.Goalie, g.Position);
            }
        }

        private static void ResetCommands(World world, Side win)
        {
            foreach (Player p in world.Players)
            {
                p.Command = default;
                if (p.OnIce && !p.IsGoalie)
                {
                    p.Begin(p.Side == win ? PlayerAction.Celebrate : PlayerAction.None);
                }
            }
        }

        /// <summary>After a shootout the skaters are on the bench (the last shooter aside): the starters pour back over the boards at their door.</summary>
        private static void Pour(World world, Side side)
        {
            if (world.Match.Phase != MatchPhase.Shootout)
            {
                return;
            }

            Vector2 door = Squad.Door(side);
            int on = world.OnIce(side).Count(p => !p.IsGoalie);
            List<Player> bench =
            [
                .. world
                    .Players.Where(p => p.Side == side && !p.IsGoalie && !p.OnIce)
                    .Take(Math.Max(0, 5 - on)),
            ];
            for (int i = 0; i < bench.Count; i++)
            {
                Player p = bench[i];
                p.OnIce = true;
                p.Slot = -1;
                p.Position = new Vector3(door.X + ((i - 2) * 1.1f), 0, door.Y + 0.6f);
                p.Velocity = Vector3.Zero;
                p.Heading = 0;
            }
        }

        /// <summary>One step of the celebration, after the movement systems ran: poses every player and ends in <see cref="MatchPhase.Final"/>.</summary>
        /// <returns>True while a celebration owns the players, so the match flow does nothing else.</returns>
        internal static bool Update(World world, FlowState s, float dt)
        {
            if (!s.Celebration.TryGet(out CelebrationState c))
            {
                return false;
            }

            MatchState m = world.Match;
            // The goal replay (a sudden-death winner) plays out first; the live state comes back when it ends
            if (world.ReplayPlaying)
            {
                return true;
            }

            Puck pk = world.Puck;
            pk.Live = false;
            pk.Carrier = -1;
            pk.Position = c.Anchor;
            pk.Velocity = Vector3.Zero;

            c.Elapsed += dt;
            foreach (Player p in world.Players)
            {
                if (!p.OnIce || !c.Parts.TryGetValue(p.Id, out Part part))
                {
                    continue;
                }

                p.Command = default;
                if (part.Held.TryGet(out Vector3 held))
                {
                    p.Position = held;
                }

                Step(c, p, part, dt);
                part.Held = p.Position;
            }

            if (!c.Done && c.Elapsed >= c.Seconds)
            {
                c.Done = true;
                m.Phase = MatchPhase.Final;
            }
            return true;
        }

        private static void Step(CelebrationState c, Player p, Part part, float dt)
        {
            switch (part.Mood)
            {
                case Mood.Winner:
                    if (Move(p, part.Target, SkateSpeed, dt))
                    {
                        Face(p, c.Pile);
                        part.StartClip(c);
                    }
                    else if (Vector3.Distance(p.Position, c.Pile) < 5f)
                    {
                        part.StartClip(c); // the last metres are a glide with the stick up
                    }
                    break;
                case Mood.Goalie:
                    if (p.Side == c.Won)
                    {
                        if (Move(p, part.Target, GoalieSpeed, dt))
                        {
                            Face(p, c.Pile);
                        }
                    }
                    else
                    {
                        Brake(p, dt);
                    }
                    break;
                case Mood.Drop:
                    if (float.IsNaN(part.ClipSince) && !Move(p, part.Target, DropSpeed, dt))
                    {
                        break;
                    }

                    Brake(p, dt);
                    p.Heading = SimMath.Atan2(c.Pile.X - p.Position.X, 0); // the back to the winners, slumped toward the far end
                    p.Heading += MathF.PI;
                    part.StartClip(c);
                    break;
                case Mood.SkateOff:
                    if (Move(p, part.Target, TrudgeSpeed, dt))
                    {
                        p.Heading = MathF.PI; // back to the ice, face in the bench
                        part.StartClip(c);
                    }
                    break;
                default:
                    break;
            }
        }

        private const float GoalieSpeed = 5f;

        /// <summary>Skates a player one step toward a spot, slowing on arrival.</summary>
        private static bool Move(Player p, Vector3 target, float speed, float dt)
        {
            Vector3 to = target - p.Position;
            to.Y = 0;
            float d = to.Length();
            if (d < 0.08f)
            {
                p.Position = target;
                p.Velocity = Vector3.Zero;
                return true;
            }
            Vector3 dir = to / d;
            float v = Math.Min(speed, 0.6f + (d * 2.5f));
            float step = Math.Min(v * dt, d);
            p.Position += dir * step;
            p.Velocity = dir * v;
            p.Heading = SimMath.Atan2(dir.X, dir.Z);
            return false;
        }

        private static void Brake(Player p, float dt)
        {
            Vector3 v = p.Velocity;
            v.Y = 0;
            float s = v.Length();
            if (s < 0.05f)
            {
                p.Velocity = Vector3.Zero;
                return;
            }
            float ns = Math.Max(0, s - (8f * dt));
            p.Position += v / s * (s + ns) / 2 * dt;
            p.Velocity = v / s * ns;
        }

        private static void Face(Player p, Vector3 at)
        {
            p.Heading = SimMath.Atan2(at.X - p.Position.X, at.Z - p.Position.Z);
        }

        internal enum Mood
        {
            Winner,
            Goalie,
            Drop,
            SkateOff,
        }

        internal sealed class Part(Mood mood, Vector3 target)
        {
            public Mood Mood { get; } = mood;

            public Vector3 Target { get; } = target;

            /// <summary>Gets or sets where the celebration left the player last step; the movement and goalie systems run first and would drag him away.</summary>
            public Maybe<Vector3> Held { get; set; }

            /// <summary>Gets the celebration time the pose clip started, NaN until it has.</summary>
            public float ClipSince { get; private set; } = float.NaN;

            public void StartClip(CelebrationState c)
            {
                if (float.IsNaN(ClipSince))
                {
                    ClipSince = c.Elapsed;
                }
            }
        }

        internal sealed class CelebrationState(Side winner, float seconds)
        {
            public Side Won { get; } = winner;

            public float Seconds { get; } = seconds;

            public float Elapsed { get; set; }

            public bool Done { get; set; }

            public Vector3 Pile { get; set; }

            public Vector3 Anchor { get; set; }

            internal Dictionary<int, Part> Parts { get; } = [];
        }
    }
}
