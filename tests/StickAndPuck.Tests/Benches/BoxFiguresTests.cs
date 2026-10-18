using System.Numerics;
using StickAndPuck.App.Benches;
using StickAndPuck.App.Core;
using StickAndPuck.App.Lines;
using StickAndPuck.App.Replay;
using StickAndPuck.App.Rules.Penalties;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Benches
{
    /// <summary>
    /// The penalty boxes behind the near boards: each skater serving sits side by side in his club's
    /// box, facing the ice and watching the puck; one who skates in appears at the door and walks to
    /// his seat; an ejected skater is not there, a released one leaves; and none of it touches the match.
    /// </summary>
    public sealed class BoxFiguresTests : CheckedTest
    {
        private static World Make()
        {
            World w = new WorldBuilder().Then(w => new LinesSystem().Start(w)).Build();
            w.Match.Phase = MatchPhase.Play;
            return w;
        }

        private static Player Skater(World w, Side side, PlayingPosition position)
        {
            return w.OnIce(side).First(p => p.Data.Position == position);
        }

        private static void Steps(World w, BenchCrowd crowd, float seconds)
        {
            for (int i = 0; i < seconds / Game.Step; i++)
            {
                crowd.Update(w, Game.Step);
                w.Time += Game.Step;
            }
        }

        [Test]
        public void ServingSkatersSitInTheirBox()
        {
            World w = Make();
            BenchCrowd crowd = new(1);
            Player lw = Skater(w, Side.Home, PlayingPosition.LW);
            Player rw = Skater(w, Side.Home, PlayingPosition.RW);
            Player d = Skater(w, Side.Away, PlayingPosition.D);
            foreach (Player p in new[] { lw, rw, d })
            {
                PenaltyCalls.Give(w, p.Id, Infraction.Roughing, Severity.Minor);
            }

            Steps(w, crowd, 3);
            List<BenchFigure> home = [.. crowd.InBoxes.Where(f => f.Player.Side == Side.Home)];
            List<BenchFigure> away = [.. crowd.InBoxes.Where(f => f.Player.Side == Side.Away)];
            (float from, float to) = BenchLayout.BoxSpan(Side.Home);
            Check(
                home.Count == 2
                    && away.Count == 1
                    && home.TrueForAll(f => f.InBox && f.X > from && f.X < to)
                    && MathF.Abs(home[0].X - home[1].X) > 0.6f,
                "two home skaters sit side by side in the home box, the away skater in his own"
            );
            Check(
                away[0].X > 0 && home.TrueForAll(f => f.X < 0),
                "home's box left of centre, away's right"
            );
            Check(
                crowd.InBoxes.All(f =>
                    f.Pose == BenchPose.Watch
                    && !f.Standing
                    && MathF.Abs(f.Heading - MathF.PI) <= 1.21f
                ),
                "all seated, facing the ice"
            );
            Check(
                crowd.InBoxes.All(f => !crowd.Of(f.Player).HasValue),
                "nobody in the box sits on a bench too"
            );

            w.Puck.Position = new Vector3(-25f, 0, 0);
            Steps(w, crowd, 2);
            bool left = crowd.InBoxes.All(f => f.Heading > MathF.PI + 0.3f);
            w.Puck.Position = new Vector3(25f, 0, 0);
            Steps(w, crowd, 2);
            Check(
                left && crowd.InBoxes.All(f => f.Heading < MathF.PI - 0.3f),
                "they turn after the puck, left end and right end"
            );
        }

        [Test]
        public void SkatingInWalksToTheSeat()
        {
            World w = Make();
            BenchCrowd crowd = new(1);
            Player c = Skater(w, Side.Away, PlayingPosition.C);
            Steps(w, crowd, 0.1f);
            PenaltyCalls.Give(w, c.Id, Infraction.Roughing, Severity.Minor);
            Steps(w, crowd, Game.Step);
            BenchFigure f = Must(crowd.InBoxes.FirstOrNone(x => x.Player == c));
            Check(
                MathF.Abs(f.X - BenchLayout.BoxDoorX(Side.Away)) < 0.1f
                    && f.Pose == BenchPose.Return
                    && f.Standing,
                "a skater coming off the ice appears standing at the box door"
            );
            Steps(w, crowd, 1.5f);
            Check(
                f.Pose == BenchPose.Watch
                    && MathF.Abs(f.X - BenchLayout.BoxSeatX(Side.Away, 0)) < 1e-4f
                    && f.TiredUntil > w.Time,
                "and walks to his seat and sits, tired for a while"
            );
        }

        [Test]
        public void EjectedAndReleasedLeave()
        {
            World w = Make();
            BenchCrowd crowd = new(1);
            Player lw = Skater(w, Side.Home, PlayingPosition.LW);
            Player d = Skater(w, Side.Home, PlayingPosition.D);
            PenaltyCalls.Give(w, lw.Id, Infraction.Roughing, Severity.Minor);
            PenaltyCalls.Give(w, d.Id, Infraction.Boarding, Severity.GameMisconduct);
            Steps(w, crowd, 1);
            Check(
                crowd.InBoxes.Any(f => f.Player == lw) && !crowd.InBoxes.Any(f => f.Player == d),
                "an ejected skater is not in the box"
            );
            PenaltyCalls.End(
                w,
                PenaltyCalls.Book(w),
                w.Match.Penalties.First(p => p.PlayerId == lw.Id),
                "served"
            );
            Steps(w, crowd, Game.Step);
            Check(
                lw.OnIce && !crowd.InBoxes.Any(f => f.Player == lw),
                "a skater let out of the box leaves it"
            );
        }

        [Test]
        public void NeverTouchesTheMatch()
        {
            World w = Make();
            BenchCrowd crowd = new(1);
            PenaltyCalls.Give(
                w,
                Skater(w, Side.Home, PlayingPosition.LW).Id,
                Infraction.Roughing,
                Severity.Minor
            );
            bool same = true;
            for (int i = 0; i < 120; i++)
            {
                ulong before = WorldChecksum.Of(w);
                crowd.Update(w, Game.Step);
                same &= before == WorldChecksum.Of(w);
                w.Time += Game.Step;
            }
            Check(
                same && crowd.InBoxes.Count == 1,
                "the world's checksum is the same before and after every step with a skater in the box"
            );
        }

        private static T Must<T>(Maybe<T> maybe)
            where T : notnull
        {
            return maybe.TryGet(out T value)
                ? value
                : throw new InvalidOperationException("expected a value");
        }
    }
}
