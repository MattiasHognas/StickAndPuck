using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using StickAndPuck.App.AI.Difficulty;
using StickAndPuck.App.Core;
using StickAndPuck.App.Goalie;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players.Puck;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Goalie
{
    /// <summary>
    /// Shots the way a player takes them, against a club-average goalie (reflexes 66): the stick
    /// pointed at the middle of the net, a tapped or briefly held wrist shot from the slot or the
    /// circles, or a wound-up slap shot from the circles, through the same shot code a pad uses.
    /// About one in ten goes in, and the goalie holds well under half of what it stops.
    /// </summary>
    public sealed class HumanShotTests : CheckedTest
    {
        private const int Rounds = 300;

        /// <summary>What a batch of shots came to.</summary>
        [StructLayout(LayoutKind.Auto)]
        private readonly record struct Tally(int Saves, int Caught, int Goals, int Wide)
        {
            public float Shooting => (float)Goals / Math.Max(1, Saves + Goals);

            public float CatchShare => (float)Caught / Math.Max(1, Saves);

            public override string ToString()
            {
                return $"{Goals} goals, {Saves} saves ({Caught} held), {Wide} wide: {Shooting:P1} go in, {CatchShare:P0} of saves held";
            }
        }

        /// <summary>A club-average goalie on normal.</summary>
        private static readonly Traits Average = new()
        {
            GoalieReflexes = 66,
            HockeyIq = 64,
            GoalieGlove = 64,
            GoalieBlocker = 64,
            GoalieReboundControl = 64,
            GoaliePuckHandling = 0,
        };

        [Test]
        public void SlotAndCircles()
        {
            Tally tapSlot = Batch(Average, SlotSpot, ShotKind.Wrist, 0.05f, AtNet);
            Tally tapCircle = Batch(Average, CircleSpot, ShotKind.Wrist, 0.05f, AtNet);
            Tally heldSlot = Batch(Average, SlotSpot, ShotKind.Wrist, 0.2f, AtNet);
            Tally slap = Batch(Average, CircleSpot, ShotKind.Slap, 0.8f, AtNet);
            Console.WriteLine($"      tapped wrist shots from the slot: {tapSlot}");
            Console.WriteLine($"      tapped wrist shots from the circles: {tapCircle}");
            Console.WriteLine($"      held wrist shots from the slot: {heldSlot}");
            Console.WriteLine($"      slap shots from the circles: {slap}");
            Tally[] all = [tapSlot, tapCircle, heldSlot, slap];
            float shooting =
                (float)all.Sum(t => t.Goals) / Math.Max(1, all.Sum(t => t.Saves + t.Goals));
            float held = (float)all.Sum(t => t.Caught) / Math.Max(1, all.Sum(t => t.Saves));
            Check(
                tapSlot.Shooting is > 0.07f and < 0.22f && heldSlot.Shooting is > 0.07f and < 0.22f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"a slot shot goes in about one time in ten ({tapSlot.Shooting:P1} tapped, {heldSlot.Shooting:P1} held)"
                )
            );
            Check(
                shooting is > 0.06f and < 0.14f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"about one shot in ten goes in overall ({shooting:P1})"
                )
            );
            Check(
                held < 0.45f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the goalie holds under half of what it stops ({held:P0})"
                )
            );
        }

        /// <summary>
        /// Goalie stats matter: the same shots against a weak club goalie (reflexes 55) and a top one (80);
        /// the top one saves clearly more (saverate measures the same gap over full matches).
        /// </summary>
        [Test]
        public void BetterGoalieSavesMore()
        {
            static float Saved(int reflexes)
            {
                Traits traits = Average with { GoalieReflexes = reflexes };
                Tally[] shots =
                [
                    Batch(traits, SlotSpot, ShotKind.Wrist, 0.05f, AtNet),
                    Batch(traits, CircleSpot, ShotKind.Slap, 0.8f, AtNet),
                ];
                Console.WriteLine(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"      reflexes {reflexes}: {shots[0]}; {shots[1]}"
                    )
                );
                return (float)shots.Sum(t => t.Saves)
                    / Math.Max(1, shots.Sum(t => t.Saves + t.Goals));
            }

            float weak = Saved(55),
                top = Saved(80);
            Check(
                top > weak + 0.03f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the best goalies save clearly more than the weakest ({top:P1} against {weak:P1})"
                )
            );
        }

        /// <summary>
        /// The shots of a played match: the stick pushed up the ice toward the end boards, a wound-up slap
        /// shot or a quick wrist shot from the slot or the circles, against the club-average goalie as each
        /// difficulty plays him. Each difficulty scales the AI goalie, normal included, so the gaps are wide.
        /// </summary>
        [Test]
        public void PlayedShots()
        {
            Dictionary<Difficulty, float> pct = [];
            foreach (
                Difficulty level in new[] { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard }
            )
            {
                Traits traits = DifficultyScale.Goalie(Average, level);
                Tally[] shots =
                [
                    Batch(traits, SlotSpot, ShotKind.Slap, 0.8f, UpIce),
                    Batch(traits, SlotSpot, ShotKind.Wrist, 0.05f, UpIce),
                    Batch(traits, SlotSpot, ShotKind.Wrist, 0.25f, UpIce),
                    Batch(traits, CircleSpot, ShotKind.Slap, 0.8f, UpIce),
                ];
                pct[level] =
                    (float)shots.Sum(t => t.Goals) / Math.Max(1, shots.Sum(t => t.Goals + t.Saves));
                Console.WriteLine(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"      {level} (reflexes {traits.GoalieReflexes}, positioning {traits.HockeyIq}): slot slap {shots[0]}; slot tap {shots[1]}; slot held wrist {shots[2]}; circle slap {shots[3]}; {pct[level]:P1} go in"
                    )
                );
            }
            // played matches score about two thirds of these rates (traffic, rushed and tired shots): on the ice
            // that is a third or more on easy, about one in six on normal and one in nine on hard
            Check(
                pct[Difficulty.Easy] is > 0.42f and < 0.58f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"on easy about half the played shots go in ({pct[Difficulty.Easy]:P1})"
                )
            );
            Check(
                pct[Difficulty.Normal] is > 0.2f and < 0.3f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"on normal about one in four ({pct[Difficulty.Normal]:P1})"
                )
            );
            Check(
                pct[Difficulty.Hard] is > 0.13f and < 0.2f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"on hard about one in six ({pct[Difficulty.Hard]:P1})"
                )
            );
        }

        private static Vector2 AtNet(Vector3 from, Random dice)
        {
            return Vector2.Normalize(new Vector2(RinkSpec.GoalLineX - from.X, -from.Z));
        }

        private static Vector2 UpIce(Vector3 from, Random dice)
        {
            return Vector2.Normalize(new Vector2(1f, ((dice.NextSingle() * 2) - 1) * 0.3f));
        }

        private static Vector3 SlotSpot(Random dice)
        {
            return new(
                RinkSpec.GoalLineX - 6 - (dice.NextSingle() * 4),
                0,
                ((dice.NextSingle() * 2) - 1) * 2
            );
        }

        private static Vector3 CircleSpot(Random dice)
        {
            return new(
                RinkSpec.GoalLineX - 8 - (dice.NextSingle() * 4),
                0,
                (dice.NextSingle() < 0.5f ? -1 : 1) * (5 + (dice.NextSingle() * 2))
            );
        }

        private static Tally Batch(
            Traits traits,
            Func<Random, Vector3> spot,
            ShotKind kind,
            float held,
            Func<Vector3, Random, Vector2> stick
        )
        {
            World world = new WorldBuilder().NoStarters().NotBegun().Build();
            PlayerData data = new()
            {
                Position = PlayingPosition.G,
                Traits = traits with { GoaliePuckHandling = 0 },
            };
            Player g = world.AddPlayer(Side.Away, data, new Vector3(RinkSpec.GoalLineX - 1f, 0, 0)); // away defends +x in period 1
            g.Heading = -MathF.PI / 2;
            Player shooter = world.AddPlayer(
                Side.Home,
                new PlayerData
                {
                    Position = PlayingPosition.C,
                    Traits = new Traits { ShotAccuracy = 60, ShotPower = 60 },
                },
                new Vector3(0, 0, 0)
            );
            using GoalieSystem goalie = new() { Dice = Seeds.Dice(3) };
            using PuckPhysics physics = new();
            Random dice = Seeds.Dice(7);
            int saves = 0,
                caught = 0,
                goals = 0,
                wide = 0;
            for (int i = 0; i < Rounds; i++)
            {
                world.Puck.Carrier = -1;
                world.Puck.Live = false;
                Vector3 start = spot(dice) with { Y = 0.02f };
                world.Puck.Position = start;
                g.Position = GoalieModel.Spot(1, start, data.Traits.HockeyIq);
                Run(world, goalie, physics, 0.6f);
                shooter.Position = start with { X = start.X - 0.6f, Y = 0 };
                shooter.Command = new PlayerCommand { Move = stick(start, dice) };
                float speed =
                    kind == ShotKind.Slap
                        ? PuckHandling.SlapSpeed(shooter.Data.Traits, held)
                        : PuckHandling.WristSpeed(shooter.Data.Traits);
                world.Puck.Live = true;
                world.Puck.Position = start;
                world.Puck.Velocity = PuckHandling.ShotVelocity(
                    world,
                    shooter,
                    start,
                    kind,
                    speed,
                    dice,
                    held
                );
                world.Puck.LastTouch = shooter.Id;
                int outcome = Outcome(world, goalie, physics, g);
                wide += outcome == 0 ? 1 : 0;
                goals += outcome < 0 ? 1 : 0;
                saves += outcome > 0 ? 1 : 0;
                caught += outcome == 2 ? 1 : 0;
            }
            return new Tally(saves, caught, goals, wide);
        }

        /// <summary>Runs until the shot is decided: 0 wide, -1 a goal, 1 a rebound, 2 held.</summary>
        private static int Outcome(World world, GoalieSystem goalie, PuckPhysics physics, Player g)
        {
            for (int i = 0; i < 96; i++)
            {
                world.Events.Clear();
                goalie.Update(world, Game.Step);
                physics.Update(world, Game.Step);
                world.Time += Game.Step;
                world.Tick++;
                if (world.Events.Current.Any(e => e is GoalScored))
                {
                    return -1;
                }

                if (world.Events.Current.Any(e => e is ShotSaved))
                {
                    return world.Puck.Carrier == g.Id ? 2 : 1;
                }
            }
            return 0;
        }

        private static void Run(
            World world,
            GoalieSystem goalie,
            PuckPhysics physics,
            float seconds
        )
        {
            for (int i = 0; i < (int)(seconds / Game.Step); i++)
            {
                world.Events.Clear();
                goalie.Update(world, Game.Step);
                physics.Update(world, Game.Step);
                world.Time += Game.Step;
                world.Tick++;
            }
        }
    }
}
