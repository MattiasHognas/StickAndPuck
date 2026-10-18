using System.Globalization;
using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Lines;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Referee;
using StickAndPuck.App.Rules.Icing;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Rules.Offside;
using StickAndPuck.App.Rules.Overtime;
using StickAndPuck.App.Rules.Penalties;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Rules.Penalties
{
    /// <summary>
    /// The skate to the box: a skater called while play is stopped skates to his club's box door and in,
    /// on the ice and in the box's count on the way, while the faceoff waits for him; a skate that
    /// takes too long, play starting again under him, or the end of regulation puts him in at once; and
    /// a call outside a stoppage still puts him straight in the box.
    /// </summary>
    public sealed class BoxTripTests : CheckedTest
    {
        private const float Period = 600;

        private static World Make()
        {
            return new WorldBuilder()
                .Setup(s => s with { PeriodSeconds = Period })
                .Then(w => Run(w, x => x.Match.Phase == MatchPhase.Play, 15))
                .Build();
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
            World,
            ISystem[]
        > Sets = [];

        private static void Run(World world, Func<World, bool> until, float limit = 10)
        {
            _ = Run(world, until, limit, _ => { });
        }

        private static List<MatchEvent> Run(
            World world,
            Func<World, bool> until,
            float limit,
            Action<World> each
        )
        {
            ISystem[] systems = Sets.GetValue(
                world,
                _ =>
                    [
                        new LinesSystem(),
                        new PuckPhysics(),
                        new MatchRules(),
                        new OffsideRule(),
                        new IcingRule(),
                        new OvertimeRules(),
                        new PenaltyRules(),
                        new RefereeSystem(),
                    ]
            );
            List<MatchEvent> events = [];
            for (int i = 0; i < limit / Game.Step && !until(world); i++)
            {
                world.Events.Clear();
                each(world);
                foreach (ISystem s in systems)
                {
                    s.Update(world, Game.Step);
                }

                events.AddRange(world.Events.Current);
                world.Time += Game.Step;
                world.Tick++;
            }
            return events;
        }

        private static Player Skater(World w, Side side, PlayingPosition position)
        {
            return w.OnIce(side).First(p => p.Data.Position == position);
        }

        /// <summary>Calls a trip on a skater and makes it at once by giving his club the puck: the whistle goes.</summary>
        private static void Call(World w, Player p)
        {
            w.Puck.Carrier = Skater(w, Side.Away, PlayingPosition.C).Id;
            _ = PenaltyCalls.Signal(w, p.Id, Infraction.Tripping);
            w.Puck.Carrier = w.OnIce(p.Side).First(o => !o.IsGoalie && o != p).Id;
            Run(w, x => x.Match.Phase != MatchPhase.Play, 1);
            w.Puck.Carrier = -1;
        }

        [Test]
        public void SkatesIn()
        {
            World w = Make();
            Player lw = Skater(w, Side.Home, PlayingPosition.LW);
            lw.Position = new Vector3(-18, 0, -8);
            lw.Slot = 0;
            Call(w, lw);
            Check(
                lw.OnIce
                    && PenaltyCalls.OnTheWay(w, lw.Id)
                    && PenaltyCalls.InBox(w, lw.Id)
                    && lw.Slot < 0
                    && w.Match.Phase == MatchPhase.Stopped,
                "a skater called at a stoppage skates to the box: on the ice, in the box's count, a human handed on to a teammate"
            );
            Vector3 door = PenaltyCalls.BoxDoor(Side.Home);
            float from = Vector3.Distance(lw.Position, door);
            bool held = true,
                fast = false,
                faced = true;
            _ = Run(
                w,
                _ => !lw.OnIce,
                15,
                x =>
                {
                    if (!lw.OnIce)
                    {
                        return;
                    }

                    held &= x.Match.Phase == MatchPhase.Stopped;
                    fast |= lw.Velocity.Length() > 5f;
                    if (lw.Velocity.Length() > 1f)
                    {
                        faced &=
                            Vector3.Dot(
                                new Vector3(MathF.Sin(lw.Heading), 0, MathF.Cos(lw.Heading)),
                                Vector3.Normalize(lw.Velocity)
                            ) > 0.99f;
                    }
                }
            );
            Check(
                !lw.OnIce
                    && lw.Position.Z > RinkSpec.HalfWidth
                    && MathF.Abs(lw.Position.X - door.X) < 0.01f
                    && !PenaltyCalls.OnTheWay(w, lw.Id),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"he goes in through his club's door ({from:0.0} m away), off the ice"
                )
            );
            Check(
                held && fast && faced,
                "the faceoff waits for him all the way; he skates at a cruise, facing where he goes"
            );
            Run(w, x => x.Match.Phase == MatchPhase.Play, 5);
            Check(
                w.Match.Phase == MatchPhase.Play
                    && w.OnIce(Side.Home).Where(p => !p.IsGoalie).Take(5).Count() == 4,
                "then the faceoff, his club a skater short"
            );
        }

        [Test]
        public void TakesTooLong()
        {
            World w = Make();
            Player c = Skater(w, Side.Away, PlayingPosition.C);
            Call(w, c);
            Run(w, _ => false, 0.5f);
            Check(c.OnIce && PenaltyCalls.OnTheWay(w, c.Id), "on his way");
            w.Time += PenaltyTuning.TripTimeout;
            Run(w, _ => false, Game.Step * 1.5f);
            Check(
                !c.OnIce
                    && !PenaltyCalls.OnTheWay(w, c.Id)
                    && PenaltyCalls.InBox(w, c.Id)
                    && c.Position.Z > RinkSpec.HalfWidth,
                "a skate that takes too long puts him in the box at once"
            );
        }

        [Test]
        public void PlayStartsUnderHim()
        {
            World w = Make();
            Player d = Skater(w, Side.Home, PlayingPosition.D);
            Call(w, d);
            Check(d.OnIce && PenaltyCalls.OnTheWay(w, d.Id), "on his way");
            w.Match.Phase = MatchPhase.Intermission;
            Run(w, _ => false, Game.Step * 1.5f);
            Check(
                !d.OnIce && !PenaltyCalls.OnTheWay(w, d.Id) && PenaltyCalls.InBox(w, d.Id),
                "the period ending under him puts him in the box"
            );

            World w2 = Make();
            Player d2 = Skater(w2, Side.Home, PlayingPosition.D);
            Call(w2, d2);
            w2.Match.Phase = MatchPhase.Shootout;
            Run(w2, _ => false, Game.Step * 1.5f);
            Check(
                !d2.OnIce
                    && !PenaltyCalls.OnTheWay(w2, d2.Id)
                    && !PenaltyCalls.InBox(w2, d2.Id)
                    && w2.Match.Penalties.Count == 0
                    && PenaltyCalls.Book(w2).Trips.Count == 0,
                "the end of regulation empties the box, a skater on his way included: he goes to the bench"
            );
        }

        [Test]
        public void InPlayStraightIn()
        {
            World w = Make();
            Player rw = Skater(w, Side.Away, PlayingPosition.RW);
            PenaltyCalls.Give(w, rw.Id, Infraction.Roughing, Severity.Minor);
            Check(
                w.Match.Phase == MatchPhase.Play
                    && !rw.OnIce
                    && !PenaltyCalls.OnTheWay(w, rw.Id)
                    && PenaltyCalls.InBox(w, rw.Id),
                "a penalty given in play puts him straight in the box"
            );
        }
    }
}
