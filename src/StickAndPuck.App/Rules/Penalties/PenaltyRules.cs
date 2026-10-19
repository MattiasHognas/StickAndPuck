using System.Collections.Immutable;
using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Hud;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Rules.Overtime;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Rules.Penalties
{
    /// <summary>Tripping, hooking and slashing calls, the penalty box, power play and penalty kill. Runs
    /// after physics, the match rules, offside and icing, so it sees this step's checks, goals and
    /// whistles. See <see cref="PenaltyCalls"/> for the rules it keeps. Line changes stay the lines system's: a skater
    /// in the box is never sent on, so a club changes lines a skater short until he comes back.
    /// Draws the penalty box under the scorebug: each penalty's number and time left, a delayed call,
    /// and a club's extra attacker while its goalie is pulled.
    /// At every faceoff the skaters on the ice are balanced to <see cref="PenaltyCalls.TargetSkaters"/>,
    /// which gives 4 on 3 after a penalty in 3 on 3 overtime and sends an extra attacker off once the
    /// goalie is back; a skater let out of the box in overtime plays on until that next whistle.
    /// With <see cref="RuleSet.PenaltiesCarryIntoOvertime"/> the box keeps its penalties through
    /// the overtime break (calls still pending there are made), and only the shootout and the final
    /// empty it; the first overtime faceoff then takes the format's skaters as the full strength.
    /// Waiting third penalties show in the box with their clock standing; skaters kept in the box after
    /// their time go to the bench at the next stoppage.
    /// Each call is graded (<see cref="PenaltyModel.Grade"/>); a power play goal ends a minor or
    /// the running half of a double minor, never a major; misconducts run on their own clock; hits in
    /// the moments after the whistle can be called as roughing. The box shows majors with a game
    /// misconduct as "5:00 GM" and misconducts as "10:00 MISC".</summary>
    internal sealed class PenaltyRules : ISystem, IDisposable
    {
        private static readonly ImmutableArray<Side> BothSides = [Side.Home, Side.Away];

        private Maybe<HudArt> art;

        public void Start(World world)
        {
            if (MatchFlow.IsRunning(world))
            {
                art = HudArt.Load();
            }
        }

        public void Update(World world, float dt)
        {
            if (!MatchFlow.IsRunning(world))
            {
                return;
            }

            PenaltyBook book = PenaltyCalls.Book(world);
            MatchState m = world.Match;
            PenaltyCalls.Skate(world, book, dt);
            if (ShouldEndOfRegulation(world, m))
            {
                EndOfRegulation(world, book);
                book.LastPhase = m.Phase;
                return;
            }

            if (m.Phase != MatchPhase.Play)
            {
                PenaltyCalls.LetOutHeld(world, book);
            }

            if (m.Phase == MatchPhase.Overtime)
            {
                HandleOvertime(world, book, m);
                return;
            }

            if (float.IsNaN(book.FirstDrop) && m.Phase == MatchPhase.Play)
            {
                book.FirstDrop = world.Time; // the referee drops the puck after this system runs
            }

            if (m.Phase == MatchPhase.Stopped && book.LastPhase != MatchPhase.Stopped)
            {
                book.StoppedAt = world.Time;
            }

            if (m.Phase == MatchPhase.Faceoff && book.LastPhase != MatchPhase.Faceoff)
            {
                AtFaceoff(world, book);
            }

            book.LastPhase = m.Phase;
            RunClocks(world, book);
            if (m.Phase == MatchPhase.Goal)
            {
                Goals(world, book);
            }

            PenaltyShot.Step(world, book, dt);
            HandlePlayState(world, book, m, dt);
        }

        private static bool ShouldEndOfRegulation(World world, MatchState m)
        {
            return m.Phase is MatchPhase.Shootout or MatchPhase.Final
                || (
                    m.Phase == MatchPhase.Overtime && !world.Setup.Rules.PenaltiesCarryIntoOvertime
                );
        }

        private static void HandleOvertime(World world, PenaltyBook book, MatchState m)
        {
            // the overtime break: the penalties carry on; a call still pending at the horn is made now
            if (book.Delayed.Count > 0)
            {
                PenaltyCalls.CallDelayed(world, book);
            }

            book.LastPhase = m.Phase;
        }

        private static void HandlePlayState(World world, PenaltyBook book, MatchState m, float dt)
        {
            if (m.Phase == MatchPhase.Play)
            {
                Script(world, book);
                if (world.Settings.Penalties)
                {
                    Watch(world, book, dt);
                }

                Dev.PenaltyShotDemo.Update(world);

                if (book.Delayed.Count > 0 && Touched(world, book).TryGetSide(out Side offender))
                {
                    _ = MatchFlow.Whistle(world, PenaltyCalls.FaceoffDot(world, offender));
                    PenaltyCalls.CallDelayed(world, book);
                }
            }
            else if (
                m.Phase == MatchPhase.Stopped
                && world.Settings.Penalties
                && world.Time - book.StoppedAt < PenaltyTuning.AfterWhistle
            )
            {
                LateHits(world, book);
            }

            if (m.Phase != MatchPhase.Play && book.Delayed.Count > 0)
            {
                // play stopped by itself (offside, icing, a frozen puck, the period's end): the call is made now
                if (m.Phase == MatchPhase.Stopped)
                {
                    MatchFlow.State(world).Dot = PenaltyCalls.FaceoffDot(
                        world,
                        book.Delayed[0].Side
                    );
                }

                PenaltyCalls.CallDelayed(world, book);
            }
        }

        /// <summary>The faceoff is set up: the first one of a period with an empty box reads the full strength
        /// off the ice (5 in regulation, 3 or 5 in overtime); with penalties carried into overtime it is
        /// the overtime format's skaters; then each club's skaters are brought to
        /// <see cref="PenaltyCalls.TargetSkaters"/>, one on from the bench or one off to it, and placed again.</summary>
        private static void AtFaceoff(World world, PenaltyBook book)
        {
            SetBaseSkaters(world, book);
            if (AdjustSkatersToTarget(world))
            {
                FaceoffFormation.Place(world, MatchFlow.FaceoffDot(world));
            }
        }

        private static void SetBaseSkaters(World world, PenaltyBook book)
        {
            MatchState m = world.Match;
            if (
                book.BasePeriod != m.Period
                && m.Penalties.Count == 0
                && !Lines.GoaliePull.IsPulled(world, Side.Home)
                && !Lines.GoaliePull.IsPulled(world, Side.Away)
            )
            {
                int most = BothSides.Max(side => world.OnIce(side).Count(p => !p.IsGoalie));
                book.BaseSkaters = Math.Clamp(
                    most,
                    PenaltyTuning.MinSkaters,
                    PenaltyTuning.MaxSkaters
                );
                book.BasePeriod = m.Period;
            }
            else if (
                book.BasePeriod != m.Period
                && m.Period > world.Setup.Periods
                && OvertimeFormat.Of(world.Setup).Skaters > 0
            )
            {
                book.BaseSkaters = Math.Clamp(
                    OvertimeFormat.Of(world.Setup).Skaters,
                    PenaltyTuning.MinSkaters,
                    PenaltyTuning.MaxSkaters
                );
                book.BasePeriod = m.Period;
            }
        }

        private static bool AdjustSkatersToTarget(World world)
        {
            bool changed = false;
            foreach (Side side in BothSides)
            {
                int target = PenaltyCalls.TargetSkaters(world, side);
                int on = world.OnIce(side).Count(p => !p.IsGoalie);
                changed |= BringSkatersOn(world, side, target, ref on);
                changed |= SendSkatersOff(world, side, target, ref on);
            }
            return changed;
        }

        private static bool BringSkatersOn(World world, Side side, int target, ref int on)
        {
            bool changed = false;
            for (; on < target; on++)
            {
                if (
                    !Lines
                        .Bench.SendOn(world, side, Lines.Group.Forwards, onTheFly: false)
                        .TryGet(out Player added)
                )
                {
                    break;
                }

                changed = true;
                PenaltyBook.Log(
                    world,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{PenaltyCalls.Name(world, added)} comes on, {target} skaters"
                    )
                );
            }
            return changed;
        }

        private static bool SendSkatersOff(World world, Side side, int target, ref int on)
        {
            bool changed = false;
            for (; on > target; on--)
            {
                if (!Lines.Bench.Surplus(world, side, target).TryGet(out Player off))
                {
                    break;
                }

                Lines.Bench.SendOff(world, off);
                changed = true;
                PenaltyBook.Log(
                    world,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{PenaltyCalls.Name(world, off)} goes off, {target} skaters"
                    )
                );
            }
            return changed;
        }

        /// <summary>Runs each penalty's clock by exactly what the game clock ran this step, and lets out whoever has served his time.</summary>
        private static void RunClocks(World world, PenaltyBook book)
        {
            MatchState m = world.Match;
            float ran =
                book.LastPeriod == m.Period && !float.IsNaN(book.LastClock)
                    ? book.LastClock - m.Clock
                    : 0;
            book.LastClock = m.Clock;
            book.LastPeriod = m.Period;
            if (ran <= 0)
            {
                return;
            }

            foreach (PenaltyState p in m.Penalties.ToList())
            {
                float left = p.SecondsLeft - ran;
                if (left <= 0.0001f)
                {
                    PenaltyCalls.End(world, book, p, "served");
                }
                else
                {
                    m.Penalties[m.Penalties.IndexOf(p)] = p with { SecondsLeft = left };
                }
            }
            foreach (PenaltyState p in book.Misconducts.ToList())
            {
                float left = p.SecondsLeft - ran;
                if (left <= 0.0001f)
                {
                    PenaltyCalls.EndMisconduct(world, book, p);
                }
                else
                {
                    book.Misconducts[book.Misconducts.IndexOf(p)] = p with { SecondsLeft = left };
                }
            }
        }

        /// <summary>A goal against the offending club waves its delayed calls off; a power play goal ends the
        /// shortest minor of the club scored on, or the running half of a double minor; a major
        /// is served in full.</summary>
        private static void Goals(World world, PenaltyBook book)
        {
            // MatchRules counts one goal per step
            if (world.Events.Of<GoalScored>().FirstOrDefault() is not { } goal)
            {
                return;
            }

            Side scorer = goal.Side;
            Side other = scorer == Side.Home ? Side.Away : Side.Home;
            if (book.Delayed.RemoveAll(d => d.Side == other) > 0)
            {
                PenaltyBook.Log(world, "delayed call waved off by the goal");
            }

            if (
                PenaltyCalls.Serving(world, scorer) < PenaltyCalls.Serving(world, other)
                && world
                    .Match.Penalties.Where(p =>
                        p.Side == other
                        && !PenaltyCalls.Substituted(world, p.PlayerId)
                        && Ends(world, p)
                    )
                    .MinBy(p => p.SecondsLeft)
                    is { } shortest
            )
            {
                float minor = PenaltyCalls.MinorSeconds(world);
                if (
                    PenaltyCalls.SeverityOf(world, shortest.PlayerId) == Severity.DoubleMinor
                    && shortest.SecondsLeft > minor + 0.0001f
                )
                {
                    world.Match.Penalties[world.Match.Penalties.IndexOf(shortest)] = shortest with
                    {
                        SecondsLeft = minor,
                    };
                    PenaltyBook.Log(
                        world,
                        $"power play goal ends the first half of {PenaltyCalls.Name(world, world.Players[shortest.PlayerId])}'s double minor"
                    );
                }
                else
                {
                    PenaltyCalls.End(world, book, shortest, "power play goal");
                }
            }
        }

        /// <summary>Gets whether a power play goal can end a penalty: minors and double minors, not majors.</summary>
        private static bool Ends(World world, PenaltyState p)
        {
            return PenaltyCalls.SeverityOf(world, p.PlayerId)
                is Severity.Minor
                    or Severity.DoubleMinor
                    or Severity.MinorAndMisconduct;
        }

        /// <summary>Judges every check this step and every close chase of the carrier.</summary>
        private static void Watch(World world, PenaltyBook book, float dt)
        {
            foreach (CheckLanded check in world.Events.Of<CheckLanded>().ToList())
            {
                if (
                    PenaltyModel
                        .Judge(world, check, Again(world, book, check))
                        .TryGet(out Foul foul)
                )
                {
                    Roll(world, book, foul, check.Strength);
                }
            }
            foreach (Foul foul in PenaltyModel.Chases(world, dt).ToList())
            {
                Roll(world, book, foul, 0);
            }

            foreach (Foul foul in PenaltyModel.Holds(world, dt).ToList())
            {
                Roll(world, book, foul, 0);
            }
        }

        /// <summary>Hits in the moments after the whistle can only be roughing.</summary>
        private static void LateHits(World world, PenaltyBook book)
        {
            foreach (CheckLanded check in world.Events.Of<CheckLanded>().ToList())
            {
                if (PenaltyModel.Judge(world, check, afterWhistle: true).TryGet(out Foul foul))
                {
                    Roll(world, book, foul, check.Strength, afterWhistle: true);
                }
            }
        }

        /// <summary>Notes the hit and gets whether the checker hit the same skater a moment ago (roughing).</summary>
        private static bool Again(World world, PenaltyBook book, CheckLanded check)
        {
            Player target = world
                .Players.Where(p => p.Side != check.Side && p.OnIce && !p.IsGoalie)
                .MinBy(p => System.Numerics.Vector3.DistanceSquared(p.Position, check.Position));
            if (target is not { })
            {
                return false;
            }

            bool again =
                book.LastHit.TryGetValue(check.CheckerId, out (int Target, float Time) last)
                && last.Target == target.Id
                && world.Time - last.Time < PenaltyTuning.RoughingWindow;
            book.LastHit[check.CheckerId] = (target.Id, world.Time);
            return again;
        }

        private static void Roll(
            World world,
            PenaltyBook book,
            Foul foul,
            float strength,
            bool afterWhistle = false
        )
        {
            if (
                book.Rng.NextSingle()
                >= foul.Chance
                    * world.Switches.PenaltyStrictness
                    * world.Setup.Rules.StrictnessScale
                    * Talk(world, foul.PlayerId)
            )
            {
                return;
            }

            Severity severity = PenaltyModel.Grade(
                world.Setup.Rules,
                foul.Infraction,
                strength,
                book.Rng.NextSingle()
            );
            // a foul from behind on a clean breakaway is a penalty shot, not a minor
            if (!afterWhistle && PenaltyShot.Award(world, foul.PlayerId, foul.Infraction, severity))
            {
                return;
            }

            _ = PenaltyCalls.Signal(world, foul.PlayerId, foul.Infraction, severity, afterWhistle);
        }

        /// <summary>The coach's pre-game talk on the offender's club: calm and discipline draw fewer calls, firing up more.</summary>
        private static float Talk(World world, int playerId)
        {
            return playerId >= 0 && playerId < world.Players.Count
                ? CoachTalk.PenaltyScale(world.Setup, world.Players[playerId].Side)
                : 1f;
        }

        /// <summary>Gets the offending club once it touches the puck: it carries it, or last played it.</summary>
        private static SideOrNone Touched(World world, PenaltyBook book)
        {
            Puck pk = world.Puck;
            int touch = pk.Carrier >= 0 ? pk.Carrier : pk.LastTouch;
            if (touch < 0 || touch >= world.Players.Count)
            {
                return SideOrNone.None;
            }

            Side side = world.Players[touch].Side;
            return book.Delayed.Exists(d => d.Side == side) ? side.OrNone() : SideOrNone.None;
        }

        /// <summary>The penalty scene's scripted call: a few seconds after the opening drop, once away has the puck, the home skater nearest it trips someone (or boards him).</summary>
        private static void Script(World world, PenaltyBook book)
        {
            if (
                float.IsNaN(book.ScriptAfterDrop)
                || float.IsNaN(book.FirstDrop)
                || world.Time - book.FirstDrop < book.ScriptAfterDrop
            )
            {
                return;
            }
            // wait for away to have the puck, so the call is delayed and the referee's arm shows
            Puck pk = world.Puck;
            if (pk.Carrier < 0 || world.Players[pk.Carrier].Side != Side.Away)
            {
                return;
            }

            book.ScriptAfterDrop = float.NaN;
            Player offender = world
                .OnIce(Side.Home)
                .Where(p => !p.IsGoalie && p.Id != world.Puck.Carrier)
                .MinBy(p =>
                    System.Numerics.Vector3.DistanceSquared(p.Position, world.Puck.Position)
                );
            if (offender is { } called)
            {
                _ = PenaltyCalls.Signal(
                    world,
                    called.Id,
                    book.ScriptCall.Infraction,
                    book.ScriptCall.Severity
                );
            }
        }

        /// <summary>Penalties end before the shootout and with the match, and with regulation unless they carry into overtime: skaters in the box go to the bench and delayed calls are dropped.</summary>
        private static void EndOfRegulation(World world, PenaltyBook book)
        {
            bool any = world.Match.Penalties.Count + book.Waiting.Count + book.Held.Count > 0;
            PenaltyCalls.ClearBox(world, book);
            if (any)
            {
                PenaltyBook.Log(
                    world,
                    world.Match.Phase == MatchPhase.Overtime
                        ? "penalties end with regulation"
                        : "penalties end"
                );
            }
        }

        public void DrawHud(World world, RenderContext render)
        {
            if (!art.TryGet(out HudArt hud) || !MatchFlow.IsRunning(world))
            {
                return;
            }

            MatchState m = world.Match;
            if (m.Phase is MatchPhase.Intro or MatchPhase.Shootout or MatchPhase.Final)
            {
                return;
            }

            const int h = 20,
                pad = 3,
                crest = 16,
                gap = 4;
            int y = HudSystem.PenaltyTop;
            int x = hud.BugPosition.X;
            DrawPenalties(world, hud, x, ref y, h, pad, crest, gap);
            DrawDelayed(world, hud, x, ref y, h, pad, crest, gap);
            DrawExtraAttackers(world, hud, x, ref y, h, pad, crest, gap);
        }

        private static void DrawPenalties(
            World world,
            HudArt hud,
            int x,
            ref int y,
            int h,
            int pad,
            int crest,
            int gap
        )
        {
            MatchState m = world.Match;
            foreach (Side side in BothSides)
            {
                foreach (
                    PenaltyState p in m
                        .Penalties.Where(p => p.Side == side)
                        .OrderBy(p => p.SecondsLeft)
                )
                {
                    string text = Text.Format(
                        PenaltyCalls.Ejected(world, p.PlayerId) ? K.PenaltyBoxGm : K.PenaltyBoxRow,
                        world.Players[p.PlayerId].Data.Number,
                        MatchFlow.ClockText(p.SecondsLeft)
                    );
                    Row(
                        hud,
                        world.Setup.Club(side).Id,
                        text,
                        "panel_red",
                        x,
                        y,
                        h,
                        pad,
                        crest,
                        gap
                    );
                    y += h + 1;
                }
                foreach (PenaltyState p in PenaltyCalls.Waiting(world).Where(p => p.Side == side))
                {
                    string text = Text.Format(
                        K.PenaltyBoxNext,
                        world.Players[p.PlayerId].Data.Number,
                        MatchFlow.ClockText(p.SecondsLeft)
                    );
                    Row(hud, world.Setup.Club(side).Id, text, "panel", x, y, h, pad, crest, gap);
                    y += h + 1;
                }
                foreach (
                    PenaltyState p in PenaltyCalls.Misconducts(world).Where(p => p.Side == side)
                )
                {
                    string text = Text.Format(
                        K.PenaltyBoxMisc,
                        world.Players[p.PlayerId].Data.Number,
                        MatchFlow.ClockText(p.SecondsLeft)
                    );
                    Row(hud, world.Setup.Club(side).Id, text, "panel", x, y, h, pad, crest, gap);
                    y += h + 1;
                }
            }
        }

        private static void DrawDelayed(
            World world,
            HudArt hud,
            int x,
            ref int y,
            int h,
            int pad,
            int crest,
            int gap
        )
        {
            foreach (DelayedPenalty d in PenaltyCalls.Delayed(world))
            {
                Row(
                    hud,
                    world.Setup.Club(d.Side).Id,
                    Text.Get(K.Delayed),
                    "panel",
                    x,
                    y,
                    h,
                    pad,
                    crest,
                    gap
                );
                y += h + 1;
            }
        }

        private static void DrawExtraAttackers(
            World world,
            HudArt hud,
            int x,
            ref int y,
            int h,
            int pad,
            int crest,
            int gap
        )
        {
            foreach (Side side in BothSides.Where(side => Lines.GoaliePull.IsPulled(world, side)))
            {
                Row(
                    hud,
                    world.Setup.Club(side).Id,
                    Text.Get(K.ExtraAttacker),
                    "panel_gold",
                    x,
                    y,
                    h,
                    pad,
                    crest,
                    gap
                );
                y += h + 1;
            }
        }

        private static void Row(
            HudArt art,
            string clubId,
            string text,
            string panel,
            int x,
            int y,
            int h,
            int pad,
            int crest,
            int gap
        )
        {
            int w = pad + crest + gap + art.Measure(text) + pad + 1;
            art.DrawPanel(panel, x, y, w, h);
            art.DrawCrest(clubId, x + pad, y + ((h - crest) / 2));
            // text y is 2 px above the 7 px capitals
            art.DrawText(text, x + pad + crest + gap, y + ((h - 7) / 2) - 2, art.Text);
        }

        public void Dispose()
        {
            if (art.TryGet(out HudArt hud))
            {
                hud.Unload();
            }

            art = Maybe.None<HudArt>();
        }
    }
}
