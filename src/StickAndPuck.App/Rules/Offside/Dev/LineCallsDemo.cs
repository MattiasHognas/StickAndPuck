using System.Globalization;
using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Camera;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match;

namespace StickAndPuck.App.Rules.Offside.Dev
{
    /// <summary>The scripted plays and camera behind <see cref="LineCallsDemoScene"/>.</summary>
    internal sealed class LineCallsDemo
    {
        private float playFrom = float.NegativeInfinity;
        private int play = -1;
        private bool sent;
        private float cameraX;

        /// <summary>A second after the puck drops, places the players and sends the puck; pans the camera to the puck.</summary>
        public static void Update(World world)
        {
            if (world.Dev.LineCalls.TryGet(out LineCallsDemo demo))
            {
                demo.Run(world);
            }
        }

        private void Run(World world)
        {
            Pan(world);
            if (world.Match.Phase != MatchPhase.Play)
            {
                playFrom = float.NegativeInfinity;
                return;
            }
            if (float.IsNegativeInfinity(playFrom))
            {
                playFrom = world.Time;
                play = (play + 1) % 3;
                sent = false;
            }
            float since = world.Time - playFrom;
            if (!sent && since > 1f)
            {
                // staged in the same step as the puck is sent, so the AI has not moved anyone yet
                sent = true;
                Stage(world);
                Send(world);
            }
            if (play == 1 && sent)
            {
                Carry(world);
            }
            // a play the rules let stand ends after a few seconds with a frozen-puck style whistle at centre
            if (since > 6f)
            {
                _ = MatchFlow.Whistle(world, RinkSpec.CentreDot);
            }
        }

        /// <summary>Prints a call to the console while the demo runs.</summary>
        public static void Log(World world, string call, Side side, Vector3 at, Vector3 dot)
        {
            if (world.Dev.LineCalls.TryGet(out LineCallsDemo demo))
            {
                Console.WriteLine(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"SAP: {call} by {side} at ({at.X:0.0}, {at.Z:0.0}), faceoff at ({dot.X}, {dot.Z}), play {demo.play}"
                    )
                );
            }
        }

        private static int Attacks(World world)
        {
            return -FaceoffFormation.DefendedEnd(world.Match, Side.Home);
        }

        private static Player Home(World world, PlayingPosition position)
        {
            return world.OnIce(Side.Home).First(p => p.Data.Position == position);
        }

        /// <summary>Places the players for the coming play.</summary>
        private void Stage(World world)
        {
            int e = Attacks(world);
            Player rw = Home(world, PlayingPosition.RW);
            Player c = Home(world, PlayingPosition.C);
            Player awayD = world.OnIce(Side.Away).First(p => p.Data.Position == PlayingPosition.D);
            Park(world);
            switch (play)
            {
                case 0:
                    rw.Position = new Vector3(e * 10.5f, 0, 4);
                    break;
                case 1:
                    rw.Position = new Vector3(e * 3, 0, -6);
                    c.Position = new Vector3(e * 2, 0, 2);
                    break;
                default:
                    awayD.Position = new Vector3(e * 22, 0, 9);
                    rw.Position = new Vector3(e * 2, 0, -9);
                    break;
            }
        }

        /// <summary>Moves every skater out of the puck's way along the boards, each club in its own half.</summary>
        private static void Park(World world)
        {
            float[] z = [-11, -9.5f, 9.5f, 11, 11];
            foreach (Side side in new[] { Side.Home, Side.Away })
            {
                int own = FaceoffFormation.DefendedEnd(world.Match, side);
                int i = 0;
                foreach (Player p in world.OnIce(side).Where(p => !p.IsGoalie))
                {
                    p.Position = new Vector3(own * (3 + (i % 3 * 2)), 0, z[i]);
                    p.Velocity = Vector3.Zero;
                    i++;
                }
            }
        }

        /// <summary>The pass, carry or dump of the current play.</summary>
        private void Send(World world)
        {
            int e = Attacks(world);
            Puck pk = world.Puck;
            Player d = Home(world, PlayingPosition.D);
            Player c = Home(world, PlayingPosition.C);
            switch (play)
            {
                case 0:
                    Loose(pk, d, new Vector3(e * 6.6f, 0, 4), new Vector3(e * 14, 0, 0));
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

        private static void Carry(World world)
        {
            Player c = Home(world, PlayingPosition.C);
            if (world.Puck.Carrier != c.Id || Math.Abs(c.Position.X) > 18)
            {
                return;
            }

            c.Position += new Vector3(Attacks(world) * 8 * Game.Step, 0, 0);
            c.Heading = Attacks(world) > 0 ? MathF.PI / 2 : -MathF.PI / 2;
            world.Puck.Position = c.Position + new Vector3(Attacks(world) * 0.6f, 0, 0.2f);
        }

        private static void Loose(Puck pk, Player by, Vector3 from, Vector3 velocity)
        {
            pk.Carrier = -1;
            pk.LastTouch = by.Id;
            pk.Position = from;
            pk.Velocity = velocity;
        }

        private void Pan(World world)
        {
            float goal = Math.Clamp(world.Puck.Position.X, -17f, 17f);
            cameraX += (goal - cameraX) * 0.06f;
            float x = MathF.Round(cameraX * 4, MidpointRounding.ToEven) / 4;
            Camera3D cam = BroadcastCamera.Straight();
            world.Camera = cam with
            {
                Position = cam.Position + new Vector3(x, 0, 0),
                Target = cam.Target + new Vector3(x, 0, 0),
            };
        }
    }
}
