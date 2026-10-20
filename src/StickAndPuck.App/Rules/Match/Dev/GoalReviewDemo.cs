using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Rules.Match.Dev
{
    /// <summary>The script behind <see cref="GoalReviewScene"/>: a moment into each spell of play a skater puts the puck
    /// in from the edge of the crease. In turn a clean goal (nobody near the goalie) and one with the scorer's
    /// teammate in the crease, leaning on the goalie (goalie interference); with <see cref="Challenge"/> the visitors
    /// score with a teammate standing in the crease off the goalie, and the home human's coach answers the challenge
    /// prompt with pass a moment after it shows.</summary>
    internal sealed class GoalReviewDemo
    {
        /// <summary>Seconds into a spell of play before the play is set up.</summary>
        private const float SetUpAfter = 1.5f;

        /// <summary>Seconds the play is held before the puck goes in, so the attacker is in the crease well before it.</summary>
        private const float HoldFor = 0.3f;

        /// <summary>Seconds after the shot before a puck kept out is set up again.</summary>
        private const float GiveUpAfter = 2f;

        /// <summary>Seconds the challenge prompt shows before the script presses pass.</summary>
        private const float AnswerAfter = 1.5f;

        private float stageAt = float.NegativeInfinity;
        private int stage;
        private int plays;
        private Vector3 goaliePosition,
            screenPosition;
        private int screenId = -1;

        /// <summary>Gets a value indicating whether the visitors score with a man in the crease and the home human challenges.</summary>
        public bool Challenge { get; init; }

        /// <summary>Runs the script; called by the match rules before they judge the step.</summary>
        public static void Update(World world)
        {
            if (world.Dev.GoalReview.TryGet(out GoalReviewDemo demo))
            {
                demo.Run(world);
            }
        }

        private void Run(World world)
        {
            if (GoalReview.Asking(world).HasValue)
            {
                Answer(world);
                return;
            }

            if (world.Match.Phase != MatchPhase.Play)
            {
                if (stage != 0)
                {
                    plays++;
                }

                stage = 0;
                stageAt = world.Time;
                return;
            }

            Side scorer = Challenge ? Side.Away : Side.Home;
            Side defender = Challenge ? Side.Home : Side.Away;
            int end = FaceoffFormation.DefendedEnd(world.Match, defender);
            if (world.OnIce(defender).FirstOrDefault(p => p.IsGoalie) is not { } goalie)
            {
                return;
            }

            List<Player> attackers = [.. world.OnIce(scorer).Where(p => !p.IsGoalie)];
            if (attackers.Count < 2)
            {
                return;
            }

            bool screen = Challenge || plays % 2 == 1;
            if (stage == 0 && world.Time - stageAt >= SetUpAfter)
            {
                SetUp(world, end, goalie, attackers, screen);
                stage = 1;
                stageAt = world.Time;
            }
            else if (stage == 1)
            {
                Hold(world, goalie);
                if (world.Time - stageAt >= HoldFor)
                {
                    Shoot(world, end, attackers[0]);
                    stage = 2;
                    stageAt = world.Time;
                }
            }
            else if (stage == 2)
            {
                Hold(world, goalie);
                if (world.Time - stageAt >= GiveUpAfter)
                {
                    stage = 0; // kept out somehow: set it up again
                    stageAt = world.Time;
                }
            }
        }

        /// <summary>Clears the defenders away up the ice, sets the goalie at his far post and, for a screen, a man in the crease beside him.</summary>
        private void SetUp(World world, int end, Player goalie, List<Player> attackers, bool screen)
        {
            Side defender = goalie.Side;
            foreach (Player p in world.OnIce(defender).Where(p => !p.IsGoalie))
            {
                p.Position = new Vector3(end * 12f, 0, p.Position.Z * 0.5f); // caught up the ice
            }

            foreach (Player p in attackers)
            {
                p.Position = new Vector3(end * 14f, 0, p.Position.Z * 0.5f);
            }

            goaliePosition = new Vector3(end * (RinkSpec.GoalLineX - 0.4f), 0, -0.45f);
            goalie.Position = goaliePosition;
            screenId = -1;
            if (screen)
            {
                // leaning on the goalie (contact), or in the crease a stride off him, on his far side, when the visitors score for a challenge
                Player s = attackers[1];
                screenPosition = Challenge
                    ? new Vector3(end * (RinkSpec.GoalLineX - 0.6f), 0, -1.35f)
                    : new Vector3(end * (RinkSpec.GoalLineX - 0.5f), 0, -1.05f);
                s.Position = screenPosition;
                s.Velocity = Vector3.Zero;
                screenId = s.Id;
            }

            Puck pk = world.Puck;
            pk.Carrier = -1;
            pk.Position = new Vector3(end * (RinkSpec.GoalLineX - 6f), 0, 1.2f);
            pk.Velocity = Vector3.Zero;
        }

        /// <summary>Holds the goalie and the screen where the script put them, whatever their systems wanted this step.</summary>
        private void Hold(World world, Player goalie)
        {
            goalie.Position = goaliePosition;
            goalie.Velocity = Vector3.Zero;
            if (screenId >= 0)
            {
                Player s = world.Players[screenId];
                s.Position = screenPosition;
                s.Velocity = Vector3.Zero;
            }
        }

        /// <summary>The shooter puts the puck in at the near post, past the goalie at the far one.</summary>
        private static void Shoot(World world, int end, Player shooter)
        {
            Puck pk = world.Puck;
            Vector3 from = new(end * (RinkSpec.GoalLineX - 0.3f), 0, 0.6f);
            shooter.Position = from + new Vector3(-end * 1.5f, 0, 0.6f);
            pk.Carrier = -1;
            pk.Position = from;
            pk.Velocity = new Vector3(end * 12f, 0, 0);
            pk.LastTouch = shooter.Id;
        }

        /// <summary>The home human's coach challenges for goalie interference (pass) a moment into the prompt.</summary>
        private void Answer(World world)
        {
            if (stage != 3)
            {
                stage = 3;
                stageAt = world.Time;
            }

            if (world.Time - stageAt < AnswerAfter)
            {
                return;
            }

            foreach (Player p in world.Players.Where(p => p.Slot >= 0))
            {
                p.Command = p.Command with { Pass = true };
            }
        }
    }
}
