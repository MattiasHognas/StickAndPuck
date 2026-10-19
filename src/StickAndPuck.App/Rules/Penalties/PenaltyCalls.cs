using System.Globalization;
using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Lines;
using StickAndPuck.App.Rules.Match;

namespace StickAndPuck.App.Rules.Penalties
{
    /// <summary>The penalty calls other systems and scenes use. <see cref="PenaltyRules"/> runs them each step.
    /// A call is delayed (<see cref="Signal"/>): the referee's arm goes up and play goes on until the
    /// offending club touches the puck, which blows the whistle, or play stops by itself. Then the
    /// skater goes to the box (<see cref="Player.OnIce"/> false) for a two minute minor
    /// (<see cref="MinorSeconds"/>), listed in <see cref="MatchState.Penalties"/>, and his club plays a
    /// skater short. The clock of a penalty runs only while the game clock runs. A goal by the club
    /// with more skaters ends the shortest penalty of the other club; a goal against the offending club
    /// while a call is delayed cancels the call. Penalties carry over between periods and end with
    /// regulation; none are called in the shootout.
    /// Calls happen in overtime too. The strength each club plays at comes from the skaters
    /// on the ice when the period started (<see cref="TargetSkaters"/>): at 5 on 5 the penalised
    /// club loses a skater, at 3 on 3 it keeps three and the other club adds one (4 on 3, then 5 on 3).
    /// A club that has pulled its goalie (<see cref="GoaliePull"/>) has one skater more.
    /// As <see cref="MatchSetup.Rules"/> says: penalties running at the end of regulation carry
    /// into overtime (<see cref="RuleSet.PenaltiesCarryIntoOvertime"/>); a club already two short
    /// takes a third penalty into the box, where it waits and starts when one of the two ends, the
    /// skater whose time is up staying in the box until play stops (<see cref="RuleSet.StackPenalties"/>,
    /// <see cref="Waiting"/>); and equal minors on both clubs at the same whistle are substituted,
    /// leaving the strength as it was, or in the NHL played 4 on 4 when both clubs were at full
    /// strength (<see cref="RuleSet.CoincidentalMinors"/>, <see cref="Substituted"/>).
    /// With <see cref="RuleSet.PenaltySeverity"/> Full: a call can cost more than a minor
    /// (<see cref="Severity"/>). A double minor is two minors back to back, a power play goal ending
    /// the half that runs; a major is five minutes no goal ends; a misconduct keeps the offender in the
    /// box ten minutes while his club plays at full strength (<see cref="Misconducts"/>); a game
    /// misconduct ejects him (<see cref="Ejected"/>): he leaves the squad, the lines refill without him
    /// (<see cref="Bench.Eject"/>) and a teammate comes out of the box when the club's time is up.
    /// With MinorsOnly every referee's call is a minor. <see cref="Give"/> hands out any severity at
    /// once, for fights.</summary>
    public static partial class PenaltyCalls
    {
        /// <summary>Gets the calls signalled and not yet made.</summary>
        public static IReadOnlyList<DelayedPenalty> Delayed(World world)
        {
            return Book(world).Delayed;
        }

        /// <summary>Gets the length of a minor in this match: two minutes, shortened in proportion to shorter periods.</summary>
        public static float MinorSeconds(World world)
        {
            return PenaltyTuning.MinorSeconds
                * world.Setup.PeriodSeconds
                / PenaltyTuning.FullPeriodSeconds;
        }

        /// <summary>Gets how many penalties a club is serving.</summary>
        public static int Serving(World world, Side side)
        {
            PenaltyBook book = Book(world);
            return world.Match.Penalties.Count(p =>
                p.Side == side && !book.Substituted.Contains(p.PlayerId)
            );
        }

        /// <summary>Gets the penalties waiting in the box for one of the club's two running penalties to end.</summary>
        /// <returns>The waiting penalties, in the order they start; their clocks stand still.</returns>
        public static IReadOnlyList<PenaltyState> Waiting(World world)
        {
            return Book(world).Waiting;
        }

        /// <summary>Gets whether a running penalty is one of a coincidental pair that was substituted: it leaves the strength alone.</summary>
        public static bool Substituted(World world, int playerId)
        {
            return Book(world).Substituted.Contains(playerId);
        }

        /// <summary>Gets the club on the power play: the one serving fewer penalties, or None at even strength.</summary>
        public static SideOrNone PowerPlay(World world)
        {
            int home = Serving(world, Side.Home),
                away = Serving(world, Side.Away);
            return (home < away)
                ? SideOrNone.Home
                : ((away < home) ? SideOrNone.Away : SideOrNone.None);
        }

        /// <summary>Gets the skaters each club plays at full strength this period: 5 in regulation, 3 (or 5) in overtime, read off the ice when the period started.</summary>
        public static int BaseSkaters(World world)
        {
            return Book(world).BaseSkaters;
        }

        /// <summary>Gets how many skaters a club should have on the ice now, its goalie aside: the short-handed
        /// club plays <see cref="BaseSkaters"/> less its penalties but never under three, the club with
        /// fewer penalties plays that count plus its advantage, never over five. An extra attacker for
        /// a pulled goalie adds one.</summary>
        public static int TargetSkaters(World world, Side side)
        {
            Side other = side == Side.Home ? Side.Away : Side.Home;
            int mine = Serving(world, side),
                theirs = Serving(world, other),
                full = BaseSkaters(world);
            int skaters =
                mine < theirs
                    ? Math.Min(
                        PenaltyTuning.MaxSkaters,
                        Math.Max(PenaltyTuning.MinSkaters, full - theirs) + theirs - mine
                    )
                    : Math.Max(PenaltyTuning.MinSkaters, full - mine);
            return skaters + (GoaliePull.IsPulled(world, side) ? 1 : 0);
        }

        /// <summary>Gets whether a player is in the penalty box.</summary>
        public static bool InBox(World world, int playerId)
        {
            PenaltyBook book = Book(world);
            return world.Match.Penalties.Any(p => p.PlayerId == playerId)
                || book.Waiting.Exists(p => p.PlayerId == playerId)
                || book.Held.Contains(playerId)
                || book.Misconducts.Exists(p => p.PlayerId == playerId)
                || book.Ejected.Contains(playerId);
        }

        /// <summary>Gets the misconducts running: the offender sits them out, his club at full strength.</summary>
        /// <returns>Each misconduct with the time the offender has left in the box, minor included.</returns>
        public static IReadOnlyList<PenaltyState> Misconducts(World world)
        {
            return Book(world).Misconducts;
        }

        /// <summary>Gets whether a player was thrown out of the match by a game misconduct.</summary>
        public static bool Ejected(World world, int playerId)
        {
            return Book(world).Ejected.Contains(playerId);
        }

        /// <summary>Gets what a running or waiting penalty costs.</summary>
        public static Severity SeverityOf(World world, int playerId)
        {
            return Book(world).Lengths.TryGetValue(playerId, out Severity severity)
                ? severity
                : Severity.Minor;
        }

        /// <summary>Gets the time a club serves for a call: a minor, two of them, a major, or none for a misconduct alone.</summary>
        public static float ClubSeconds(World world, Severity severity)
        {
            float minor = MinorSeconds(world);
            return severity switch
            {
                Severity.Minor or Severity.MinorAndMisconduct => minor,
                Severity.DoubleMinor => 2 * minor,
                Severity.Major or Severity.MajorAndGameMisconduct => minor
                    * PenaltyTuning.MajorSeconds
                    / PenaltyTuning.MinorSeconds,
                Severity.Misconduct or Severity.GameMisconduct => 0,
                _ => throw new NotSupportedException(
                    $"Unhandled severity {severity} in club seconds"
                ),
            };
        }

        /// <summary>Gets the length of a misconduct in this match: ten minutes, shortened in proportion to shorter periods.</summary>
        public static float MisconductSeconds(World world)
        {
            return MinorSeconds(world)
                * PenaltyTuning.MisconductSeconds
                / PenaltyTuning.MinorSeconds;
        }

        /// <summary>Hands out a penalty at once, with no delayed call: what a fight costs uses it. Calls on
        /// both clubs at the same moment are not paired as coincidental here.</summary>
        public static void Give(World world, int playerId, string infraction, Severity severity)
        {
            if (
                playerId < 0
                || playerId >= world.Players.Count
                || world.Players[playerId].IsGoalie
                || InBox(world, playerId)
            )
            {
                return;
            }

            Player p = world.Players[playerId];
            Assess(
                world,
                Book(world),
                new DelayedPenalty(p.Side, p.Id, infraction, world.Time, severity)
            );
        }

        /// <summary>The referee sees an infraction: the arm goes up (<see cref="Referee.RefereeState.Signal"/>) and the call
        /// waits for the offending club to touch the puck. Waved off when penalties are off in the settings,
        /// outside play (the shootout included), for a goalie, for a skater already called, or when the club already serves two
        /// and the rules do not stack penalties (<see cref="RuleSet.StackPenalties"/>). Never called when the
        /// infraction's switch is off (<see cref="Infraction.Called"/>); every call is a minor under MinorsOnly; a call
        /// after the whistle (roughing) is made at the stoppage it came in.</summary>
        public static bool Signal(
            World world,
            int playerId,
            string infraction,
            Severity severity = Severity.Minor,
            bool afterWhistle = false
        )
        {
            if (!world.Settings.Penalties || !MatchFlow.IsRunning(world))
            {
                return false;
            }

            if (playerId < 0 || playerId >= world.Players.Count)
            {
                return false;
            }

            MatchState m = world.Match;
            if (m.Phase != MatchPhase.Play && !(afterWhistle && m.Phase == MatchPhase.Stopped))
            {
                return false;
            }

            if (!Infraction.Called(world.Setup.Rules, infraction))
            {
                return false;
            }

            if (world.Setup.Rules.PenaltySeverity == PenaltySeverity.MinorsOnly)
            {
                severity = Severity.Minor;
            }

            Player p = world.Players[playerId];
            PenaltyBook book = Book(world);
            if (
                p.IsGoalie
                || InBox(world, playerId)
                || book.Delayed.Exists(d => d.PlayerId == playerId)
            )
            {
                return false;
            }

            if (
                !world.Setup.Rules.StackPenalties
                && Serving(world, p.Side) + book.Delayed.Count(d => d.Side == p.Side)
                    >= PenaltyTuning.MaxServing
            )
            {
                return false;
            }

            book.Delayed.Add(
                new DelayedPenalty(p.Side, playerId, infraction, world.Time, severity)
            );
            Referee.RefereeState.Signal(world, infraction, playerId);
            PenaltyBook.Log(
                world,
                $"{infraction} on {Name(world, p)}{(severity == Severity.Minor ? "" : $" ({severity})")}, {(afterWhistle ? "after the whistle" : "delayed")}"
            );
            return true;
        }

        /// <summary>Gets the end-zone dot of the club that defends that end, on the puck's side: where play restarts after a call.</summary>
        public static Vector3 FaceoffDot(World world, Side side)
        {
            int end = FaceoffFormation.DefendedEnd(world.Match, side);
            float z = world.Puck.Position.Z < 0 ? -6.7f : 6.7f;
            return new Vector3(end * 20.7f, 0, z);
        }

        /// <summary>Gets the spot on the ice by a club's penalty box door, on the near boards opposite the benches.</summary>
        public static Vector3 BoxDoor(Side side)
        {
            return new Vector3(
                side == Side.Home ? -PenaltyTuning.BoxDoorX : PenaltyTuning.BoxDoorX,
                0,
                RinkSpec.HalfWidth - PenaltyTuning.BoxDoorInset
            );
        }

        /// <summary>Makes every delayed call: each skater to the box, a Penalty event per call, the referee's signal.
        /// Calls on both clubs at the same whistle pair up as coincidental minors: substituted, so
        /// the strength stays, except one each from full strength under the NHL's 4 on 4.</summary>
        internal static void CallDelayed(World world, PenaltyBook book)
        {
            // Only minors pair up; a major, double minor or misconduct is served as called
            int pairs = Math.Min(
                book.Delayed.Count(d => d.Side == Side.Home && d.Severity == Severity.Minor),
                book.Delayed.Count(d => d.Side == Side.Away && d.Severity == Severity.Minor)
            );
            bool fourOnFour =
                world.Setup.Rules.CoincidentalMinors == CoincidentalMinors.FourOnFour
                && pairs == 1
                && book.Delayed.Count == 2
                && Serving(world, Side.Home) == 0
                && Serving(world, Side.Away) == 0;
            HashSet<int> paired = [];
            if (!fourOnFour)
            {
                foreach (Side side in new[] { Side.Home, Side.Away })
                {
                    foreach (
                        DelayedPenalty d in book
                            .Delayed.Where(d => d.Side == side && d.Severity == Severity.Minor)
                            .Take(pairs)
                    )
                    {
                        _ = paired.Add(d.PlayerId);
                    }
                }
            }

            foreach (DelayedPenalty d in book.Delayed)
            {
                Assess(world, book, d, paired.Contains(d.PlayerId));
            }

            book.Delayed.Clear();
        }

        /// <summary>Sends a skater to the box and publishes the Penalty event (Strength = seconds the club serves,
        /// or the misconduct's for a misconduct alone). A substituted coincidental minor leaves the
        /// strength alone; a club already serving two puts the penalty on <see cref="Waiting"/>.
        /// A misconduct keeps the offender in the box on his own clock, a game misconduct ejects him.</summary>
        internal static void Assess(
            World world,
            PenaltyBook book,
            DelayedPenalty d,
            bool substitute = false
        )
        {
            Player p = world.Players[d.PlayerId];
            Severity severity = d.Severity;
            float seconds = ClubSeconds(world, severity);
            bool waits = AddServedPenalty(world, book, p, d, severity, seconds, substitute);
            AddMisconductPenalty(world, book, p, d, severity, seconds);

            if (severity is Severity.MajorAndGameMisconduct or Severity.GameMisconduct)
            {
                Eject(world, book, p);
            }
            else
            {
                SendToBox(world, book, p);
            }

            PublishPenaltyEvent(world, book, d, p, severity, seconds, substitute, waits);
        }

        private static bool AddServedPenalty(
            World world,
            PenaltyBook book,
            Player p,
            DelayedPenalty d,
            Severity severity,
            float seconds,
            bool substitute
        )
        {
            if (seconds <= 0)
            {
                return false;
            }

            PenaltyState penalty = new(p.Id, p.Side, d.Infraction, seconds);
            bool waits = !substitute && Serving(world, p.Side) >= PenaltyTuning.MaxServing;
            if (waits)
            {
                book.Waiting.Add(penalty);
            }
            else
            {
                world.Match.Penalties.Add(penalty);
            }

            if (substitute)
            {
                _ = book.Substituted.Add(p.Id);
            }

            if (severity != Severity.Minor)
            {
                book.Lengths[p.Id] = severity;
            }

            // a skater who changed off during the delay still goes in; a teammate of his group leaves the ice so the club still plays short
            if (
                !p.OnIce
                && !waits
                && !substitute
                && world
                    .OnIce(p.Side)
                    .Where(o => !o.IsGoalie && Squad.GroupOf(o) == Squad.GroupOf(p))
                    .MinBy(o => o.Position.Z)
                    is { } stand
            )
            {
                ToBench(world, stand);
            }

            return waits;
        }

        private static void AddMisconductPenalty(
            World world,
            PenaltyBook book,
            Player p,
            DelayedPenalty d,
            Severity severity,
            float seconds
        )
        {
            if (severity is Severity.Misconduct or Severity.MinorAndMisconduct)
            {
                book.Misconducts.Add(
                    new PenaltyState(p.Id, p.Side, d.Infraction, seconds + MisconductSeconds(world))
                );
            }
        }

        private static void PublishPenaltyEvent(
            World world,
            PenaltyBook book,
            DelayedPenalty d,
            Player p,
            Severity severity,
            float seconds,
            bool substitute,
            bool waits
        )
        {
            book.Called[(int)p.Side]++;
            float shown = seconds > 0 ? seconds : MisconductSeconds(world);
            world.Events.Publish(new PenaltyCalled(world.Time, p.Side, p.Id, p.Position, shown));
            Referee.RefereeState.Announce(world, d.Infraction, p.Id);
            string what = severity == Severity.Minor ? "" : $", {severity}";
            string penaltyState = "";
            if (substitute)
            {
                penaltyState = ", coincidental, substituted";
            }
            else if (waits)
            {
                penaltyState = ", waits in the box";
            }

            PenaltyBook.Log(
                world,
                $"{d.Infraction}, {Name(world, p)}, {MatchFlow.ClockText(shown)}{what}{penaltyState}"
            );
        }

        internal static PenaltyBook Book(World world)
        {
            return world.Penalties.Get(() =>
                new PenaltyBook
                {
                    Rng = Seeds.Dice(world.Seed, "penalties", 14),
                    ShotRng = Seeds.Dice(world.Seed, "penaltyshot", 15),
                }
            );
        }

        internal static string Name(World world, Player p)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{world.Setup.Club(p.Side).ShortName} #{p.Data.Number} {p.Data.LastName}"
            );
        }
    }
}
