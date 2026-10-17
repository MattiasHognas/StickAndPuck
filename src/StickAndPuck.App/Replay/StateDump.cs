using System.Globalization;
using System.Numerics;
using System.Text;
using StickAndPuck.App.Core;
using StickAndPuck.App.AI;
using StickAndPuck.App.Rules;

namespace StickAndPuck.App.Replays;

/// <summary>The match state at one step as plain text, for scrubbing a replay (SAP_AT) and for comparing two runs by eye.</summary>
public static class StateDump
{
    /// <summary>Writes the state.</summary>
    public static string Of(World world)
    {
        var c = CultureInfo.InvariantCulture;
        var m = world.Match;
        var b = new StringBuilder();
        b.AppendLine(c, $"STATE tick {world.Tick} ({world.Time:0.000} s), checksum {WorldChecksum.Text(WorldChecksum.Of(world))}, seed {world.Seed}");
        b.AppendLine(c, $"STATE {world.Setup.Home.Id} {m.Score[0]}-{m.Score[1]} {world.Setup.Away.Id}, shots {m.Shots[0]}-{m.Shots[1]}, period {m.Period} {MatchFlow.ClockText(m.Clock)} ({m.Clock:0.000} s), {m.Phase}{(m.Paused ? ", paused" : "")}");
        foreach (var pen in m.Penalties)
            b.AppendLine(c, $"STATE penalty {pen.Side} #{world.Players[pen.PlayerId].Data.Number} {pen.Infraction} {MatchFlow.ClockText(pen.SecondsLeft)} left");
        var puck = world.Puck;
        b.AppendLine(c, $"STATE puck at {V(puck.Position)} moving {V(puck.Velocity)}, carrier {puck.Carrier}, last touch {puck.LastTouch}{(puck.Live ? "" : ", dead")}");
        foreach (var p in world.Players)
        {
            string who = $"{p.Side} #{p.Data.Number} {p.Data.Position} {p.Data.LastName}";
            string control = p.Slot >= 0 ? $", human {p.Slot}" : "";
            b.AppendLine(c, $"STATE player {p.Id} {who}{(p.OnIce ? "" : " (bench)")}{control}: at {V(p.Position)} moving {V(p.Velocity)} heading {p.Heading:0.000}, {p.Action} {p.ActionTime:0.00} s, energy {p.Energy:0.000}");
        }
        foreach (var e in world.Events.Current)
            b.AppendLine(c, $"STATE event {ReplayFile.EventText(e)}");
        return b.ToString().TrimEnd();
    }

    /// <summary>Writes exact floating-point values for diagnosing a cross-platform replay difference.</summary>
    public static string Exact(World world)
    {
        var c = CultureInfo.InvariantCulture;
        return string.Join('\n', world.Players.Select(p => string.Create(c,
            $"EXACT {p.Id} position {p.Position.X:R},{p.Position.Y:R},{p.Position.Z:R} velocity {p.Velocity.X:R},{p.Velocity.Y:R},{p.Velocity.Z:R} heading {p.Heading:R} action {p.ActionTime:R} energy {p.Energy:R} move {p.Command.Move.X:R},{p.Command.Move.Y:R} target {Positioning.TargetOf(world, p).X:R},{Positioning.TargetOf(world, p).Y:R} intent {AiCommand.IntentOf(world, p).Owner} {AiCommand.IntentOf(world, p).Reason}")))
            + string.Create(c, $"\nEXACT puck {world.Puck.Position.X:R},{world.Puck.Position.Y:R},{world.Puck.Position.Z:R} velocity {world.Puck.Velocity.X:R},{world.Puck.Velocity.Y:R},{world.Puck.Velocity.Z:R}");
    }

    static string V(Vector3 v) => string.Create(CultureInfo.InvariantCulture, $"({v.X:0.000}, {v.Y:0.000}, {v.Z:0.000})");
}
