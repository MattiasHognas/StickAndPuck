using System.Globalization;
using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.AI.Positioning;
using StickAndPuck.App.AI.SpecialTeams;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players.Puck;
using StickAndPuck.App.Rules.Penalties;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.AI.Tactics
{
    /// <summary>Each club plays its own tactics, from the <c language="C#">tactics</c> field of its club file
    /// (<see cref="ClubTactics"/>). Runs after positioning, defence and special teams
    /// and before decisions, and takes over AI skaters the same way defence does: job
    /// <see cref="Job.Requested"/>, its own target and the move, so decisions leave them alone.
    /// Only at even strength in regulation; overtime's 3-on-3 and special teams stay as they are.
    /// <list type="bullet">
    /// <item>Aggressive forecheck (2-1-2): while the other side has the puck in its own half, the
    /// skater positioning sends at the carrier stays on him, a second forward goes in on his nearest
    /// outlet, the third sits high in the zone and both defencemen pinch at the blue line.</item>
    /// <item>Trap (1-3-1): while the other side has the puck in its own zone, one forward angles at
    /// their blue line, three wait in a line across the red line and the last defenceman stays back.</item>
    /// <item>Dump and chase: an AI carrier coming through the neutral zone with defenders ahead
    /// carries wide and, once over the red line, fires the puck into the corner with fewer defenders;
    /// two forwards go after it.</item>
    /// <item>Power play: a club that always sets up the umbrella or the 1-3-1 gets that shape instead
    /// of the special teams coin toss.</item>
    /// <item>Balanced and carry change nothing: the AI as it plays without tactics.</item>
    /// </list>
    /// Hockey IQ still sets how quickly and exactly each skater reads his tactical spot.</summary>
    /// <remarks>Initializes the system with its own randomness, so tests repeat.</remarks>
    internal sealed partial class TacticsSystem(Random rng) : ISystem
    {
        private const float NoShotRange = 26.4f; // a wrist shot from nearer, facing the net, goes at the net (PuckHandling.LongestShot is 26)
        private const float Release = 0.32f; // seconds from deciding a dump to the puck leaving the stick: 3 steps of shoot plus the strike delay
        private const float WideZ = 10f; // how wide a dump-and-chase carrier takes the puck before the red line
        private const float ChaseFor = 3.5f; // seconds the second forward goes after a dump
        private const float Settled = 1.5f; // seconds the breakout side has had the puck before it counts in the tally

        /// <summary>The dice seed without a match seed.</summary>
        internal const int Fallback = 4;

        private readonly Random rng = rng;

        public TacticsSystem()
            : this(Seeds.Dice(Seeds.Environment, "tactics", Fallback)) { }

        public void Update(World world, float dt)
        {
            TacticsBoard board = TacticsState.Board(world);
            board.Tasks.Clear();
            Puck pk = world.Puck;
            if (
                !PositioningModel.Drives(world)
                || world.Match.Phase is not (MatchPhase.Play or MatchPhase.Overtime)
                || !pk.Live
            )
            {
                board.Chasing = Maybe.None<ChaseWatch>();
                board.Dump = Maybe.None<DumpPlan>();
                Track(world, board, Maybe.None<Player>(), 0f, even: false);
                return;
            }

            Maybe<Player> carrier =
                pk.Carrier >= 0 && pk.Carrier < world.Players.Count
                    ? world.Players[pk.Carrier]
                    : Maybe.None<Player>();
            if (PenaltyCalls.PowerPlay(world).TryGetSide(out Side pp))
            {
                PowerPlay(world, pp);
            }

            bool even =
                world.Match.Phase == MatchPhase.Play
                && PenaltyCalls.PowerPlay(world) == SideOrNone.None
                && Skaters(world, Side.Home).Count == Skaters(world, Side.Away).Count;
            Track(world, board, carrier, dt, even);
            if (!even)
            {
                board.Chasing = Maybe.None<ChaseWatch>();
                board.Dump = Maybe.None<DumpPlan>();
                return;
            }

            Watch(world, board, carrier);
            if (carrier.TryGet(out Player skater) && !skater.IsGoalie)
            {
                if (
                    TacticsState.Of(world, skater.Side).Entry == ZoneEntry.DumpAndChase
                    && skater.Slot < 0
                )
                {
                    Entry(world, board, skater);
                }

                Side defending = Other(skater.Side);
                switch (TacticsState.Of(world, defending).Forecheck)
                {
                    case Forecheck.Aggressive:
                        Aggressive(world, board, defending, skater, dt);
                        break;
                    case Forecheck.Trap:
                        Trap(world, board, defending, skater, dt);
                        break;
                    case Forecheck.Balanced:
                        break;
                    default:
                        break;
                }
            }
            else if (!carrier.HasValue && board.Chasing.TryGet(out ChaseWatch chase))
            {
                if (world.Time > chase.Until)
                {
                    board.Chasing = Maybe.None<ChaseWatch>();
                }
                else
                {
                    Chase(world, board, chase.Side, dt);
                }
            }
        }

        /// <summary>Follows a dump: off the stick, then who gets to it first.</summary>
        private static void Watch(World world, TacticsBoard board, Maybe<Player> onPuck)
        {
            if (!board.Chasing.TryGet(out ChaseWatch dumped))
            {
                return;
            }

            if (!onPuck.TryGet(out Player carrier))
            {
                board.Chasing = dumped with { Released = true };
            }
            else if (dumped.Released || carrier.Id != dumped.Dumper || world.Time > dumped.Until)
            {
                if (dumped.Released && carrier.Side == dumped.Side)
                {
                    TacticsState.Tally(world, dumped.Side).Recovered++;
                }

                board.Chasing = Maybe.None<ChaseWatch>();
            }
        }

        /// <summary>A club that always plays one power play shape gets it, in place of special teams' coin toss.</summary>
        private static void PowerPlay(World world, Side pp)
        {
            SpecialTeamsBoard st = SpecialTeamsState.Board(world);
            if (
                st.PowerPlay.Is(pp)
                && PowerPlayShapes
                    .Formation(TacticsState.Of(world, pp).PowerPlay)
                    .TryGet(out Formation shape)
            )
            {
                st.PpShape = shape;
            }
        }

        /// <summary>Counts each side's zone entries and, while the other side breaks out of its zone, where its skaters stand.</summary>
        private static void Track(
            World world,
            TacticsBoard board,
            Maybe<Player> carrier,
            float dt,
            bool even
        )
        {
            Puck pk = world.Puck;
            Maybe<Player> lastTouch =
                pk.LastTouch >= 0 && pk.LastTouch < world.Players.Count
                    ? world.Players[pk.LastTouch]
                    : Maybe.None<Player>();
            SideOrNone owner = SideOrNone.None;
            if (carrier.TryGet(out Player holder))
            {
                owner = holder.Side.OrNone();
            }
            else if (lastTouch.TryGet(out Player toucher))
            {
                owner = toucher.Side.OrNone();
            }
            Maybe<Player> skater =
                carrier.TryGet(out Player held) && !held.IsGoalie ? held : Maybe.None<Player>();
            if (carrier.TryGet(out Player onPuck) && !board.Owner.Is(onPuck.Side))
            {
                board.Owner = onPuck.Side.OrNone();
                board.OwnedSince = world.Time;
            }
            foreach (Side side in new[] { Side.Home, Side.Away })
            {
                int end = PuckHandling.AttackEnd(world, side);
                float a = end * pk.Position.X;
                float last = board.LastA[(int)side];
                board.LastA[(int)side] = a;
                if (!even)
                {
                    continue;
                }

                TacticsTally tally = TacticsState.Tally(world, side);
                if (owner.Is(side) && last <= RinkSpec.BlueLineX && a > RinkSpec.BlueLineX)
                {
                    tally.Entries++;
                    if (carrier.TryGet(out Player entering) && entering.Side == side)
                    {
                        tally.Carried++;
                    }
                }

                // the other side carrying the puck out over its own blue line: how many of this side are back, goal side of it
                if (
                    skater.TryGet(out Player exiting)
                    && exiting.Side != side
                    && board.LastCarrierA[(int)side] > RinkSpec.BlueLineX
                    && end * exiting.Position.X <= RinkSpec.BlueLineX
                )
                {
                    tally.Exits++;
                    tally.BackAtExits += Skaters(world, side)
                        .Count(p => end * p.Position.X < (end * exiting.Position.X) - 1f);
                }
                board.LastCarrierA[(int)side] =
                    skater.TryGet(out Player leaving) && leaving.Side != side
                        ? end * leaving.Position.X
                        : float.NegativeInfinity;

                // the other side carrying the puck in its own zone, settled for a moment: a breakout this side is meeting
                if (
                    !skater.TryGet(out Player breaking)
                    || breaking.Side == side
                    || end * breaking.Position.X <= RinkSpec.BlueLineX
                )
                {
                    continue;
                }

                if (world.Time - board.OwnedSince < Settled)
                {
                    continue;
                }

                TrackBreakout(world, side, tally, end, dt);
            }
        }

        private static void TrackBreakout(
            World world,
            Side side,
            TacticsTally tally,
            int end,
            float dt
        )
        {
            tally.BreakoutSeconds += dt;
            foreach (Player p in Skaters(world, side))
            {
                float pa = end * p.Position.X;
                if (pa > RinkSpec.BlueLineX)
                {
                    tally.DeepSkaterSeconds += dt;
                }
                else if (pa >= -RinkSpec.BlueLineX)
                {
                    tally.NeutralSkaterSeconds += dt;
                }
            }
        }

        public void DrawHud(World world, RenderContext render)
        {
            if (!world.Switches.TacticsLine && !world.Settings.MatchDebug)
            {
                return;
            }

            string Club(Side side)
            {
                ClubTactics t = TacticsState.Of(world, side);
                TacticsTally tally = TacticsState.Tally(world, side);
                return string.Create(
                    CultureInfo.InvariantCulture,
                    $"{world.Setup.Club(side).ShortName} {Name(t.Forecheck)} {(t.Entry == ZoneEntry.DumpAndChase ? "DUMP" : "CARRY")} {tally.Dumps}/{tally.Entries}"
                );
            }
            DrawText(
                $"{Club(Side.Home)}   {Club(Side.Away)}",
                6,
                246,
                10,
                new Color(0xef, 0xe0, 0xc2, 255)
            );
        }

        /// <summary>The words a debug trace shows for a tactical task.</summary>
        private static string Reason(TacticTask task)
        {
            return task switch
            {
                TacticTask.Chase => "chase the dump",
                TacticTask.Outlet => "forecheck: take the outlet",
                TacticTask.High => "forecheck: sit high",
                TacticTask.Pinch => "forecheck: pinch",
                TacticTask.Angle => "trap: angle",
                TacticTask.Back => "trap: stay back",
                TacticTask.Wall => "trap: wall",
                TacticTask.None => "tactics",
                _ => throw new NotSupportedException("no reason for task " + task),
            };
        }

        private static string Name(Forecheck f)
        {
            return f switch
            {
                Forecheck.Aggressive => "2-1-2",
                Forecheck.Trap => "TRAP",
                Forecheck.Balanced => "BAL",
                _ => throw new NotSupportedException("no name for forecheck " + f),
            };
        }

        private static List<Player> Skaters(World world, Side side)
        {
            return
            [
                .. world
                    .OnIce(side)
                    .Where(p =>
                        !p.IsGoalie && p.Action is not (PlayerAction.Fallen or PlayerAction.Fight)
                    )
                    .OrderBy(p => p.Id),
            ];
        }

        private static Side Other(Side side)
        {
            return side == Side.Home ? Side.Away : Side.Home;
        }

        private static Vector2 SafeNormal(Vector2 v)
        {
            return v.LengthSquared() > 1e-6f ? Vector2.Normalize(v) : Vector2.UnitX;
        }

        private static Vector2 Flat(Vector3 v)
        {
            return new Vector2(v.X, v.Z);
        }
    }
}
