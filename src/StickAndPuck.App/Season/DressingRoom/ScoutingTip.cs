using System.Globalization;
using StickAndPuck.App.AI.Tactics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Screens;
using StickAndPuck.App.Season.Playoffs;
using StickAndPuck.App.Season.Schedule;
using StickAndPuck.App.Season.Stats;

namespace StickAndPuck.App.Season.DressingRoom
{
    /// <summary>The coach's scouting tip in the pre-game talk (W141), at the opening's priority so it follows
    /// it: one thing about tonight's opponent, read from its real data so it is always true: its forecheck,
    /// zone entries, playstyle (<see cref="ClubStrengths.StyleName"/>), power play, its top goal scorer this
    /// regular season, or a clear gap between its attack, defence or goalie and the human's club's
    /// (<see cref="ClubStrengths"/>). Every true tip is a candidate (<see cref="Tips"/>); the season seed
    /// and the game pick one, and one of its <see cref="Variants"/> lines, so the same game always gets
    /// the same tip. <see cref="RuleSet.ScoutingTips"/> turns it off.</summary>
    internal sealed class ScoutingTip : IPreGameTalkSource
    {
        /// <summary>The lines per tip.</summary>
        public const int Variants = 3;

        /// <summary>How many rating points apart a strength has to be before the coach calls it a gap.</summary>
        public const int Gap = 5;

        /// <summary>The goals a top scorer needs before the coach names him.</summary>
        public const int ScorerGoals = 2;

        /// <inheritdoc/>
        public void Fill(DressingRoomContext context, PreGameTalkLines lines)
        {
            if (!context.Rules.ScoutingTips || !Pick(context).TryGet(out Tip tip))
            {
                return;
            }

            lines.Say(new DressingRoomEntry(tip.Line(Variant(context.Season, tip.Kind))));
        }

        /// <summary>Gets the tip the coach gives before the human's next game, or None without an opponent
        /// (or without its data).</summary>
        public static Maybe<Tip> Pick(DressingRoomContext context)
        {
            IReadOnlyList<Tip> tips = Tips(context);
            return tips.Count == 0
                ? Maybe.None<Tip>()
                : tips[Hash(context.Season, "pick") % tips.Count];
        }

        /// <summary>Gets every tip that is true for the human's next opponent, in <see cref="ScoutKind"/> order;
        /// empty without an opponent or its data.</summary>
        public static IReadOnlyList<Tip> Tips(DressingRoomContext context)
        {
            if (
                !TalkOpening.Opponent(context.Season).TryGet(out string opponent)
                || !Club(context, opponent).TryGet(out ClubData them)
            )
            {
                return [];
            }

            ClubTactics plays = them.Tactics;
            List<Tip> tips =
            [
                new(
                    plays.Forecheck switch
                    {
                        Forecheck.Aggressive => ScoutKind.ForecheckAggressive,
                        Forecheck.Trap => ScoutKind.ForecheckTrap,
                        Forecheck.Balanced => ScoutKind.ForecheckBalanced,
                        _ => throw new NotSupportedException("no tip for " + plays.Forecheck),
                    }
                ),
                new(
                    plays.Entry == ZoneEntry.DumpAndChase
                        ? ScoutKind.EntryDump
                        : ScoutKind.EntryCarry
                ),
                new(
                    plays.Style switch
                    {
                        PlayStyle.Physical => ScoutKind.StylePhysical,
                        PlayStyle.ShootFromAnywhere => ScoutKind.StyleShootFromAnywhere,
                        PlayStyle.DefensiveShell => ScoutKind.StyleDefensiveShell,
                        PlayStyle.Balanced => ScoutKind.StyleBalanced,
                        _ => throw new NotSupportedException("no tip for " + plays.Style),
                    },
                    ClubStrengths.StyleName(plays.Style)
                ),
                new(
                    plays.PowerPlay switch
                    {
                        PowerPlayShape.Umbrella => ScoutKind.PowerPlayUmbrella,
                        PowerPlayShape.OneThreeOne => ScoutKind.PowerPlayOneThreeOne,
                        PowerPlayShape.Mixed => ScoutKind.PowerPlayMixed,
                        _ => throw new NotSupportedException("no tip for " + plays.PowerPlay),
                    }
                ),
            ];

            if (TopScorer(context.Season, opponent).TryGet(out PlayerStats scorer))
            {
                tips.Add(
                    new Tip(
                        ScoutKind.TopScorer,
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"#{scorer.Number} {scorer.Name}"
                        ),
                        scorer.Goals
                    )
                );
            }

            if (Club(context, context.UserClub).TryGet(out ClubData us))
            {
                ClubStrengths a = ClubStrengths.Of(them),
                    b = ClubStrengths.Of(us);
                AddGap(tips, a.Attack - b.Attack, ScoutKind.StrongAttack, ScoutKind.WeakAttack);
                AddGap(tips, a.Defence - b.Defence, ScoutKind.StrongDefence, ScoutKind.WeakDefence);
                AddGap(tips, a.Goalie - b.Goalie, ScoutKind.StrongGoalie, ScoutKind.WeakGoalie);
            }

            return [.. tips.OrderBy(t => t.Kind)];
        }

        /// <summary>Gets a club's top goal scorer this regular season (the stats card's pick: most goals, then
        /// points, fewest games, name), once he has <see cref="ScorerGoals"/>; None before.</summary>
        public static Maybe<PlayerStats> TopScorer(SeasonState season, string clubId)
        {
            return season
                .Stats.Regular.Players.Where(p =>
                    p.Position != PlayingPosition.G
                    && p.Goals >= ScorerGoals
                    && string.Equals(p.Club, clubId, StringComparison.Ordinal)
                )
                .OrderByDescending(p => p.Goals)
                .ThenByDescending(p => p.Points)
                .ThenBy(p => p.Games)
                .ThenBy(p => p.Name, StringComparer.Ordinal)
                .FirstOrNone();
        }

        /// <summary>Gets the string key of a tip's line, variant 0 to <see cref="Variants"/> - 1.</summary>
        public static string Key(ScoutKind kind, int variant)
        {
            return string.Create(CultureInfo.InvariantCulture, $"Scout{kind}{variant + 1}");
        }

        /// <summary>Gets which line of a tip the next game gets: fixed by the season seed and the game.</summary>
        public static int Variant(SeasonState season, ScoutKind kind)
        {
            return Hash(season, "line:" + kind) % Variants;
        }

        /// <summary>Gets the key of the human's next game: its fixture, or its playoff round and game in the series.</summary>
        public static string NextKey(SeasonState season)
        {
            return season.Playoffs.TryGet(out PlayoffState playoffs)
                ? playoffs
                    .UserSeries.Select(s =>
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"p{playoffs.Rounds.Count - 1}.{s.Games.Count}"
                        )
                    )
                    .Or("p")
                : season.NextGame.Select(g => SeasonStats.GameKey(g.Number)).Or("");
        }

        private static int Hash(SeasonState season, string stream)
        {
            return Seeds.For(
                season.Seed == 0 ? 1 : season.Seed,
                "scout:" + NextKey(season) + ":" + stream,
                0
            );
        }

        private static void AddGap(List<Tip> tips, int gap, ScoutKind strong, ScoutKind weak)
        {
            if (gap >= Gap)
            {
                tips.Add(new Tip(strong));
            }
            else if (gap <= -Gap)
            {
                tips.Add(new Tip(weak));
            }
        }

        private static Maybe<ClubData> Club(DressingRoomContext context, string clubId)
        {
            return context.Clubs.FirstOrNone(c =>
                string.Equals(c.Id, clubId, StringComparison.Ordinal)
            );
        }

        /// <summary>One true thing about the opponent.</summary>
        /// <param name="Kind">What the tip is about.</param>
        /// <param name="Name">The playstyle's or the top scorer's name, filled into the line ("" for the rest).</param>
        /// <param name="Count">The top scorer's goals (0 for the rest).</param>
        public sealed record Tip(ScoutKind Kind, string Name = "", int Count = 0)
        {
            /// <summary>Gets the tip as the coach says it, in the language shown.</summary>
            public string Line(int variant)
            {
                return Text.Format(Key(Kind, variant), Name, Count);
            }
        }
    }
}
