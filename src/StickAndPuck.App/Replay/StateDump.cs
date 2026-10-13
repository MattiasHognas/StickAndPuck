using System.Globalization;
using System.Numerics;
using System.Text;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules;

namespace StickAndPuck.App.Replays;

/// <summary>W67: the match state at one step as plain text, for scrubbing a replay (SAP_AT) and for comparing two runs by eye.</summary>
public static class StateDump
{
    /// <summary>Writes the state.</summary>
    /// <param name="world">The match.</param>
    /// <returns>Several lines of text.</returns>
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

    static string V(Vector3 v) => string.Create(CultureInfo.InvariantCulture, $"({v.X:0.000}, {v.Y:0.000}, {v.Z:0.000})");
}
