using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players.Puck;
using StickAndPuck.App.Rules.Match;

namespace StickAndPuck.App.AI.Decisions.Dev
{
    /// <summary>The script behind <see cref="AiMovesScene"/>, driven from <see cref="DecisionSystem"/>.</summary>
    internal sealed class MovesDrill
    {
        /// <summary>Seconds each round of the drill runs before it starts over.</summary>
        private const float Round = 4f;

        /// <summary>Seconds into a one-timer round that the pass goes.</summary>
        private const float PassAt = 0.6f;

        private readonly bool oneTimer;
        private readonly Player carrier,
            mate,
            defender;
        private float start;
        private bool passed;

        private MovesDrill(bool oneTimer, Player carrier, Player mate, Player defender)
        {
            this.oneTimer = oneTimer;
            this.carrier = carrier;
            this.mate = mate;
            this.defender = defender;
        }

        /// <summary>Adds the drill's skaters and goalies and starts the first round.</summary>
        public static void Setup(World world, bool oneTimer)
        {
            List<PlayerData> home = Skaters(world.Setup.Home);
            List<PlayerData> away = Skaters(world.Setup.Away);
            Traits star = new()
            {
                HockeyIq = 95,
                PuckControl = 95,
                ShotAccuracy = 90,
                TopSpeed = 80,
                Acceleration = 80,
            };
            Player homeCarrier = world.AddPlayer(
                Side.Home,
                home[0] with
                {
                    Traits = star,
                },
                Vector3.Zero
            );
            Player homeMate = world.AddPlayer(
                Side.Home,
                home[1 % home.Count] with
                {
                    Traits = star,
                },
                Vector3.Zero
            );
            Player awayDefender = world.AddPlayer(Side.Away, away[3 % away.Count], Vector3.Zero);
            foreach (Side side in new[] { Side.Home, Side.Away })
            {
                if (
                    world
                        .Setup.Club(side)
                        .Roster.FirstOrDefault(p => p.Position == PlayingPosition.G)
                    is not { } g
                )
                {
                    continue;
                }

                float end = side == Side.Home ? -1 : 1;
                world
                    .AddPlayer(side, g, new Vector3(end * (RinkSpec.GoalLineX - 1f), 0, 0))
                    .Heading = end < 0 ? MathF.PI / 2 : -MathF.PI / 2;
            }
            MatchFlow.Begin(world, log: false);
            MovesDrill drill = new(oneTimer, homeCarrier, homeMate, awayDefender);
            DecisionState.Board(world).Drill = drill;
            drill.Reset(world);
        }

        /// <summary>Starts a round over when it has run its time; before decisions.</summary>
        public static void Restart(World world, DecisionBoard board)
        {
            if (board.Drill.TryGet(out MovesDrill drill) && world.Time - drill.start >= Round)
            {
                drill.Reset(world);
            }
        }

        /// <summary>In a one-timer round, passes from the point to the slot once; after decisions, so it wins over the carrier's own read.</summary>
        public static void Pass(World world, DecisionBoard board)
        {
            if (board.Drill.TryGet(out MovesDrill drill))
            {
                drill.PassAcross(world, board);
            }
        }

        private void PassAcross(World world, DecisionBoard board)
        {
            if (
                !oneTimer
                || passed
                || world.Time - start < PassAt
                || world.Puck.Carrier != carrier.Id
                || carrier.Action != PlayerAction.None
            )
            {
                return;
            }

            passed = true;
            Vector2 to = Vector2.Normalize(
                new Vector2(
                    mate.Position.X - carrier.Position.X,
                    mate.Position.Z - carrier.Position.Z
                )
            );
            board.For(carrier.Id).PassTo = mate.Id;
            AiCommand.Act(
                world,
                carrier,
                new PlayerCommand { Move = to, Pass = true },
                "drill: pass"
            );
        }

        private void Reset(World world)
        {
            int end = PuckHandling.AttackEnd(world, Side.Home);
            float face = end > 0 ? MathF.PI / 2 : -MathF.PI / 2;
            if (oneTimer)
            {
                Place(carrier, new Vector3(end * 18f, 0, -8f), face, Vector3.Zero);
                Place(mate, new Vector3(end * 21f, 0, 3.5f), MathF.PI, Vector3.Zero);
                Place(defender, new Vector3(end * 4f, 0, 12f), -face, Vector3.Zero);
            }
            else
            {
                Place(carrier, new Vector3(end * -8f, 0, 0), face, new Vector3(end * 6f, 0, 0));
                Place(mate, new Vector3(end * -12f, 0, 9f), face, new Vector3(end * 4f, 0, 0));
                Place(
                    defender,
                    new Vector3(end * 6f, 0, 0.3f),
                    -face,
                    new Vector3(end * -3f, 0, 0)
                );
            }
            foreach (Player g in world.Players.Where(p => p.IsGoalie))
            {
                float side = PuckHandling.AttackEnd(world, g.Side);
                Place(
                    g,
                    new Vector3(-side * (RinkSpec.GoalLineX - 1f), 0, 0),
                    side > 0 ? MathF.PI / 2 : -MathF.PI / 2,
                    Vector3.Zero
                );
            }
            Puck pk = world.Puck;
            pk.Carrier = carrier.Id;
            pk.LastTouch = carrier.Id;
            pk.Position = PuckHandling.Blade(carrier) with { Y = 0 };
            pk.Velocity = Vector3.Zero;
            pk.Live = true;
            world.Match.Phase = MatchPhase.Play;
            Brain brain = DecisionState.Board(world).For(carrier.Id);
            brain.GotAt = world.Time;
            brain.DekeRolled = float.NegativeInfinity;
            start = world.Time;
            passed = false;
        }

        private static void Place(Player p, Vector3 at, float heading, Vector3 velocity)
        {
            p.Position = at;
            p.Velocity = velocity;
            p.Heading = heading;
            p.Command = default;
            p.Begin(PlayerAction.None);
        }

        private static List<PlayerData> Skaters(ClubData club)
        {
            List<PlayerData> list = [.. club.Roster.Where(p => p.Position != PlayingPosition.G)];
            return list.Count > 0 ? list : [new PlayerData { Position = PlayingPosition.C }];
        }
    }
}
