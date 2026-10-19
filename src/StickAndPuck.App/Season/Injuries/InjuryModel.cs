using StickAndPuck.App.Core;
using StickAndPuck.App.Players.Checks;
using StickAndPuck.App.Season.Stats;

namespace StickAndPuck.App.Season.Injuries
{
    /// <summary>How likely each event is to injure a player, and for how long. Every chance is at
    /// <see cref="InjuryRate.Normal"/> and is multiplied by <see cref="RuleSet.InjuryScale"/>.</summary>
    public static class InjuryModel
    {
        /// <summary>The fewest games an injury keeps a player out.</summary>
        public const int MinGames = 1;

        /// <summary>The most games an injury keeps a player out.</summary>
        public const int MaxGames = 6;

        /// <summary>The chance a hard check (<see cref="CheckingModel.HardHit"/>) injures the player hit; harder hits more, up to twice this.</summary>
        public const float CheckChance = 0.05f;

        /// <summary>The chance a blocked slap shot at <see cref="BlockSpeed"/> injures the blocker; a harder shot more, up to half again this.</summary>
        public const float BlockChance = 0.08f;

        /// <summary>The puck speed, in metres per second, that a blocked slap shot is rated at.</summary>
        public const float BlockSpeed = 30f;

        /// <summary>The chance a landed punch of strength 1 injures the fighter it hits.</summary>
        public const float PunchChance = 0.03f;

        /// <summary>The chance one club comes out of a game settled on paper with a player injured.</summary>
        public const float PaperChance = 0.06f;

        /// <summary>How often each length is drawn, 1 game first: most injuries are short.</summary>
        private static readonly int[] GameWeights = [30, 24, 18, 12, 9, 7];

        /// <summary>Gets the chance a body check of some strength injures the player hit: 0 below a hard hit.</summary>
        public static float Check(float strength, float scale)
        {
            return strength < CheckingModel.HardHit
                ? 0f
                : CheckChance * Math.Min(2f, strength / CheckingModel.HardHit) * scale;
        }

        /// <summary>Gets the chance a blocked shot injures the blocker: 0 for anything but a slap shot.</summary>
        public static float Block(bool slap, float speed, float scale)
        {
            return !slap ? 0f : BlockChance * Math.Clamp(speed / BlockSpeed, 0.5f, 1.5f) * scale;
        }

        /// <summary>Gets the chance a landed punch injures the fighter it hits.</summary>
        public static float Punch(float strength, float scale)
        {
            return PunchChance * Math.Max(0f, strength) * scale;
        }

        /// <summary>Gets the chance one club's game settled on paper injures one of its players.</summary>
        public static float Paper(float scale)
        {
            return PaperChance * scale;
        }

        /// <summary>Rolls a chance: always draws one number, so the stream stays in step whatever the chance.</summary>
        public static bool Roll(Random dice, float chance)
        {
            return dice.NextSingle() < chance;
        }

        /// <summary>Injuries for a game settled on paper: each club, with <see cref="Paper"/>'s chance, loses one of
        /// the skaters who played to a check, a blocked shot or a fight. Drawn from dice of their own,
        /// so the sheet's lines stay what the seed gave before injuries were kept.</summary>
        public static void DrawPaper(MatchStatSheet sheet, float scale, int seed)
        {
            if (scale <= 0)
            {
                return;
            }

            Random dice = Seeds.Dice(unchecked(seed ^ 0x1E7A));
            foreach (Side side in new[] { Side.Home, Side.Away })
            {
                if (!Roll(dice, Paper(scale)))
                {
                    continue;
                }

                List<MatchStatLine> skaters =
                [
                    .. sheet.Lines.Where(l => l.Side == side && l.Played && !l.IsGoalie),
                ];
                if (skaters.Count == 0)
                {
                    continue;
                }

                MatchStatLine hurt = skaters[dice.Next(skaters.Count)];
                int games = Games(dice);
                sheet.Absences.Add(
                    new Absence
                    {
                        Club = hurt.Club,
                        Number = hurt.Number,
                        Reason = AbsenceReason.Injury,
                        Cause = PaperCauses[dice.Next(PaperCauses.Length)],
                        Games = games,
                        GamesLeft = games,
                    }
                );
            }
        }

        /// <summary>What hurts a player in a game settled on paper, checks most often.</summary>
        private static readonly InjuryCause[] PaperCauses =
        [
            InjuryCause.Check,
            InjuryCause.Check,
            InjuryCause.BlockedShot,
            InjuryCause.Fight,
        ];

        /// <summary>Draws how many games an injury lasts, <see cref="MinGames"/> to <see cref="MaxGames"/>, short ones most often.</summary>
        public static int Games(Random dice)
        {
            int roll = dice.Next(GameWeights.Sum());
            for (int i = 0; i < GameWeights.Length; i++)
            {
                roll -= GameWeights[i];
                if (roll < 0)
                {
                    return MinGames + i;
                }
            }

            return MaxGames;
        }
    }
}
