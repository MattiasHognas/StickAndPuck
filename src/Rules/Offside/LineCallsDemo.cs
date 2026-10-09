using System.Numerics;
using StickAndPuck.Camera;
using StickAndPuck.Core;

namespace StickAndPuck.Rules;

/// <summary>
/// SAP_SCENE=offsidedemo: the match flow with a scripted play after each faceoff, in turn a pass
/// to a winger standing offside, a clean carry-in, an icing dump from the home end, and a dump the
/// home winger wins the race for (icing waved off). The score and clock line and the call labels are on.
/// </summary>
public sealed class LineCallsDemoScene : IScene
{
    /// <inheritdoc/>
    public string Name => "offsidedemo";

    /// <inheritdoc/>
    public World Create(IReadOnlyList<ClubData> clubs, Settings settings)
    {
        var world = MatchScenes.Exhibition(clubs, settings, 120);
        LineCallsDemo.Enabled = true;
        MatchRules.ShowLine = true;
        return world;
    }
}

/// <summary>The scripted plays and camera behind <see cref="LineCallsDemoScene"/>.</summary>
internal static class LineCallsDemo
{
    static float playFrom = float.NegativeInfinity;
    static int play = -1;
    static bool sent;
    static float cameraX;

    /// <summary>Gets or sets a value indicating whether the script runs.</summary>
    public static bool Enabled { get; set; }

    /// <summary>Sets up the next play when the puck drops, sends the puck a second later, and pans the camera to the puck.</summary>
    /// <param name="world">The match state.</param>
    public static void Update(World world)
    {
        if (!Enabled) return;
        Pan(world);
        if (world.Match.Phase != MatchPhase.Play)
        {
            playFrom = float.NegativeInfinity;
            return;
        }
        if (float.IsNegativeInfinity(playFrom))
        {
            playFrom = world.Time;
            play = (play + 1) % 4;
            sent = false;
            Stage(world);
        }
        float since = world.Time - playFrom;
        if (!sent && since > 1f)
        {
            sent = true;
            Send(world);
        }
        if (play == 1 && sent) Carry(world);
        // a play the rules let stand ends after a few seconds with a frozen-puck style whistle at centre
        if (since > 6f) MatchFlow.Whistle(world, RinkSpec.CentreDot);
    }

    /// <summary>Prints a call to the console while the demo runs.</summary>
    /// <param name="e">The Offside or Icing event.</param>
    /// <param name="dot">The faceoff dot it leads to.</param>
    public static void Log(MatchEvent e, Vector3 dot)
    {
        if (Enabled) Console.WriteLine($"SAP: {e.Kind} by {e.Side} at ({e.Position.X:0.0}, {e.Position.Z:0.0}), faceoff at ({dot.X}, {dot.Z}), play {play}");
    }

    static int Attacks(World world)
    {
        return -FaceoffFormation.DefendedEnd(world.Match, Side.Home);
    }

    static Player Home(World world, PlayingPosition position)
    {
        return world.OnIce(Side.Home).First(p => p.Data.Position == position);
    }

    /// <summary>Places the players for the coming play.</summary>
    static void Stage(World world)
    {
        int e = Attacks(world);
        var rw = Home(world, PlayingPosition.RW);
        var c = Home(world, PlayingPosition.C);
        var awayD = world.OnIce(Side.Away).First(p => p.Data.Position == PlayingPosition.D);
        Park(world);
        switch (play)
        {
            case 0:
                rw.Position = new Vector3(e * 13, 0, 4);
                break;
            case 1:
                rw.Position = new Vector3(e * 3, 0, -6);
                c.Position = new Vector3(e * 2, 0, 2);
                break;
            case 2:
                awayD.Position = new Vector3(e * 22, 0, 9);
                rw.Position = new Vector3(e * 2, 0, -9);
                break;
            default:
                rw.Position = new Vector3(e * 20, 0, 9);
                awayD.Position = new Vector3(e * 8, 0, -9);
                break;
        }
    }

    /// <summary>Moves every skater out of the puck's way along the boards, each club in its own half.</summary>
    static void Park(World world)
    {
        float[] z = [-11, -9.5f, 9.5f, 11, 11];
        foreach (var side in new[] { Side.Home, Side.Away })
        {
            int own = FaceoffFormation.DefendedEnd(world.Match, side);
            int i = 0;
            foreach (var p in world.OnIce(side).Where(p => !p.IsGoalie))
            {
                p.Position = new Vector3(own * (3 + i % 3 * 2), 0, z[i]);
                p.Velocity = Vector3.Zero;
                i++;
            }
        }
    }

    /// <summary>The pass, carry or dump of the current play.</summary>
    static void Send(World world)
    {
        int e = Attacks(world);
        var pk = world.Puck;
        var d = Home(world, PlayingPosition.D);
        var c = Home(world, PlayingPosition.C);
        switch (play)
        {
            case 0:
                Loose(pk, d, new Vector3(e * 2.5f, 0, 4), new Vector3(e * 16, 0, 0));
                break;
            case 1:
                pk.Carrier = c.Id;
                pk.LastTouch = c.Id;
                break;
            default:
                d.Position = new Vector3(-e * 10, 0, 4);
                Loose(pk, d, new Vector3(-e * 9.4f, 0, 4), new Vector3(e * 28, 0, 0));
                break;
        }
    }

    static void Carry(World world)
    {
        var c = Home(world, PlayingPosition.C);
        if (world.Puck.Carrier != c.Id || Math.Abs(c.Position.X) > 18) return;
        c.Position += new Vector3(Attacks(world) * 8 * Game.Step, 0, 0);
        c.Heading = Attacks(world) > 0 ? MathF.PI / 2 : -MathF.PI / 2;
        world.Puck.Position = c.Position + new Vector3(Attacks(world) * 0.6f, 0, 0.2f);
    }

    static void Loose(Puck pk, Player by, Vector3 from, Vector3 velocity)
    {
        pk.Carrier = -1;
        pk.LastTouch = by.Id;
        pk.Position = from;
        pk.Velocity = velocity;
    }

    static void Pan(World world)
    {
        float goal = Math.Clamp(world.Puck.Position.X, -17f, 17f);
        cameraX += (goal - cameraX) * 0.06f;
        float x = MathF.Round(cameraX * 4) / 4;
        var cam = BroadcastCamera.Straight();
        world.Camera = cam with { Position = cam.Position + new Vector3(x, 0, 0), Target = cam.Target + new Vector3(x, 0, 0) };
    }
}
