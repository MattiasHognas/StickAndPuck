using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players.Checks;

namespace StickAndPuck.App.Rules.Penalties
{
    /// <summary>The judgement, pure so tests can ask it directly. A check (the Check event, Side and PlayerId
    /// the checker, Position the target) is judged by what it did: a knockdown of a skater without the
    /// puck or from behind is tripping; a hit from behind that leaves the carrier standing is hooking; a
    /// hard hit away from the puck is slashing; any other check has a small chance of a slash. A
    /// defender trailing close behind a fast carrier can be called for hooking a little every second.
    /// A skater with a high checking trait hits cleaner: the chance runs from 125 % at 0 to 75 % at 100.
    /// Adds, each only while its switch in <see cref="MatchSetup.Rules"/> is on and judged before
    /// tripping, hooking and slashing so a switched-off one leaves the hit to them: boarding (a hard hit from behind near
    /// the boards), checking from behind (the same in open ice, with Full severity only), roughing (the
    /// same skater hit again at once, or any hit after the whistle), cross-checking (a hard hit from the
    /// front on the carrier), interference (a hit on a skater far from the puck) and high-sticking (a
    /// face-to-face check otherwise clean). Holding is a chase like hooking: a defender hanging on
    /// behind a slowed carrier. <see cref="Grade"/> says what each call costs.</summary>
    public static class PenaltyModel
    {
        /// <summary>Judges one check event.</summary>
        public static Maybe<Foul> Judge(
            World world,
            CheckLanded check,
            bool again = false,
            bool afterWhistle = false
        )
        {
            Player checker = world.Players[check.CheckerId];
            Player target = world
                .Players.Where(p => p.Side != check.Side && p.OnIce && !p.IsGoalie)
                .MinBy(p => Vector3.DistanceSquared(p.Position, check.Position));
            if (target is not { } || Vector3.Distance(target.Position, check.Position) > 1f)
            {
                return Maybe.None<Foul>();
            }

            Puck pk = world.Puck;
            bool downed = target.Action == PlayerAction.Fallen && target.ActionTime < 0.1f;
            bool carrying =
                pk.Carrier == target.Id || (downed && pk.Carrier < 0 && pk.LastTouch == target.Id);
            bool behind = FromBehind(checker.Position, target);
            if (
                Worse(
                        world.Setup.Rules,
                        world.Puck,
                        check,
                        target,
                        carrying,
                        behind,
                        again,
                        afterWhistle
                    )
                    .TryGet(out (string Infraction, float Chance) worse)
            )
            {
                return new Foul(checker.Id, worse.Infraction, worse.Chance * Discipline(checker));
            }

            if (afterWhistle)
            {
                return Maybe.None<Foul>();
            }

            (string infraction, float chance) = ResolveInfraction(
                world.Setup.Rules,
                check,
                target,
                checker,
                carrying,
                behind,
                downed
            );
            return new Foul(checker.Id, infraction, chance * Discipline(checker));
        }

        private static (string Infraction, float Chance) ResolveInfraction(
            RuleSet rules,
            CheckLanded check,
            Player target,
            Player checker,
            bool carrying,
            bool behind,
            bool downed
        )
        {
            if (downed && (behind || !carrying))
            {
                return (Infraction.Tripping, PenaltyTuning.TripChance);
            }

            if (!downed && carrying && behind)
            {
                return (Infraction.Hooking, PenaltyTuning.HookCheckChance);
            }

            if (check.Strength >= CheckingModel.HardHit && !carrying)
            {
                return (Infraction.Slashing, PenaltyTuning.SlashChance);
            }

            if (rules.HighSticking && FaceToFace(checker.Position, target))
            {
                return (Infraction.HighSticking, PenaltyTuning.HighStickChance);
            }

            return (Infraction.Slashing, PenaltyTuning.StickChance);
        }

        /// <summary>Calls on a check that come before tripping, hooking and slashing, each only with its switch on.</summary>
        private static Maybe<(string Infraction, float Chance)> Worse(
            RuleSet rules,
            Puck pk,
            CheckLanded check,
            Player target,
            bool carrying,
            bool behind,
            bool again,
            bool afterWhistle
        )
        {
            bool hard = check.Strength >= CheckingModel.HardHit;
            bool boards = BoardsDistance(target.Position) < PenaltyTuning.BoardsReach;
            if (afterWhistle)
            {
                return rules.Roughing
                    ? (Infraction.Roughing, PenaltyTuning.LateHitChance)
                    : Maybe.None<(string Infraction, float Chance)>();
            }

            if (rules.Boarding && hard && behind && boards)
            {
                return (Infraction.Boarding, PenaltyTuning.BoardingChance);
            }

            if (rules.PenaltySeverity == PenaltySeverity.Full && hard && behind && !boards)
            {
                return (Infraction.CheckingFromBehind, PenaltyTuning.FromBehindChance);
            }

            if (rules.Roughing && again)
            {
                return (Infraction.Roughing, PenaltyTuning.RoughingChance);
            }

            if (rules.CrossChecking && hard && carrying && !behind)
            {
                return (Infraction.CrossChecking, PenaltyTuning.CrossCheckChance);
            }

            bool farFromPuck =
                pk.LastTouch != target.Id
                && Vector3.Distance(pk.Position with { Y = 0 }, target.Position with { Y = 0 })
                    > PenaltyTuning.InterferenceReach;
            return rules.Interference && !hard && !carrying && farFromPuck
                ? (Maybe<(string Infraction, float Chance)>)
                    (Infraction.Interference, PenaltyTuning.InterferenceChance)
                : Maybe.None<(string Infraction, float Chance)>();
        }

        /// <summary>Gets what a call costs. With <see cref="PenaltySeverity.MinorsOnly"/> every call is a
        /// minor. Otherwise boarding and cross-checking are a major with a game misconduct from
        /// <see cref="PenaltyTuning.MajorHit"/> up, checking from behind is that or a minor and
        /// misconduct, a high stick that draws blood is a double minor, and roughing now and then
        /// earns a misconduct on top. The rest are minors.</summary>
        public static Severity Grade(RuleSet rules, string infraction, float strength, float roll)
        {
            if (rules.PenaltySeverity == PenaltySeverity.MinorsOnly)
            {
                return Severity.Minor;
            }

            bool major = strength >= PenaltyTuning.MajorHit;
            return infraction switch
            {
                Infraction.Boarding or Infraction.CrossChecking => major
                    ? Severity.MajorAndGameMisconduct
                    : Severity.Minor,
                Infraction.CheckingFromBehind => major
                    ? Severity.MajorAndGameMisconduct
                    : Severity.MinorAndMisconduct,
                Infraction.HighSticking => roll < PenaltyTuning.DoubleMinorChance
                    ? Severity.DoubleMinor
                    : Severity.Minor,
                Infraction.Roughing => roll < PenaltyTuning.RoughingMisconductChance
                    ? Severity.MinorAndMisconduct
                    : Severity.Minor,
                _ => Severity.Minor,
            };
        }

        /// <summary>Finds defenders hanging on behind a slowed carrier, holding him.</summary>
        public static IEnumerable<Foul> Holds(World world, float dt)
        {
            Puck pk = world.Puck;
            if (!world.Setup.Rules.Holding || pk.Carrier < 0 || pk.Carrier >= world.Players.Count)
            {
                yield break;
            }

            Player carrier = world.Players[pk.Carrier];
            if (carrier.IsGoalie || carrier.Speed is < 0.5f or > PenaltyTuning.HoldMaxSpeed)
            {
                yield break;
            }

            foreach (Player d in world.Players)
            {
                if (
                    d.Side == carrier.Side
                    || !d.OnIce
                    || d.IsGoalie
                    || d.Action != PlayerAction.None
                )
                {
                    continue;
                }

                float dist = Vector3.Distance(
                    d.Position with
                    {
                        Y = 0,
                    },
                    carrier.Position with
                    {
                        Y = 0,
                    }
                );
                if (dist > PenaltyTuning.HoldReach || !FromBehind(d.Position, carrier))
                {
                    continue;
                }

                yield return new Foul(
                    d.Id,
                    Infraction.Holding,
                    PenaltyTuning.HoldRate * dt * Discipline(d)
                );
            }
        }

        /// <summary>Gets whether a hit from <paramref name="from"/> lands on the target's front, face to face.</summary>
        public static bool FaceToFace(Vector3 from, Player target)
        {
            Vector2 push = new(target.Position.X - from.X, target.Position.Z - from.Z);
            if (push.LengthSquared() < 1e-6f)
            {
                return false;
            }

            Vector2 facing = new(SimMath.Sin(target.Heading), SimMath.Cos(target.Heading));
            return Vector2.Dot(Vector2.Normalize(push), facing) < -0.5f;
        }

        /// <summary>Gets how far a spot is from the boards, rounded corners included.</summary>
        public static float BoardsDistance(Vector3 at)
        {
            float ax = MathF.Abs(at.X),
                az = MathF.Abs(at.Z);
            float cx = RinkSpec.HalfLength - RinkSpec.CornerRadius,
                cz = RinkSpec.HalfWidth - RinkSpec.CornerRadius;
            return ax > cx && az > cz
                ? RinkSpec.CornerRadius - Vector2.Distance(new Vector2(ax, az), new Vector2(cx, cz))
                : Math.Min(RinkSpec.HalfLength - ax, RinkSpec.HalfWidth - az);
        }

        /// <summary>Finds defenders trailing the carrier close enough to hook him this step.</summary>
        public static IEnumerable<Foul> Chases(World world, float dt)
        {
            Puck pk = world.Puck;
            if (pk.Carrier < 0 || pk.Carrier >= world.Players.Count)
            {
                yield break;
            }

            Player carrier = world.Players[pk.Carrier];
            if (carrier.IsGoalie || carrier.Speed < PenaltyTuning.HookMinSpeed)
            {
                yield break;
            }

            Vector3 ahead = Vector3.Normalize(carrier.Velocity with { Y = 0 });
            foreach (Player d in world.Players)
            {
                if (
                    d.Side == carrier.Side
                    || !d.OnIce
                    || d.IsGoalie
                    || d.Action != PlayerAction.None
                )
                {
                    continue;
                }

                Vector3 off = (d.Position - carrier.Position) with { Y = 0 };
                float dist = off.Length();
                if (dist is > PenaltyTuning.HookReach or < 0.01f)
                {
                    continue;
                }

                if (Vector3.Dot(off / dist, ahead) > -0.4f)
                {
                    continue;
                }

                yield return new Foul(
                    d.Id,
                    Infraction.Hooking,
                    PenaltyTuning.HookRate * dt * Discipline(d)
                );
            }
        }

        /// <summary>Gets whether a hit from <paramref name="from"/> lands on the target's back.</summary>
        public static bool FromBehind(Vector3 from, Player target)
        {
            Vector2 push = new(target.Position.X - from.X, target.Position.Z - from.Z);
            if (push.LengthSquared() < 1e-6f)
            {
                return false;
            }
            // heading 0 faces +z; the push of a hit from behind goes the way the target faces
            Vector2 facing = new(SimMath.Sin(target.Heading), SimMath.Cos(target.Heading));
            return Vector2.Dot(Vector2.Normalize(push), facing)
                > SimMath.Cos(PenaltyTuning.BehindDegrees * MathF.PI / 180f);
        }

        /// <summary>Gets how much a skater's checking trait scales his chance of a foul: 1.25 at 0 down to 0.75 at 100.</summary>
        public static float Discipline(Player p)
        {
            return 1.25f - (0.5f * Traits.Unit(p.Data.Traits.Checking));
        }
    }
}
