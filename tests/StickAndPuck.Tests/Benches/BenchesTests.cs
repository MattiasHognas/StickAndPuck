using System.Globalization;
using System.Numerics;
using StickAndPuck.App.Benches;
using StickAndPuck.App.Core;
using StickAndPuck.App.Lines;
using StickAndPuck.App.Players;
using StickAndPuck.App.Replays;
using StickAndPuck.App.Rules;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests;

/// <summary>
/// The benches behind the far boards: everyone off the ice sits on his club's bench, spread evenly
/// out from the door in the order the units go on, the next unit stands and walks to the door for a change on the fly, the skaters coming off
/// walk back to their seats and sit tired, and none of it touches the match.
/// </summary>
public sealed class BenchesTests : CheckedTest
{
    static readonly Side[] BothSides = [Side.Home, Side.Away];

    static World Make() => new WorldBuilder().Then(w => new LinesSystem().Start(w)).Build();

    static T Must<T>(Maybe<T> maybe)
        where T : notnull => maybe.TryGet(out var value) ? value : throw new InvalidOperationException("expected a value");

    static Dictionary<int, float> Seats(World w, Side side) =>
        BenchLayout.Seats(Must(Bench.SquadOf(w, side)), w.Players.Where(p => p.Side == side && !p.IsGoalie), Bench.Board(w).PulledGoalie[(int)side], p => BenchLayout.OnBench(w, p));

    /// <summary>Runs lines, skating and the crowd step by step until <paramref name="until"/> or the time is up.</summary>
    static void Run(World w, BenchCrowd crowd, float seconds, Action<World> each, Func<World, bool> until)
    {
        var systems = new ISystem[] { new LinesSystem(), new SkatingSystem() };
        for (int i = 0; i < seconds / Game.Step && !until(w); i++)
        {
            w.Events.Clear();
            each(w);
            foreach (var s in systems) s.Update(w, Game.Step);
            crowd.Update(w, Game.Step);
            w.Time += Game.Step;
            w.Tick++;
        }
    }

    [Test]
    public void SeatsFollowTheLines()
    {
        var w = Make();
        foreach (var side in BothSides)
        {
            var squad = Must(Bench.SquadOf(w, side));
            var seats = Seats(w, side);
            var (from, to) = BenchLayout.Span(side);
            float door = BenchLayout.DoorX(side), way = BenchLayout.ForwardsWay(side);
            Check(w.Players.Where(p => p.Side == side).All(p => seats.ContainsKey(p.Id) == !p.OnIce), $"{side}: all {seats.Count} players off the ice have a seat, nobody on it");
            Check(seats.Values.All(x => x > from && x < to) && seats.Values.Distinct().Count() == seats.Count, $"{side}: every seat is on the club's own bench, one player each");
            var lines = squad.Lines.Skip(1).ToList();
            var pairs = squad.Pairs.Skip(1).ToList();
            Check(lines.SelectMany(u => u.Members).All(p => (seats[p.Id] - door) * way > 0), $"{side}: forwards sit toward centre ice");
            Check(pairs.SelectMany(u => u.Members).All(p => (seats[p.Id] - door) * way < 0), $"{side}: defence on the other side of the door");
            float Far(Unit u) => u.Members.Max(p => MathF.Abs(seats[p.Id] - door));
            float Near(Unit u) => u.Members.Min(p => MathF.Abs(seats[p.Id] - door));
            Check(lines.Zip(lines.Skip(1)).All(z => Far(z.First) < Near(z.Second))
                && pairs.Zip(pairs.Skip(1)).All(z => Far(z.First) < Near(z.Second)), $"{side}: with line 1 and pair 1 on, line 2 and pair 2 nearest the door, then in order");
            var backup = squad.Goalies[1];
            Check(seats.Values.Where(x => (x - door) * way < 0).All(x => MathF.Abs(x - door) <= MathF.Abs(seats[backup.Id] - door)) && (seats[backup.Id] - door) * way < 0,
                $"{side}: the backup goalie at the far end of the defence side");
        }
    }

    [Test]
    public void SeatsSpreadEvenlyClearOfTheDoor()
    {
        var w = Make();
        foreach (var side in BothSides)
        {
            var seats = Seats(w, side);
            float door = BenchLayout.DoorX(side);
            foreach (float way in (ReadOnlySpan<float>)[1f, -1f])
            {
                var row = seats.Values.Where(x => (x - door) * way > 0).Select(x => MathF.Abs(x - door)).Order().ToList();
                var gaps = row.Zip(row.Skip(1), (a, b) => b - a).ToList();
                Check(row.Count > 2 && MathF.Abs(row[0] - BenchLayout.DoorGap) < 1e-4f, string.Create(CultureInfo.InvariantCulture, $"{side} {way}: the first of {row.Count} seats is {row[0]:0.00} m from the door, none in the door gap"));
                Check(gaps.Max() - gaps.Min() < 1e-4f && gaps.Max() <= BenchLayout.MaxPitch + 1e-4f, $"{side} {way}: evenly spread, {gaps.Min():0.00} to {gaps.Max():0.00} m apart");
            }
        }

        // a line going on closes the row up
        var squad = Must(Bench.SquadOf(w, Side.Home));
        int before = Seats(w, Side.Home).Count;
        foreach (var p in squad.Lines[1].Members) p.OnIce = true;
        var after = Seats(w, Side.Home);
        Check(after.Count == before - 3 && squad.Lines[2].Members.All(p => after.ContainsKey(p.Id)), $"with line 2 on too, {after.Count} seats left and line 3 takes the door end");
    }

    [Test]
    public void PulledGoalieSitsByTheDoor()
    {
        var w = Make();
        w.Match.Phase = MatchPhase.Play;
        w.Puck.Live = true;
        var goalie = w.OnIce(Side.Home).First(p => p.IsGoalie);
        var crowd = new BenchCrowd(1);
        crowd.Update(w, Game.Step);
        Check(GoaliePull.Pull(w, Side.Home), "the home goalie is pulled");
        var seats = Seats(w, Side.Home);
        float door = BenchLayout.DoorX(Side.Home);
        Check(seats.Values.All(x => MathF.Abs(x - door) >= MathF.Abs(seats[goalie.Id] - door)), $"he sits nearest the door ({seats[goalie.Id] - door:0.00} m from it)");
        crowd.Update(w, Game.Step);
        Check(crowd.Of(goalie).Is(f => f.Pose == BenchPose.Return), "and walks there from the door");
    }

    [Test]
    public void EveryoneOffTheIceSits()
    {
        var w = Make();
        var crowd = new BenchCrowd(1);
        crowd.Update(w, Game.Step);
        foreach (var side in BothSides)
        {
            var seats = Seats(w, side);
            var mine = crowd.Shown.Where(f => f.Player.Side == side).ToList();
            Check(mine.Count == w.Players.Count(p => p.Side == side && !p.OnIce), $"{side}: {mine.Count} figures, one per player off the ice");
            Check(mine.TrueForAll(f => f.Pose == BenchPose.Watch && MathF.Abs(f.X - seats[f.Player.Id]) < 1e-4f), $"{side}: all seated in their own seats from the opening faceoff");
        }
        Check(crowd.Shown.All(f => !f.Player.OnIce), "nobody on the ice is on a bench");
    }

    [Test]
    public void SeatedFiguresWatchThePuck()
    {
        var w = Make();
        var crowd = new BenchCrowd(1);
        w.Puck.Position = new Vector3(-25f, 0, 0);
        for (int i = 0; i < 120; i++) crowd.Update(w, Game.Step);
        Check(crowd.Shown.Where(f => !f.Player.IsGoalie).All(f => f.Heading < -0.3f), "with the puck at the left end every skater on the benches turns left");
        w.Puck.Position = new Vector3(25f, 0, 0);
        for (int i = 0; i < 120; i++) crowd.Update(w, Game.Step);
        Check(crowd.Shown.Where(f => !f.Player.IsGoalie).All(f => f.Heading > 0.3f), "and right when it goes to the right end");
        int rates = crowd.Shown.Select(f => f.TurnRate).Distinct().Count();
        Check(rates > 10, $"each turns at his own pace ({rates} different rates)");
    }

    [Test]
    public void ChangeOnTheFlyStandsWalksAndSits()
    {
        var w = Make();
        w.Match.Phase = MatchPhase.Play;
        w.Puck.Live = true;
        var squad = Must(Bench.SquadOf(w, Side.Home));
        var leaving = squad.Lines[0].Members.ToList();
        var crowd = new BenchCrowd(1);
        crowd.Update(w, Game.Step);
        var start = crowd.Shown.ToDictionary(f => f.Player.Id, f => f.X);
        void Tired(World world)
        {
            foreach (var p in leaving) p.Energy = Math.Min(p.Energy, 0.2f);
            world.Puck.Position = new Vector3(20f * -FaceoffFormation.DefendedEnd(world.Match, Side.Home), 0, 0);
            world.Puck.Carrier = -1;
        }
        Run(w, crowd, 3, Tired, world => Bench.IsChanging(world, Side.Home));
        Run(w, crowd, 0.5f, Tired, _ => false);
        var ready = crowd.Shown.Where(f => f.Pose == BenchPose.Ready).ToList();
        float door = BenchLayout.DoorX(Side.Home);
        Check(ready.Count == 3 && ready.TrueForAll(f => f.Standing && MathF.Abs(f.X - door) < MathF.Abs(start[f.Player.Id] - door)),
            $"the next line stands and walks toward the door ({ready.Count} up)");
        var coming = ready.Select(f => f.Player).ToList();
        bool hiddenWhenOn = true;
        Run(w, crowd, 15, world =>
        {
            Tired(world);
            hiddenWhenOn &= coming.TrueForAll(p => !p.OnIce || !crowd.Of(p).HasValue);
        }, _ => leaving.TrueForAll(p => !p.OnIce));
        Check(leaving.TrueForAll(p => !p.OnIce) && coming.TrueForAll(p => p.OnIce), "the lines swap at the door");
        Check(hiddenWhenOn, "each newcomer's bench figure goes as he steps on the ice");
        Check(leaving.Exists(p => crowd.Of(p).Is(f => f.Pose == BenchPose.Return)), "the skaters coming off walk back along the bench");
        Run(w, crowd, 3, Tired, _ => false);
        var seats = Seats(w, Side.Home);
        Check(leaving.TrueForAll(p => crowd.Of(p).Is(f => f.Pose == BenchPose.Watch && MathF.Abs(f.X - seats[p.Id]) < 1e-4f)), "and sit down in their unit's seats");
        Check(leaving.TrueForAll(p => crowd.Of(p).Is(f => f.TiredUntil > w.Time)), "tired for a while");
    }

    [Test]
    public void TheCrowdNeverTouchesTheMatch()
    {
        var w = Make();
        w.Match.Phase = MatchPhase.Play;
        w.Puck.Live = true;
        var crowd = new BenchCrowd(1);
        bool same = true;
        Run(w, crowd, 5, world =>
        {
            ulong before = WorldChecksum.Of(world);
            crowd.Update(world, Game.Step);
            same &= before == WorldChecksum.Of(world);
            foreach (var p in world.Players.Where(p => p.OnIce && !p.IsGoalie)) p.Energy = Math.Min(p.Energy, 0.3f);   // keep changes coming
        }, _ => false);
        Check(same, "the world's checksum is the same before and after every crowd step");
    }
}
