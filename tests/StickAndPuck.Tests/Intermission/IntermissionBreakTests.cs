using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Intermission;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Rules.Overtime;
using StickAndPuck.Tests.Support;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.Tests.Intermission
{
    /// <summary>
    /// The intermission rule: each preset's length (SHL and NHL the whole Zamboni drive, Arcade at double
    /// pace, Off a short pause with no Zamboni), the corner tag with the time left and the skip, and the
    /// Zamboni break before a playoff overtime.
    /// </summary>
    public sealed class IntermissionBreakTests : CheckedTest
    {
        /// <summary>A match of 3-second periods by the given rules, the starters on and the match flow begun.</summary>
        private static World Make(RuleSet rules, Maybe<OvertimeFormat> format = default)
        {
            return new WorldBuilder()
                .Setup(s =>
                    s with
                    {
                        PeriodSeconds = 3,
                        Overtime = format.HasValue ? OvertimeRule.Shl : OvertimeRule.None,
                        OvertimeFormat = format,
                        Rules = rules,
                    }
                )
                .Switches(s => s.OvertimeSeconds = 3)
                .Build();
        }

        /// <summary>SHL and NHL drive the whole route, Arcade at double pace; a player can turn it off.</summary>
        [Test]
        public void Presets()
        {
            Check(RuleSet.Shl.Intermission == IntermissionLength.Standard, "SHL: a standard break");
            Check(RuleSet.Nhl.Intermission == IntermissionLength.Standard, "NHL: a standard break");
            Check(RuleSet.Arcade.Intermission == IntermissionLength.Quick, "Arcade: a quick break");
            RuleRow row = RuleRows.All.Single(r =>
                string.Equals(r.Key, nameof(RuleSet.Intermission), StringComparison.Ordinal)
            );
            Check(
                row.Plays
                    && row.Values.SequenceEqual(
                        ["Standard", "Quick", "Off"],
                        StringComparer.Ordinal
                    ),
                "the rules page offers standard, quick and off"
            );
            Check(
                row.Set(RuleSet.Shl, "Off").Intermission == IntermissionLength.Off,
                "the row turns the break off"
            );
            float standard = IntermissionSystem.BreakSeconds(IntermissionLength.Standard),
                quick = IntermissionSystem.BreakSeconds(IntermissionLength.Quick);
            Check(
                quick < standard * 0.6f && quick > standard * 0.4f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"a quick break is about half a standard one ({quick:F1} s against {standard:F1} s)"
                )
            );
        }

        /// <summary>Each preset's first intermission, played through, lasts its length; Off sends no Zamboni.</summary>
        [Test]
        [Arguments(RulePreset.Shl)]
        [Arguments(RulePreset.Nhl)]
        [Arguments(RulePreset.Arcade)]
        public void LengthPerPreset(RulePreset preset)
        {
            RuleSet rules = RuleSet.Of(preset);
            (float seconds, int drives) = FirstIntermission(rules);
            float wanted = IntermissionSystem.BreakSeconds(rules.Intermission);
            Check(
                MathF.Abs(seconds - wanted) < 0.2f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{preset}: the intermission lasts {seconds:F1} s ({wanted:F1} s wanted)"
                )
            );
            Check(
                drives == 1,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{preset}: the Zamboni drives once ({drives})"
                )
            );
        }

        /// <summary>With the rule Off the next period follows the short pause, with no Zamboni.</summary>
        [Test]
        public void Off()
        {
            (float seconds, int drives) = FirstIntermission(
                RuleSet.Shl with
                {
                    Intermission = IntermissionLength.Off,
                }
            );
            Check(
                MathF.Abs(seconds - MatchTuning.IntermissionSeconds) < 0.1f,
                string.Create(CultureInfo.InvariantCulture, $"the pause lasts {seconds:F1} s")
            );
            Check(drives == 0, "no Zamboni comes out");
        }

        /// <summary>The corner tag shows what follows, the time left and the skip button.</summary>
        [Test]
        public void Prompt()
        {
            IntermissionSystem.Drive drive = new() { Period = 1, Rate = 2f };
            string text = IntermissionSystem.Prompt(drive);
            string left = MatchFlow.ClockText(IntermissionSystem.Left(drive));
            Check(
                text.Contains(Text.Format(K.IntermissionNext, 2, left), StringComparison.Ordinal)
                    && text.Contains(Text.Get(K.Skip), StringComparison.Ordinal),
                $"the tag reads \"{text}\""
            );
            Check(
                MathF.Abs(
                    IntermissionSystem.Left(drive)
                        - IntermissionSystem.BreakSeconds(IntermissionLength.Quick)
                ) < 0.01f,
                "at the start the time left is the whole quick break"
            );
            IntermissionSystem.Drive overtime = new()
            {
                Period = 3,
                Rate = 1f,
                IntoOvertime = true,
            };
            string ot = IntermissionSystem.Prompt(overtime);
            Check(
                ot.StartsWith(
                    Text.Format(
                        K.IntermissionOvertime,
                        MatchFlow.ClockText(IntermissionSystem.Left(overtime))
                    ),
                    StringComparison.Ordinal
                ),
                $"before overtime the tag reads \"{ot}\""
            );
        }

        /// <summary>A tied playoff game gets a Zamboni break after regulation, then the 5 on 5 overtime period.</summary>
        [Test]
        public void BreakBeforePlayoffOvertime()
        {
            World world = Make(RuleSet.Shl, OvertimeFormat.ShlPlayoffs with { PeriodSeconds = 3 });
            IntermissionSystem system = new(() => false);
            int drives = 0;
            bool intoOvertime = false;
            float breakStart = -1,
                overtimeAt = -1;
            Maybe<IntermissionSystem.Drive> last = Maybe.None<IntermissionSystem.Drive>();
            Run(
                world,
                system,
                w => w.Match.Period == 4 && w.Match.Phase == MatchPhase.Faceoff,
                300f,
                w =>
                {
                    if (system.Current.TryGet(out IntermissionSystem.Drive d) && d != last)
                    {
                        drives++;
                        last = d;
                        if (d.IntoOvertime)
                        {
                            intoOvertime = true;
                            breakStart = w.Time;
                        }
                    }

                    if (w.Match.Phase == MatchPhase.Overtime && overtimeAt < 0)
                    {
                        overtimeAt = w.Time;
                    }
                }
            );
            Check(
                drives == 3 && intoOvertime,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the Zamboni drives after periods 1, 2 and the tied third ({drives})"
                )
            );
            Check(
                overtimeAt - breakStart >= IntermissionSystem.Route.Duration - 0.1f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the overtime waits for the whole drive ({overtimeAt - breakStart:F1} s)"
                )
            );
            Check(
                world.Match.Period == 4
                    && world.OnIce(Side.Home).Where(p => !p.IsGoalie).Take(6).Count() == 5,
                "then the playoff overtime faces off 5 on 5"
            );
        }

        /// <summary>No break before a 3 on 3 regular season overtime, nor before a playoff one with the rule off.</summary>
        [Test]
        [Arguments(false)]
        [Arguments(true)]
        public void NoBreakBeforeOtherOvertime(bool playoffOff)
        {
            World world = playoffOff
                ? Make(
                    RuleSet.Shl with
                    {
                        Intermission = IntermissionLength.Off,
                    },
                    OvertimeFormat.ShlPlayoffs with
                    {
                        PeriodSeconds = 3,
                    }
                )
                : Make(RuleSet.Shl, OvertimeFormat.Shl);
            IntermissionSystem system = new(() => false);
            Run(world, system, w => w.Match.Period == 3 && w.Match.Phase == MatchPhase.Play, 300f);
            Run(world, system, w => w.Match.Phase != MatchPhase.Play, 10f);
            Check(
                world.Match.Phase == MatchPhase.Overtime,
                $"a tied regulation goes straight to the overtime rules ({world.Match.Phase})"
            );
        }

        /// <summary>Plays to the end of the first intermission; returns its real seconds and the Zamboni drives in it.</summary>
        private static (float Seconds, int Drives) FirstIntermission(RuleSet rules)
        {
            World world = Make(rules);
            IntermissionSystem system = new(() => false);
            float start = -1;
            int drives = 0;
            Maybe<IntermissionSystem.Drive> last = Maybe.None<IntermissionSystem.Drive>();
            Run(
                world,
                system,
                w => w.Match.Period == 2,
                200f,
                w =>
                {
                    if (w.Match.Phase == MatchPhase.Intermission && start < 0)
                    {
                        start = w.Time;
                    }

                    if (system.Current.TryGet(out IntermissionSystem.Drive d) && d != last)
                    {
                        drives++;
                        last = d;
                    }
                }
            );
            return (world.Time - start, drives);
        }

        private static void Run(
            World world,
            IntermissionSystem system,
            Func<World, bool> until,
            float limit
        )
        {
            Run(world, system, until, limit, _ => { });
        }

        private static void Run(
            World world,
            IntermissionSystem system,
            Func<World, bool> until,
            float limit,
            Action<World> each
        )
        {
            ISystem[] systems = [new MatchRules(), new OvertimeRules(Seeds.Dice(1)), system];
            for (int i = 0; i < limit / Game.Step && !until(world); i++)
            {
                world.Events.Clear();
                foreach (ISystem s in systems)
                {
                    s.Update(world, Game.Step);
                }

                each(world);
                world.Time += Game.Step;
                world.Tick++;
            }
        }
    }
}
