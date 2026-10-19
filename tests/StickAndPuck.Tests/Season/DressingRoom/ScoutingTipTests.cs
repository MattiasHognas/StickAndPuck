using System.Globalization;
using StickAndPuck.App.AI.Tactics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Screens;
using StickAndPuck.App.Season.DressingRoom;
using StickAndPuck.App.Season.Schedule;
using StickAndPuck.App.Season.Stats;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Season.DressingRoom
{
    /// <summary>
    /// The coach's scouting tip (W141): every tip is true for tonight's opponent (its forecheck, entries,
    /// playstyle and power play as in its data, its top scorer named with his goals, strength gaps only
    /// when the ratings are that far apart), one is picked the same way every time and said after the
    /// opening line, in both languages, and nothing is said with the rule off.
    /// </summary>
    public sealed class ScoutingTipTests : CheckedTest
    {
        private static readonly ScoutKind[] Forechecks =
        [
            ScoutKind.ForecheckBalanced,
            ScoutKind.ForecheckAggressive,
            ScoutKind.ForecheckTrap,
        ];

        private static readonly ScoutKind[] Styles =
        [
            ScoutKind.StyleBalanced,
            ScoutKind.StylePhysical,
            ScoutKind.StyleShootFromAnywhere,
            ScoutKind.StyleDefensiveShell,
        ];

        private static readonly ScoutKind[] PowerPlays =
        [
            ScoutKind.PowerPlayUmbrella,
            ScoutKind.PowerPlayOneThreeOne,
            ScoutKind.PowerPlayMixed,
        ];

        private static readonly ScoutKind[] Gaps =
        [
            ScoutKind.StrongGoalie,
            ScoutKind.WeakGoalie,
            ScoutKind.StrongAttack,
            ScoutKind.WeakAttack,
            ScoutKind.StrongDefence,
            ScoutKind.WeakDefence,
        ];

        private static bool Eq(string a, string b)
        {
            return string.Equals(a, b, StringComparison.Ordinal);
        }

        /// <summary>A club with every forward, defender and goalie rated exactly at the given strengths.</summary>
        private static ClubData Rated(ClubData club, int attack, int defence, int goalie)
        {
            Traits goalies = new()
            {
                GoalieReflexes = goalie,
                GoalieGlove = goalie,
                GoalieBlocker = goalie,
                GoalieReboundControl = goalie,
                GoaliePuckHandling = goalie,
            };
            Traits defenders = new()
            {
                Checking = defence,
                HockeyIq = defence,
                PuckControl = defence,
            };
            Traits forwards = new()
            {
                ShotAccuracy = attack,
                ShotPower = attack,
                PuckControl = attack,
                HockeyIq = attack,
            };
            return club with
            {
                Roster =
                [
                    .. club.Roster.Select(p =>
                        p with
                        {
                            Traits =
                                p.Position == PlayingPosition.G ? goalies
                                : p.Position == PlayingPosition.D ? defenders
                                : forwards,
                        }
                    ),
                ],
            };
        }

        /// <summary>A test season and its dressing room, with the human's club and tonight's opponent rated
        /// as given and the opponent playing <paramref name="tactics"/>.</summary>
        private static (DressingRoomContext Context, string Opponent) Room(
            ClubTactics tactics,
            (int Attack, int Defence, int Goalie) us = default,
            (int Attack, int Defence, int Goalie) them = default
        )
        {
            IReadOnlyList<ClubData> all = WorldBuilder.Clubs;
            SeasonState season = SeasonState.New(all, all[0].Id, 5);
            string opponent = TalkOpening.Opponent(season).Or("");
            us = us == default ? (60, 60, 60) : us;
            them = them == default ? (60, 60, 60) : them;
            List<ClubData> clubs =
            [
                .. all.Select(c =>
                    Eq(c.Id, season.UserClub) ? Rated(c, us.Attack, us.Defence, us.Goalie)
                    : Eq(c.Id, opponent)
                        ? Rated(c, them.Attack, them.Defence, them.Goalie) with
                        {
                            Tactics = tactics,
                        }
                    : c
                ),
            ];
            return (DressingRoomContext.Of(season, clubs, RuleSet.Shl), opponent);
        }

        private static IReadOnlyList<ScoutKind> Kinds(DressingRoomContext context)
        {
            return [.. ScoutingTip.Tips(context).Select(t => t.Kind)];
        }

        [Test]
        public void ForecheckTipOnlyMatchesTheirForecheck()
        {
            (Forecheck plays, ScoutKind tip)[] cases =
            [
                (Forecheck.Balanced, ScoutKind.ForecheckBalanced),
                (Forecheck.Aggressive, ScoutKind.ForecheckAggressive),
                (Forecheck.Trap, ScoutKind.ForecheckTrap),
            ];
            foreach ((Forecheck plays, ScoutKind tip) in cases)
            {
                IReadOnlyList<ScoutKind> kinds = Kinds(
                    Room(new ClubTactics { Forecheck = plays }).Context
                );
                Check(
                    kinds.Contains(tip) && kinds.Where(Forechecks.Contains).Take(2).Count() == 1,
                    $"against a {plays} forecheck the only forecheck tip is {tip} ({string.Join(", ", kinds)})"
                );
            }
        }

        [Test]
        public void EntryTipOnlyMatchesTheirEntries()
        {
            IReadOnlyList<ScoutKind> dump = Kinds(
                Room(new ClubTactics { Entry = ZoneEntry.DumpAndChase }).Context
            );
            IReadOnlyList<ScoutKind> carry = Kinds(
                Room(new ClubTactics { Entry = ZoneEntry.Carry }).Context
            );
            Check(
                dump.Contains(ScoutKind.EntryDump) && !dump.Contains(ScoutKind.EntryCarry),
                "a dump-and-chase club gets the dump-in tip only"
            );
            Check(
                carry.Contains(ScoutKind.EntryCarry) && !carry.Contains(ScoutKind.EntryDump),
                "a carrying club gets the carry-in tip only"
            );
        }

        [Test]
        public void StyleTipNamesTheirPlaystyle()
        {
            (PlayStyle style, ScoutKind tip)[] cases =
            [
                (PlayStyle.Balanced, ScoutKind.StyleBalanced),
                (PlayStyle.Physical, ScoutKind.StylePhysical),
                (PlayStyle.ShootFromAnywhere, ScoutKind.StyleShootFromAnywhere),
                (PlayStyle.DefensiveShell, ScoutKind.StyleDefensiveShell),
            ];
            foreach ((PlayStyle style, ScoutKind kind) in cases)
            {
                IReadOnlyList<ScoutingTip.Tip> tips = ScoutingTip.Tips(
                    Room(new ClubTactics { Style = style }).Context
                );
                Check(
                    tips.Where(t => Styles.Contains(t.Kind)).Take(2).Count() == 1
                        && tips.Any(t =>
                            t.Kind == kind && Eq(t.Name, ClubStrengths.StyleName(style))
                        ),
                    $"a {style} club gets the {kind} tip with its style's name"
                );
            }

            ScoutingTip.Tip shell = new(
                ScoutKind.StyleDefensiveShell,
                ClubStrengths.StyleName(PlayStyle.DefensiveShell)
            );
            Check(
                shell.Line(0).Contains("DEF. SHELL", StringComparison.Ordinal),
                $"the first line of a style tip names the style ({shell.Line(0)})"
            );
        }

        [Test]
        public void PowerPlayTipOnlyMatchesTheirShape()
        {
            (PowerPlayShape shape, ScoutKind tip)[] cases =
            [
                (PowerPlayShape.Umbrella, ScoutKind.PowerPlayUmbrella),
                (PowerPlayShape.OneThreeOne, ScoutKind.PowerPlayOneThreeOne),
                (PowerPlayShape.Mixed, ScoutKind.PowerPlayMixed),
            ];
            foreach ((PowerPlayShape shape, ScoutKind tip) in cases)
            {
                IReadOnlyList<ScoutKind> kinds = Kinds(
                    Room(new ClubTactics { PowerPlay = shape }).Context
                );
                Check(
                    kinds.Contains(tip) && kinds.Where(PowerPlays.Contains).Take(2).Count() == 1,
                    $"a {shape} power play gets only the {tip} tip"
                );
            }
        }

        [Test]
        public void StrengthGapsOnlyWhenTheyAreReal()
        {
            ClubTactics plays = new();
            Check(
                !Kinds(Room(plays).Context).Any(Gaps.Contains),
                "two clubs rated the same: no strength gap tip"
            );
            int near = ScoutingTip.Gap - 1;
            Check(
                !Kinds(Room(plays, them: (60 + near, 60 - near, 60 + near)).Context)
                    .Any(Gaps.Contains),
                "ratings just short of the gap: no strength gap tip"
            );

            int gap = ScoutingTip.Gap + 5;
            ((int, int, int) them, ScoutKind tip)[] cases =
            [
                ((60, 60, 60 + gap), ScoutKind.StrongGoalie),
                ((60, 60, 60 - gap), ScoutKind.WeakGoalie),
                ((60 + gap, 60, 60), ScoutKind.StrongAttack),
                ((60 - gap, 60, 60), ScoutKind.WeakAttack),
                ((60, 60 + gap, 60), ScoutKind.StrongDefence),
                ((60, 60 - gap, 60), ScoutKind.WeakDefence),
            ];
            foreach (((int, int, int) them, ScoutKind tip) in cases)
            {
                List<ScoutKind> gaps =
                [
                    .. Kinds(Room(plays, them: them).Context).Where(Gaps.Contains),
                ];
                Check(
                    gaps.Count == 1 && gaps[0] == tip,
                    $"an opponent rated {them} against our 60s gets only the {tip} tip ({string.Join(", ", gaps)})"
                );
            }
        }

        [Test]
        public void TopScorerNamedWithHisGoals()
        {
            (DressingRoomContext context, string opponent) = Room(new ClubTactics());
            Check(
                !Kinds(context).Contains(ScoutKind.TopScorer),
                "before anyone has scored there is no top scorer tip"
            );

            ICollection<PlayerStats> players = context.Season.Stats.Regular.Players;
            players.Add(
                new PlayerStats
                {
                    Club = opponent,
                    Number = 7,
                    Name = "Second",
                    Position = PlayingPosition.LW,
                    Games = 4,
                    Goals = 3,
                }
            );
            players.Add(
                new PlayerStats
                {
                    Club = opponent,
                    Number = 19,
                    Name = "Sniper",
                    Position = PlayingPosition.C,
                    Games = 4,
                    Goals = 5,
                }
            );
            players.Add(
                new PlayerStats
                {
                    Club = opponent,
                    Number = 1,
                    Name = "Keeper",
                    Position = PlayingPosition.G,
                    Games = 4,
                    Goals = 9,
                }
            );
            players.Add(
                new PlayerStats
                {
                    Club = context.UserClub,
                    Number = 91,
                    Name = "Ours",
                    Position = PlayingPosition.C,
                    Games = 4,
                    Goals = 12,
                }
            );
            List<ScoutingTip.Tip> scorer =
            [
                .. ScoutingTip.Tips(context).Where(t => t.Kind == ScoutKind.TopScorer),
            ];
            Check(
                scorer.Count == 1 && Eq(scorer[0].Name, "#19 Sniper") && scorer[0].Count == 5,
                $"the top scorer is the opponent's skater with the most goals ({string.Join(", ", scorer)})"
            );
            Check(
                scorer[0].Line(2).Contains("#19 Sniper", StringComparison.Ordinal)
                    && scorer[0].Line(2).Contains('5', StringComparison.Ordinal),
                $"the line names him and his goals ({scorer[0].Line(2)})"
            );

            players.Clear();
            players.Add(
                new PlayerStats
                {
                    Club = opponent,
                    Number = 19,
                    Name = "Sniper",
                    Position = PlayingPosition.C,
                    Goals = ScoutingTip.ScorerGoals - 1,
                }
            );
            Check(
                !Kinds(context).Contains(ScoutKind.TopScorer),
                "one goal is not enough to be called their sniper"
            );
        }

        [Test]
        public void SaidAfterTheOpeningTheSameEveryTime()
        {
            (DressingRoomContext context, string opponent) = Room(
                new ClubTactics { Forecheck = Forecheck.Trap }
            );
            IReadOnlyList<DressingRoomEntry> said = PreGameTalkSources.Build(context).All;
            Check(
                said.Count == 2
                    && said[0].Text.Contains(context.Name(opponent), StringComparison.Ordinal),
                "the coach opens with the opponent, then gives one scouting tip"
            );
            ScoutingTip.Tip tip = ScoutingTip
                .Pick(context)
                .Or(new ScoutingTip.Tip(ScoutKind.TopScorer));
            Check(
                ScoutingTip.Tips(context).Contains(tip)
                    && Eq(said[1].Text, tip.Line(ScoutingTip.Variant(context.Season, tip.Kind))),
                $"the tip said is one of the true ones ({said[1].Text})"
            );
            Check(
                Eq(PreGameTalkSources.Build(context).All[1].Text, said[1].Text),
                "the same game always gets the same tip"
            );

            HashSet<string> lines = [with(StringComparer.Ordinal)];
            for (int seed = 1; seed <= 40; seed++)
            {
                context.Season.Seed = seed;
                lines.Add(PreGameTalkSources.Build(context).All[1].Text);
            }

            Check(lines.Count > 4, $"other seasons get other tips and lines ({lines.Count})");

            DressingRoomContext off = context with
            {
                Rules = RuleSet.Shl with { ScoutingTips = false },
            };
            Check(
                PreGameTalkSources.Build(off).All.Count == 1,
                "with scouting tips off the coach only opens"
            );
            Check(!RuleSet.Arcade.ScoutingTips, "arcade rules have no scouting tips");
        }

        [Test]
        public void EveryLineInBothLanguages()
        {
            foreach (Language language in new[] { Language.English, Language.Swedish })
            {
                IReadOnlyDictionary<string, string> table = Text.Read(Text.FileOf(language));
                foreach (ScoutKind kind in Enum.GetValues<ScoutKind>())
                {
                    for (int v = 0; v < ScoutingTip.Variants; v++)
                    {
                        string key = ScoutingTip.Key(kind, v);
                        string line = table.GetValueOrDefault(key, "");
                        Check(
                            line.Length > 0
                                && string.Format(CultureInfo.InvariantCulture, line, "X", 3).Length
                                    > 0,
                            $"{key} has a {language} line"
                        );
                    }
                }
            }
        }
    }
}
