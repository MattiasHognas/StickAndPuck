using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players;
using StickAndPuck.App.Rules;
using System.Collections.Immutable;
using static Raylib_cs.Raylib;
using System.Globalization;

namespace StickAndPuck.App.Lines;

/// <summary>Energy per player, automatic or manual line changes, and the goalie swap after a burst of
/// goals. Runs only in a world with the match flow (<see cref="MatchFlow.Begin"/>); practice and
/// test scenes keep their players fresh.
/// <list type="bullet">
/// <item>The whole match squad is in the world from the start: the rest of each roster joins the
/// starters on the bench (<see cref="Player.OnIce"/> false), as four lines, three pairs and two
/// goalies (<see cref="Squad"/>).</item>
/// <item>Skaters tire on the ice in play, more when skating fast or sprinting, and rest on the bench
/// and during stoppages. Energy scales top speed, acceleration, stops and checks and shot
/// speed down; a spent skater skates at 70 % (see <see cref="SkatingModel.EnergyFactor"/>).</item>
/// <item>Forwards and defence change separately. In play a tired group changes on the fly: the
/// skaters head for the bench door and each one coming on steps out as one goes off; at a faceoff
/// the change is instant. With <see cref="LineChanges.Automatic"/> the bench calls changes by
/// itself; with <see cref="LineChanges.Manual"/> a human's club changes only on the change line
/// button, which works in both modes. AI clubs always change by themselves.</item>
/// <item>The goalie stays in unless he lets in <see cref="LinesTuning.GoalieGoals"/> in a short
/// spell; then the backup comes in before the next faceoff.</item>
/// <item>A club pulls its goalie for an extra attacker on a delayed call against the other
/// club, and when trailing late in regulation (AI by itself, a human on the button); he comes
/// back at the next whistle. See <see cref="GoaliePull"/>.</item>
/// <item>Each new period starts with line 1 and pair 1, everyone rested.</item>
/// </list>
/// Each change publishes one <see cref="LineChanged"/> (the first player on).
/// Positioning hands out roles again by itself whenever the skaters on the ice change.</summary>
internal sealed partial class LinesSystem : ISystem
{

    static readonly ImmutableArray<Side> BothSides = [Side.Home, Side.Away];
    static readonly ImmutableArray<Group> BothGroups = [Group.Forwards, Group.Defence];

    public void Start(World world)
    {
        if (MatchFlow.IsRunning(world)) SetUp(world);
    }

    public void Update(World world, float dt)
    {
        if (!MatchFlow.IsRunning(world)) return;
        var b = Bench.Board(world);
        if (!b.Squads[0].HasValue) SetUp(world);
        var m = world.Match;
        if (m.Phase == MatchPhase.Play) b.GameTime += dt;

        Tire(world, m.Phase == MatchPhase.Play, dt);
        CountGoals(world, b);
        GoaliePull.Update(world);
        Buttons(world, b);
        if (m.Phase == MatchPhase.Intermission && b.LastPhase != MatchPhase.Intermission) Rest(world, b);
        if (m.Phase == MatchPhase.Faceoff && b.LastPhase != MatchPhase.Faceoff) AtFaceoff(world, b);
        if (m.Phase == MatchPhase.Play)
        {
            foreach (var side in BothSides)
            {
                if (b.Changing[(int)side].TryGet(out var change)) Progress(world, b, change);
                else OnTheFly(world, b, side);
            }
        }
        b.LastPhase = m.Phase;
    }

    /// <summary>Puts both rosters in the world and builds the squads.</summary>
    internal static void SetUp(World world)
    {
        var b = Bench.Board(world);
        if (b.Squads[0].HasValue) return;
        foreach (var side in BothSides) b.Squads[(int)side] = Squad.Build(world, side);
        for (int i = 0; i < 2; i++) b.LastScore[i] = world.Match.Score[i];
    }

    static void Tire(World world, bool play, float dt)
    {
        float pace = world.Switches.FatiguePace;
        foreach (var p in world.Players)
        {
            if (p.IsGoalie) continue;
            float change;
            if (!p.OnIce)
            {
                change = LinesTuning.BenchRest;
            }
            else if (!play)
            {
                change = LinesTuning.IceRest;
            }
            else
            {
                float pace01 = Math.Min(p.Speed / SkatingModel.TopSpeed(p.Data.Traits), 1.2f);
                bool sprinting = p.Command.Sprint && p.Speed > 1f;
                change = -(LinesTuning.BaseDrain + LinesTuning.SkateDrain * pace01 + (sprinting ? LinesTuning.SprintDrain : 0f));
            }
            p.Energy = Math.Clamp(p.Energy + change * pace * dt, 0f, 1f);
        }
    }

    /// <summary>Notes each new goal against the club that let it in, and calls for the backup after a burst.</summary>
    static void CountGoals(World world, LinesBoard b)
    {
        float spell = LinesTuning.GoalieSpell * world.Setup.PeriodSeconds;
        foreach (var scorer in BothSides)
        {
            int i = (int)scorer, score = world.Match.Score[i];
            var conceding = Other(scorer);
            var against = b.GoalsAgainst[(int)conceding];
            for (int g = b.LastScore[i]; g < score; g++) against.Add(b.GameTime);
            b.LastScore[i] = score;
            against.RemoveAll(t => t < b.GameTime - spell);
            if (against.Count >= LinesTuning.GoalieGoals && Backup(b.Squads[(int)conceding]).HasValue)
            {
                b.GoalieSwap[(int)conceding] = true;
                against.Clear();
            }
        }
    }

    static void Buttons(World world, LinesBoard b)
    {
        foreach (var p in world.Players)
            if (p.Slot >= 0 && p.Command.ChangeLine) b.Requested[(int)p.Side] = true;

        foreach (var side in BothSides)
        {
            if (!b.Requested[(int)side]) continue;
            b.Requested[(int)side] = false;
            var group = BothGroups.MinBy(g => Bench.OnIceEnergy(world, side, g));
            if (world.Match.Phase == MatchPhase.Play)
            {
                if (!b.Changing[(int)side].HasValue && Best(b, side, group).TryGet(out var unit)) Begin(world, b, side, group, unit);
            }
            else if (!IcingRule.HoldsLines(world, side))   // a club that iced the puck keeps its skaters
            {
                b.Pending[(int)side, (int)group] = true;
            }
        }
    }

    /// <summary>Between periods everyone rests, and the next period starts with the first line and pair.</summary>
    static void Rest(World world, LinesBoard b)
    {
        foreach (var side in BothSides)
        {
            if (b.Changing[(int)side].TryGet(out var change)) Finish(world, b, change, onTheFly: false);
            b.FirstUnits[(int)side] = true;
        }
        foreach (var p in world.Players) p.Energy = 1f;
    }

    /// <summary>The faceoff is set up and the puck not yet dropped: finish changes, swap goalies, send out fresh units.</summary>
    static void AtFaceoff(World world, LinesBoard b)
    {
        foreach (var side in BothSides)
        {
            int s = (int)side;
            if (IcingRule.HoldsLines(world, side))
            {
                Hold(world, b, side);
                continue;
            }
            if (b.Changing[s].TryGet(out var change)) Finish(world, b, change, onTheFly: false);
            if (!b.Squads[s].TryGet(out var squad)) continue;   // never: the squads are built before any faceoff

            if (b.GoalieSwap[s])
            {
                b.GoalieSwap[s] = false;
                if (world.OnIce(side).FirstOrDefault(p => p.IsGoalie) is { } on && Backup(squad).TryGet(out var backup))
                {
                    Put(on, backup, onTheFly: false);
                    world.Events.Publish(new LineChanged(world.Time, side, backup.Id, backup.Position));
                    b.Last[s] = (string.Create(CultureInfo.InvariantCulture, $"G{backup.Data.Number}"), world.Time);
                    Log(world, $"{world.Setup.Club(side).ShortName} backup goalie {backup.Data.LastName} comes in");
                }
            }

            foreach (var group in BothGroups)
            {
                Maybe<Unit> unit;
                if (b.FirstUnits[s])
                {
                    unit = squad.Units(group).FirstOrNone();
                    if (unit.Is(static u => u.Members.All(p => p.OnIce))) unit = Maybe<Unit>.None;
                }
                else
                {
                    unit = Best(b, side, group);
                    float energy = Bench.OnIceEnergy(world, side, group);
                    bool want = b.Pending[s, (int)group]
                        || (Automatic(world, side) && energy < LinesTuning.FaceoffBelow && unit.Is(u => u.Energy >= energy + LinesTuning.FresherBy));
                    if (!want) unit = Maybe<Unit>.None;
                }
                b.Pending[s, (int)group] = false;
                if (unit.TryGet(out var next) && Begin(world, b, side, group, next).TryGet(out var instant)) Finish(world, b, instant, onTheFly: false);
            }
            b.FirstUnits[s] = false;
        }
    }

    /// <summary>No change after icing: the club that iced the puck faces off with the skaters it had on
    /// the ice at the whistle. A change on the fly stops where it is (who reached the bench stays
    /// there), asked-for changes are dropped and a goalie swap waits for the next faceoff.</summary>
    static void Hold(World world, LinesBoard b, Side side)
    {
        int s = (int)side;
        b.Changing[s] = Maybe<ShiftChange>.None;
        foreach (var group in BothGroups) b.Pending[s, (int)group] = false;
        Log(world, $"{world.Setup.Club(side).ShortName} iced the puck: no line change");
    }

    /// <summary>In play, a club whose forwards or defence are tired calls a change on the fly, if it is safe or they are spent.</summary>
    static void OnTheFly(World world, LinesBoard b, Side side)
    {
        if (!Automatic(world, side)) return;
        foreach (var group in BothGroups.OrderBy(g => Bench.OnIceEnergy(world, side, g)))
        {
            float energy = Bench.OnIceEnergy(world, side, group);
            if (energy >= LinesTuning.ChangeBelow) continue;
            if (energy >= LinesTuning.ForceBelow && !Safe(world, side)) continue;
            if (!Best(b, side, group).TryGet(out var unit) || unit.Energy < energy + LinesTuning.FresherBy) continue;
            Begin(world, b, side, group, unit);
            return;
        }
    }

    /// <summary>Gets the freshest unit of a group with nobody on the ice, lines 1 to 3 a little ahead of line 4.</summary>
    static Maybe<Unit> Best(LinesBoard b, Side side, Group group)
    {
        if (!b.Squads[(int)side].TryGet(out var squad)) return Maybe<Unit>.None;
        var bias = group == Group.Forwards ? LinesTuning.ForwardBias : LinesTuning.PairBias;
        return Maybe.From<Unit>(squad.Units(group)
            .Where(u => u.Members.All(p => !p.OnIce))
            .MaxBy(u => u.Energy - bias[Math.Min(u.Number - 1, bias.Length - 1)]));
    }

    static Maybe<Player> Backup(Maybe<Squad> squad)
    {
        return squad.TryGet(out var s) ? s.Goalies.FirstOrNone(g => !g.OnIce) : Maybe<Player>.None;
    }

    /// <summary>Gets whether the bench calls this club's changes by itself: always for the AI, for a human only in automatic mode.</summary>
    static bool Automatic(World world, Side side)
    {
        return world.Settings.Lines == LineChanges.Automatic || !world.Players.Exists(p => p.Side == side && p.Slot >= 0);
    }

    /// <summary>Gets whether a change on the fly is safe: the club has the puck, or the puck is out of its own zone.</summary>
    static bool Safe(World world, Side side)
    {
        var pk = world.Puck;
        if (pk.Carrier >= 0 && pk.Carrier < world.Players.Count && world.Players[pk.Carrier].Side == side) return true;
        int end = FaceoffFormation.DefendedEnd(world.Match, side);
        return pk.Position.X * end < RinkSpec.BlueLineX;
    }

    static Vector2 Flat(Player p)
    {
        return new Vector2(p.Position.X, p.Position.Z);
    }

    static Side Other(Side side)
    {
        return side == Side.Home ? Side.Away : Side.Home;
    }

    static void Log(World world, string text)
    {
        if (MatchFlow.State(world).Log) Console.WriteLine($"LINES: {text}");
    }

    /// <summary>Draws a small panel per club along the bottom: the unit on the ice for forwards and defence with an energy bar, and the goalie.</summary>
    public void DrawHud(World world, RenderContext render)
    {
        if (!world.Switches.LinesPanel || !MatchFlow.IsRunning(world)) return;
        var b = Bench.Board(world);
        var cream = new Color(0xef, 0xe0, 0xc2, 255);
        foreach (var side in BothSides)
        {
            int x = side == Side.Home ? 6 : Game.Width - 150, y = Game.Height - 14;
            DrawRectangle(x - 3, y - 3, 147, 14, new Color(0x2b, 0x21, 0x37, 220));
            DrawText(world.Setup.Club(side).ShortName, x, y, 10, cream);
            int cx = x + 28;
            foreach (var group in BothGroups)
            {
                string name = Bench.OnIceUnit(world, side, group).Select(static u => u.Name).Or("-");
                float energy = Bench.OnIceEnergy(world, side, group);
                DrawText(name, cx, y, 10, cream);
                DrawRectangle(cx + 14, y + 2, 30, 5, new Color(0x4a, 0x3a, 0x52, 255));
                Color bar;
                if (energy > LinesTuning.FaceoffBelow)
                    bar = new Color(0x7a, 0xa8, 0x5c, 255);
                else if (energy > LinesTuning.ChangeBelow)
                    bar = new Color(0xe0, 0xa8, 0x48, 255);
                else
                    bar = new Color(0xc8, 0x57, 0x4a, 255);
                DrawRectangle(cx + 14, y + 2, (int)MathF.Round(30 * energy), 5, bar);
                cx += 48;
            }
            if (world.OnIce(side).FirstOrDefault(p => p.IsGoalie) is { } g) DrawText(string.Create(CultureInfo.InvariantCulture, $"G{g.Data.Number}"), cx, y, 10, cream);
            var (what, at) = b.Last[(int)side];
            if (world.Time - at < 2.5f) DrawText($"CHANGE {what}", x, y - 14, 10, cream);
        }
    }
}
