using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Penalties;

namespace StickAndPuck.App.Rules.Match
{
    /// <summary>The draw at a faceoff. Each human controls their club's centre. When the puck drops a window of
    /// <see cref="MatchTuning.DrawWindow"/> opens and the first centre to press shoot or pass in it wins the draw.
    /// A human press before the drop is a false start and hands the draw to the other club. An AI centre presses
    /// after a delay set by its hockey IQ, with a little luck. If nobody presses in the window, puck control
    /// and hockey IQ decide it on a roll. Until the draw is won the puck lies dead on the dot and the
    /// phase stays <see cref="MatchPhase.Faceoff"/>; then play starts and the puck slides back to the winner.
    /// Under <see cref="RuleSet.FaceoffViolations"/> moving early (a human press, or an AI centre jumping
    /// the drop now and then, more often with less hockey IQ) is a violation instead: the centre is waved out
    /// and a winger takes the draw (<see cref="FaceoffFormation.Place(World, Vector3, IReadOnlyCollection{int})"/>).
    /// A club's second violation in the same faceoff is a delay-of-game minor under
    /// <see cref="RuleSet.FaceoffViolationMinor"/>, with the faceoff moved to its own end-zone dot,
    /// and otherwise a false start.</summary>
    internal static class FaceoffDraw
    {
        /// <summary>Clears the draw for a new faceoff; a button already held as it begins is not a press.</summary>
        public static void Reset(World world, FlowState s)
        {
            DrawState d = s.Draw;
            d.Dropped = false;
            d.Since = 0;
            d.FalseStart = SideOrNone.None;
            d.Waited = 0;
            d.Violations[0] = d.Violations[1] = 0;
            d.Kicked.Clear();
            foreach (Side side in new[] { Side.Home, Side.Away })
            {
                d.JumpAt[(int)side] = RollJump(world, s, side);
            }

            for (int slot = 0; slot < d.Down.Length; slot++)
            {
                d.Down[slot] = world.Controlled(slot).Is(p => Pressing(p.Command));
            }
        }

        /// <summary>One step of a faceoff: humans onto their centres, presses read, and the draw won once the window decides it.</summary>
        public static void Step(World world, FlowState s, float dt)
        {
            ControlCentres(world, s.Dot);
            List<Side> pressed = Presses(world, s);
            DrawState d = s.Draw;
            if (!d.Dropped)
            {
                d.Waited += dt;
                if (world.Setup.Rules.FaceoffViolations)
                {
                    Violations(world, s, pressed);
                }
                else if (!d.FalseStart.IsSet() && pressed.Count > 0)
                {
                    d.FalseStart = Pick(world, s, pressed).OrNone();
                }

                return;
            }

            d.Since += dt;
            // a human press this step beats an AI reaching its delay this step: the press came first within it
            if (pressed.Count > 0)
            {
                Win(world, s, Pick(world, s, pressed));
                return;
            }
            List<Side> react =
            [
                .. new[] { Side.Home, Side.Away }.Where(side => d.Delay[(int)side] <= d.Since),
            ];
            if (react.Count > 0)
            {
                Win(world, s, react.MinBy(side => d.Delay[(int)side]));
                return;
            }
            if (d.Since >= MatchTuning.DrawWindow)
            {
                Win(world, s, Roll(world, s));
            }
        }

        /// <summary>The puck drops: the window opens and each AI centre rolls its reaction. After a false start the other club wins at once.</summary>
        public static void Drop(World world, FlowState s)
        {
            DrawState d = s.Draw;
            d.Dropped = true;
            d.Since = 0;
            foreach (Side side in new[] { Side.Home, Side.Away })
            {
                float luck = s.Dice.NextSingle();
                bool ai = Centre(world, s.Dot, side).Is(p => p.Slot < 0);
                d.Delay[(int)side] = ai
                    ? Reaction(
                        Centre(world, s.Dot, side).Select(p => p.Data.Traits.Iq).Or(0f),
                        luck
                    )
                    : float.PositiveInfinity;
            }
            if (d.FalseStart.TryGetSide(out Side early))
            {
                Win(world, s, Other(early));
            }
        }

        /// <summary>Gets how long after the drop an AI centre presses: quicker with more hockey IQ (0..1), luck (0..1) adding up to <see cref="MatchTuning.DrawJitter"/>.</summary>
        public static float Reaction(float iq, float luck)
        {
            return MatchTuning.DrawReaction
                + (MatchTuning.DrawSlowness * (1f - Math.Clamp(iq, 0f, 1f)))
                + (MatchTuning.DrawJitter * luck);
        }

        /// <summary>Gets the skater of a club nearest the dot: the one taking the draw.</summary>
        public static Maybe<Player> Centre(World world, Vector3 dot, Side side)
        {
            return
                world
                    .OnIce(side)
                    .Where(p => !p.IsGoalie)
                    .MinBy(p => Vector3.DistanceSquared(p.Position, dot))
                    is { } c
                ? c
                : Maybe.None<Player>();
        }

        /// <summary>Gets a value indicating whether a human takes this faceoff's draw, so the prompt shows.</summary>
        public static bool HumanCentre(World world, FlowState s)
        {
            return Centre(world, s.Dot, Side.Home).Is(p => p.Slot >= 0)
                || Centre(world, s.Dot, Side.Away).Is(p => p.Slot >= 0);
        }

        /// <summary>The clubs that moved early this step, a human press or an AI centre's jump, each committing a violation.</summary>
        private static void Violations(World world, FlowState s, List<Side> pressed)
        {
            DrawState d = s.Draw;
            foreach (Side side in new[] { Side.Home, Side.Away })
            {
                bool jumped =
                    d.JumpAt[(int)side] <= d.Waited
                    && Centre(world, s.Dot, side).Is(p => p.Slot < 0);
                if (!pressed.Contains(side) && !jumped)
                {
                    continue;
                }

                if (Violation(world, s, side))
                {
                    return;
                }
            }
        }

        /// <summary>A club's centre moved early: waved out for a winger, or on the club's second violation a minor or a false start.
        /// Returns true when the faceoff moved (the minor), so nothing more is read this step.</summary>
        private static bool Violation(World world, FlowState s, Side side)
        {
            DrawState d = s.Draw;
            d.JumpAt[(int)side] = float.PositiveInfinity;
            if (d.FalseStart.IsSet() || !Centre(world, s.Dot, side).TryGet(out Player centre))
            {
                return false;
            }

            d.Violations[(int)side]++;
            if (d.Violations[(int)side] < 2)
            {
                _ = d.Kicked.Add(centre.Id);
                // the drop keeps its time; with a referee he gets set again first (RefereeSystem)
                FaceoffFormation.Place(world, s.Dot, d.Kicked);
                Player taker = Centre(world, s.Dot, side).Or(centre);
                world.Events.Publish(
                    new FaceoffViolated(world.Time, side, centre.Id, s.Dot, taker.Id)
                );
                Log(world, s, $"faceoff violation by {Tag(centre)}, {Tag(taker)} takes the draw");
                // the new man at the dot may jump it too, on his own roll
                d.JumpAt[(int)side] = RollJump(world, s, side) + d.Waited;
                return false;
            }

            world.Events.Publish(new FaceoffViolated(world.Time, side, centre.Id, s.Dot, -1));
            if (world.Setup.Rules.FaceoffViolationMinor && world.Settings.Penalties)
            {
                Log(world, s, $"second faceoff violation by {Tag(centre)}: delay of game");
                PenaltyCalls.Give(world, centre.Id, Infraction.DelayOfGame, Severity.Minor);
                MatchFlow.Stop(world, PenaltyCalls.FaceoffDot(world, side));
                return true;
            }

            Log(
                world,
                s,
                $"second faceoff violation by {Tag(centre)}: the draw goes to the other club"
            );
            d.FalseStart = side.OrNone();
            return false;
        }

        private static string Tag(Player p)
        {
            return string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"#{p.Data.Number} {p.Data.LastName} ({p.Data.Position})"
            );
        }

        private static void Log(World world, FlowState s, string what)
        {
            if (s.Log)
            {
                Console.WriteLine(
                    string.Create(
                        System.Globalization.CultureInfo.InvariantCulture,
                        $"SAP: {what}, P{world.Match.Period} {MatchFlow.ClockText(world.Match.Clock)}"
                    )
                );
            }
        }

        /// <summary>Rolls whether and when a club's AI centre moves before the drop in this faceoff: rarer with more hockey IQ.
        /// Rolled for both clubs, AI or not, so the stream stays the same; only an AI centre acts on it.</summary>
        private static float RollJump(World world, FlowState s, Side side)
        {
            if (!world.Setup.Rules.FaceoffViolations)
            {
                return float.PositiveInfinity;
            }

            float roll = s.ViolationDice.NextSingle(),
                at = s.ViolationDice.NextSingle();
            float iq = Centre(world, s.Dot, side).Select(p => p.Data.Traits.Iq).Or(0.5f);
            return roll < MatchTuning.ViolationChance * (1.25f - Math.Clamp(iq, 0f, 1f))
                ? MatchTuning.JumpEarliest + (MatchTuning.JumpSpread * at)
                : float.PositiveInfinity;
        }

        private static bool Pressing(PlayerCommand c)
        {
            return c.Shoot || c.Pass;
        }

        /// <summary>Moves each human slot that controls a skater onto its club's centre.</summary>
        private static void ControlCentres(World world, Vector3 dot)
        {
            for (int slot = 0; slot < MatchSetup.Slots; slot++)
            {
                if (!world.Controlled(slot).TryGet(out Player me))
                {
                    continue;
                }

                if (
                    !Centre(world, dot, me.Side).TryGet(out Player centre)
                    || centre == me
                    || centre.Slot >= 0
                )
                {
                    continue;
                }

                PlayerCommand carried = me.Command;
                me.Slot = -1;
                me.Command = default;
                centre.Command = carried;
                centre.Slot = slot;
            }
        }

        /// <summary>Gets the clubs whose human centre pressed shoot or pass this step.</summary>
        private static List<Side> Presses(World world, FlowState s)
        {
            DrawState d = s.Draw;
            List<Side> sides = [];
            for (int slot = 0; slot < d.Down.Length; slot++)
            {
                bool was = d.Down[slot];
                Maybe<Player> controlled = world.Controlled(slot);
                bool down = controlled.Is(c => Pressing(c.Command));
                d.Down[slot] = down;
                if (!controlled.TryGet(out Player p))
                {
                    continue;
                }

                if (down && !was && Centre(world, s.Dot, p.Side) == p && !sides.Contains(p.Side))
                {
                    sides.Add(p.Side);
                }
            }
            return sides;
        }

        /// <summary>Gets the one club of those that pressed in the same step; two at once go to the roll.</summary>
        private static Side Pick(World world, FlowState s, List<Side> sides)
        {
            return sides.Count == 1 ? sides[0] : Roll(world, s);
        }

        /// <summary>The roll: the two centres contest it on puck control and hockey IQ with some luck.</summary>
        private static Side Roll(World world, FlowState s)
        {
            static float Skill(Maybe<Player> p)
            {
                return p.Select(c =>
                        0.5f + Traits.Unit(c.Data.Traits.PuckControl) + (0.5f * c.Data.Traits.Iq)
                    )
                    .Or(0f);
            }

            float h = Skill(Centre(world, s.Dot, Side.Home)),
                a = Skill(Centre(world, s.Dot, Side.Away));
            return s.Dice.NextSingle() * (h + a) < h ? Side.Home : Side.Away;
        }

        /// <summary>The draw is won: play starts and the puck is pulled back towards the winner's blade.</summary>
        private static void Win(World world, FlowState s, Side winner)
        {
            world.Match.Phase = MatchPhase.Play;
            Puck pk = world.Puck;
            pk.Position = s.Dot;
            pk.Velocity = Vector3.Zero;
            pk.Live = true;
            s.Draw.Winner = winner.OrNone();
            s.Draw.Dropped = false;
            if (!Centre(world, s.Dot, winner).TryGet(out Player centre))
            {
                return;
            }

            Vector3 pull = centre.Position + ((centre.Position - s.Dot) * 0.5f) - s.Dot;
            pull.Y = 0;
            if (pull.LengthSquared() > 1e-6f)
            {
                pk.Velocity = Vector3.Normalize(pull) * MatchTuning.DrawSpeed;
            }
        }

        private static Side Other(Side side)
        {
            return side == Side.Home ? Side.Away : Side.Home;
        }
    }
}
