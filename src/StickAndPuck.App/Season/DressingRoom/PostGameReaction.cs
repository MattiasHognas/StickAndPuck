using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Schedule;
using StickAndPuck.App.Season.Stats;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Season.DressingRoom
{
    /// <summary>The coach's post-game reaction (W127): one line, over the placeholder from
    /// <see cref="LastResultCoach"/>, about the sharpest thing in the human's last game, with the player's
    /// name filled in. The sheet is the one the human played, or a simmed game's paper sheet (no leads,
    /// fights or penalties there). Each situation has <see cref="Variants"/> lines, picked from the season
    /// seed and the game, so the same game always gets the same line. <see cref="RuleSet.CoachReaction"/>
    /// turns it off; <see cref="RuleSet.CoachTemper"/> sets how big a moment it takes.</summary>
    internal sealed class PostGameReaction : IDressingRoomSource
    {
        /// <summary>The priority of a reaction, over the placeholder's 0.</summary>
        public const int Priority = 5;

        /// <summary>The lines per situation.</summary>
        public const int Variants = 3;

        /// <summary>The goals that make a hat trick.</summary>
        public const int HatTrickGoals = 3;

        /// <summary>Every situation's check, in <see cref="ReactionKind"/> order.</summary>
        private static readonly Func<Facts, Maybe<Reaction>>[] Checks =
        [
            HatTrick,
            Comeback,
            BlownLead,
            Shutout,
            Decider,
            HeavyLoss,
            HotGoalie,
            LostFight,
            PenaltyTrouble,
            WonFight,
        ];

        private static readonly Dictionary<ReactionKind, string[]> Lines = new()
        {
            [ReactionKind.HatTrick] =
            [
                K.CoachReactHatTrick1,
                K.CoachReactHatTrick2,
                K.CoachReactHatTrick3,
            ],
            [ReactionKind.Comeback] =
            [
                K.CoachReactComeback1,
                K.CoachReactComeback2,
                K.CoachReactComeback3,
            ],
            [ReactionKind.BlownLead] =
            [
                K.CoachReactBlownLead1,
                K.CoachReactBlownLead2,
                K.CoachReactBlownLead3,
            ],
            [ReactionKind.Shutout] =
            [
                K.CoachReactShutout1,
                K.CoachReactShutout2,
                K.CoachReactShutout3,
            ],
            [ReactionKind.Decider] =
            [
                K.CoachReactDecider1,
                K.CoachReactDecider2,
                K.CoachReactDecider3,
            ],
            [ReactionKind.HeavyLoss] =
            [
                K.CoachReactHeavyLoss1,
                K.CoachReactHeavyLoss2,
                K.CoachReactHeavyLoss3,
            ],
            [ReactionKind.HotGoalie] =
            [
                K.CoachReactHotGoalie1,
                K.CoachReactHotGoalie2,
                K.CoachReactHotGoalie3,
            ],
            [ReactionKind.LostFight] =
            [
                K.CoachReactLostFight1,
                K.CoachReactLostFight2,
                K.CoachReactLostFight3,
            ],
            [ReactionKind.PenaltyTrouble] =
            [
                K.CoachReactPenaltyTrouble1,
                K.CoachReactPenaltyTrouble2,
                K.CoachReactPenaltyTrouble3,
            ],
            [ReactionKind.WonFight] =
            [
                K.CoachReactWonFight1,
                K.CoachReactWonFight2,
                K.CoachReactWonFight3,
            ],
        };

        /// <inheritdoc/>
        public void Fill(DressingRoomContext context, DressingRoomReport report)
        {
            if (
                !context.Rules.CoachReaction
                || !context.Last.TryGet(out LastGame game)
                || !context.Sheet().TryGet(out MatchStatSheet sheet)
                || !Pick(game, sheet, context.Rules.CoachTemper).TryGet(out Reaction reaction)
            )
            {
                return;
            }

            string key = Key(reaction.Kind, Variant(context.Season.Seed, game.Key, reaction.Kind));
            report.Say(
                new DressingRoomEntry(
                    Text.Format(key, reaction.Player, reaction.Count),
                    Priority,
                    Highlight: reaction.Mood == CoachMood.Pleased,
                    reaction.Mood
                )
            );
        }

        /// <summary>Gets the string key of one line of a situation.</summary>
        public static string Key(ReactionKind kind, int variant)
        {
            string[] lines = Lines[kind];
            return lines[Math.Clamp(variant, 0, lines.Length - 1)];
        }

        /// <summary>Gets which line of a situation a game gets, 0 to <see cref="Variants"/> - 1: fixed by the
        /// season seed and the game's key.</summary>
        public static int Variant(int seasonSeed, string gameKey, ReactionKind kind)
        {
            return Seeds.For(seasonSeed == 0 ? 1 : seasonSeed, "coach:" + gameKey + ":" + kind, 0)
                % Variants;
        }

        /// <summary>Gets the lead it takes to blow one (and the deficit to come back from) at a temper.</summary>
        public static int LeadMargin(CoachTemper temper)
        {
            return temper == CoachTemper.Mild ? 3 : 2;
        }

        /// <summary>Gets the losing margin that makes a heavy loss at a temper.</summary>
        public static int HeavyLossMargin(CoachTemper temper)
        {
            return temper switch
            {
                CoachTemper.Mild => 5,
                CoachTemper.Strong => 3,
                CoachTemper.Normal => 4,
                _ => throw new NotSupportedException("no heavy loss margin for " + temper),
            };
        }

        /// <summary>Gets the saves that make a hot goalie at a temper.</summary>
        public static int HotGoalieSaves(CoachTemper temper)
        {
            return temper switch
            {
                CoachTemper.Mild => 40,
                CoachTemper.Strong => 30,
                CoachTemper.Normal => 35,
                _ => throw new NotSupportedException("no hot goalie saves for " + temper),
            };
        }

        /// <summary>Gets the club's penalties that make penalty trouble at a temper.</summary>
        public static int TroublePenalties(CoachTemper temper)
        {
            return temper switch
            {
                CoachTemper.Mild => 6,
                CoachTemper.Strong => 4,
                CoachTemper.Normal => 5,
                _ => throw new NotSupportedException("no penalty trouble for " + temper),
            };
        }

        /// <summary>Picks the coach's reaction to a game from its sheet: the first situation that holds, in
        /// <see cref="ReactionKind"/> order; None for an ordinary game (the placeholder line stays).</summary>
        public static Maybe<Reaction> Pick(LastGame game, MatchStatSheet sheet, CoachTemper temper)
        {
            Side own = game.AtHome ? Side.Home : Side.Away;
            List<MatchStatLine> ours = [.. sheet.Lines.Where(l => l.Side == own)];
            Facts f = new(
                game,
                sheet,
                temper,
                own,
                ours,
                [.. ours.Where(l => l.Played && !l.IsGoalie)],
                ours.Where(l => l.IsGoalie && l.Played)
                    .OrderByDescending(l => l.Saves)
                    .FirstOrNone(_ => true)
            );
            foreach (Func<Facts, Maybe<Reaction>> check in Checks)
            {
                if (check(f).TryGet(out Reaction reaction))
                {
                    return reaction;
                }
            }

            return Maybe.None<Reaction>();
        }

        private static Maybe<Reaction> HatTrick(Facts f)
        {
            return f
                .Ours.Where(l => l.Goals >= HatTrickGoals)
                .OrderByDescending(l => l.Goals)
                .FirstOrNone(_ => true)
                .Select(l => new Reaction(ReactionKind.HatTrick, l.Name, l.Goals, f.ResultMood));
        }

        private static Maybe<Reaction> Comeback(Facts f)
        {
            int trailed = f.Sheet.MaxLead(f.Other);
            return f.Game.Won && trailed >= LeadMargin(f.Temper)
                ? Best(f.Skaters)
                    .Select(l => new Reaction(
                        ReactionKind.Comeback,
                        l.Name,
                        trailed,
                        CoachMood.Pleased
                    ))
                : Maybe.None<Reaction>();
        }

        private static Maybe<Reaction> BlownLead(Facts f)
        {
            int led = f.Sheet.MaxLead(f.Own);
            return !f.Game.Won && led >= LeadMargin(f.Temper)
                ? Worst(f.Skaters)
                    .Select(l => new Reaction(ReactionKind.BlownLead, l.Name, led, CoachMood.Angry))
                : Maybe.None<Reaction>();
        }

        private static Maybe<Reaction> Shutout(Facts f)
        {
            return f.Game.Won && f.Game.GoalsAgainst == 0
                ? f.Goalie.Select(l => new Reaction(
                    ReactionKind.Shutout,
                    l.Name,
                    l.Saves,
                    CoachMood.Pleased
                ))
                : Maybe.None<Reaction>();
        }

        private static Maybe<Reaction> Decider(Facts f)
        {
            return f.Game.Won && f.Game.Result.Kind != ResultKind.Regulation
                ? f
                    .Ours.FirstOrNone(l => l.Decider)
                    .Select(l => new Reaction(
                        ReactionKind.Decider,
                        l.Name,
                        l.Goals,
                        CoachMood.Pleased
                    ))
                : Maybe.None<Reaction>();
        }

        private static Maybe<Reaction> HeavyLoss(Facts f)
        {
            return f.Game.GoalsAgainst - f.Game.GoalsFor >= HeavyLossMargin(f.Temper)
                ? Worst(f.Skaters)
                    .Select(l => new Reaction(
                        ReactionKind.HeavyLoss,
                        l.Name,
                        f.Game.GoalsAgainst,
                        CoachMood.Angry
                    ))
                : Maybe.None<Reaction>();
        }

        private static Maybe<Reaction> HotGoalie(Facts f)
        {
            return f.Goalie.TryGet(out MatchStatLine hot) && hot.Saves >= HotGoalieSaves(f.Temper)
                ? new Reaction(ReactionKind.HotGoalie, hot.Name, hot.Saves, f.ResultMood)
                : Maybe.None<Reaction>();
        }

        private static Maybe<Reaction> LostFight(Facts f)
        {
            return f
                .Ours.FirstOrNone(l => l.FightsLost > 0)
                .Select(l => new Reaction(
                    ReactionKind.LostFight,
                    l.Name,
                    l.FightsLost,
                    f.Game.Won ? CoachMood.Neutral : CoachMood.Angry
                ));
        }

        private static Maybe<Reaction> PenaltyTrouble(Facts f)
        {
            int penalties = f.Ours.Sum(l => l.Penalties);
            return penalties >= TroublePenalties(f.Temper)
                ? f
                    .Ours.OrderByDescending(l => l.Penalties)
                    .FirstOrNone(_ => true)
                    .Select(l => new Reaction(
                        ReactionKind.PenaltyTrouble,
                        l.Name,
                        penalties,
                        CoachMood.Angry
                    ))
                : Maybe.None<Reaction>();
        }

        private static Maybe<Reaction> WonFight(Facts f)
        {
            return f
                .Ours.FirstOrNone(l => l.FightsWon > 0)
                .Select(l => new Reaction(
                    ReactionKind.WonFight,
                    l.Name,
                    l.FightsWon,
                    CoachMood.Pleased
                ));
        }

        /// <summary>The skater with the most points (goals first), roster order on ties.</summary>
        private static Maybe<MatchStatLine> Best(List<MatchStatLine> skaters)
        {
            return skaters
                .OrderByDescending(l => l.Points)
                .ThenByDescending(l => l.Goals)
                .FirstOrNone(_ => true);
        }

        /// <summary>The skater with the worst plus/minus, roster order on ties.</summary>
        private static Maybe<MatchStatLine> Worst(List<MatchStatLine> skaters)
        {
            return skaters.OrderBy(l => l.PlusMinus).FirstOrNone(_ => true);
        }

        /// <summary>What the checks read: the game, its sheet, the temper and the human's club's lines.</summary>
        private sealed record Facts(
            LastGame Game,
            MatchStatSheet Sheet,
            CoachTemper Temper,
            Side Own,
            List<MatchStatLine> Ours,
            List<MatchStatLine> Skaters,
            Maybe<MatchStatLine> Goalie
        )
        {
            public Side Other => Own == Side.Home ? Side.Away : Side.Home;

            /// <summary>Gets the face for praise that also fits a loss: pleased after a win, else neutral.</summary>
            public CoachMood ResultMood => Game.Won ? CoachMood.Pleased : CoachMood.Neutral;
        }
    }
}
