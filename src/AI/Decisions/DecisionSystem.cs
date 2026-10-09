using System.Numerics;
using StickAndPuck.Core;
using StickAndPuck.Players;

namespace StickAndPuck.AI;

/// <summary>
/// W6b: what each AI skater does with the puck and around it, added to the move positioning
/// (W6a) chose. It writes PlayerCommand like a human's input, so puck handling, checking and
/// skating never know who is in control.
/// <list type="bullet">
/// <item>Carrier: every look (quicker for a smart passer) weighs carrying on, shooting and each
/// pass on one scale (see <see cref="Choices"/>), and does the best. Smart passes sets how far
/// the carrier looks, how well they read the defenders and how much their judgement wobbles, so a
/// smart club finds open teammates up the ice and a poor one passes into sticks.</item>
/// <item>Shots: a wrist shot in close or under pressure, a slap shot from the point with time,
/// aimed away from where the goalie leans. Shot accuracy makes a skater shoot from further out.</item>
/// <item>Chase: the teammate a pass is meant for skates onto its line to meet it, and after a
/// shot the forward nearest the net crashes it for the rebound. Loose pucks are chased by
/// positioning's chaser.</item>
/// <item>Checks: the defender pressing the carrier decides once per approach whether to finish the
/// check, likelier with a better checking trait, then skates through the carrier and checks.</item>
/// </list>
/// </summary>
internal sealed class DecisionSystem : ISystem
{
    const float CheckFrom = 3.2f;   // distance at which a presser decides whether to hit
    const float ShootFrom = 0.6f;   // a shot worth less than this is not taken: only good chances, about 1 a minute

    readonly Random rng;

    public DecisionSystem()
#pragma warning disable S2245 // gameplay variation, not security
        : this(new Random())
#pragma warning restore S2245
    {
    }

    /// <summary>Initializes the system with its own randomness, so tests repeat.</summary>
    /// <param name="rng">Randomness for misreads, shot choice and checks.</param>
    public DecisionSystem(Random rng)
    {
        this.rng = rng;
    }

    public void Update(World world, float dt)
    {
        var board = Decisions.Board(world);
        Track(world, board);
        if (!Positioning.Drives(world)) return;
        var pk = world.Puck;
        if (world.Match.Phase is not (MatchPhase.Play or MatchPhase.Overtime) || !pk.Live)
        {
            board.ForgetShots();
            return;
        }

        int crasher = Crasher(world, board);
        foreach (var p in world.Players)
        {
            if (!p.OnIce || p.IsGoalie || p.Slot >= 0 || p.Action is PlayerAction.Fallen or PlayerAction.Fight) continue;
            var brain = board.For(p.Id);
            if (brain.ShootSteps > 0 || (p.Action == PlayerAction.Shoot && pk.Carrier == p.Id)) { HoldShot(world, p, brain); continue; }
            if (pk.Carrier == p.Id) { Carry(world, p, brain); continue; }
            if (MeetPass(world, board, p)) continue;
            if (Positioning.JobOf(world, p) == Job.Chase && pk.Carrier < 0) { Pounce(world, p); continue; }
            if (p.Id == crasher) { CrashNet(world, p); continue; }
            Hit(world, board, p, brain);
        }
    }

    public void DrawHud(World world, RenderContext render)
    {
        DecisionScenes.DrawLine(world);
    }

    /// <summary>Reads what happened last step: new carriers, passes and shots made, and how the pass in flight ended.</summary>
    static void Track(World world, DecisionBoard board)
    {
        var pk = world.Puck;
        int last = board.LastCarrier;
        if (pk.Carrier >= 0 && pk.Carrier != last && pk.Carrier < world.Players.Count)
        {
            var brain = board.For(pk.Carrier);
            brain.GotAt = world.Time;
            brain.NextThink = world.Time + 0.12f;
        }
        if (last >= 0 && last < world.Players.Count && pk.Carrier < 0 && pk.LastTouch == last && pk.Live)
        {
            var p = world.Players[last];
            var tally = board.Tally[(int)p.Side];
            if (p.Action == PlayerAction.Pass)
            {
                tally.Passes++;
                var passer = board.For(p.Id);
                board.Pass = new InFlight(p.Id, passer.PassTo, p.Side, world.Time);
                passer.PassTo = -1;   // a human's pass later has no receiver in mind
            }
            else if (p.Action == PlayerAction.Shoot)
            {
                tally.Shots++;
                board.Shot = (p.Side, world.Time);
            }
        }
        board.LastCarrier = pk.Carrier;

        if (board.Pass is not { } pass) return;
        if (pk.Carrier >= 0 && pk.Carrier < world.Players.Count)
        {
            var tally = board.Tally[(int)pass.Side];
            if (world.Players[pk.Carrier].Side == pass.Side) tally.Completed++;
            else tally.Intercepted++;
            board.Pass = null;
        }
        else if (!pk.Live || world.Time - pass.At > 3f || world.Match.Phase is not (MatchPhase.Play or MatchPhase.Overtime))
        {
            board.Pass = null;
        }
    }

    /// <summary>Keeps shoot held through a slap shot's wind-up, then lets go, with the stick on the aim until the puck leaves.</summary>
    static void HoldShot(World world, Player p, Brain brain)
    {
        if (world.Puck.Carrier != p.Id) { brain.ShootSteps = 0; return; }
        bool hold = brain.ShootSteps > 0;
        if (hold) brain.ShootSteps--;
        p.Command = p.Command with { Move = brain.ShootAim, Shoot = hold, Sprint = false };
    }

    /// <summary>The carrier's look at the play: carry on, shoot or pass, whichever is worth most.</summary>
    void Carry(World world, Player p, Brain brain)
    {
        if (p.Action != PlayerAction.None || world.Time < brain.NextThink) return;
        int smart = p.Data.Traits.SmartPasses;
        brain.NextThink = world.Time + Choices.ThinkEvery(smart) * (0.8f + 0.4f * rng.NextSingle());

        int end = PuckHandling.AttackEnd(world, p.Side);
        var at = Flat(world.Puck.Position);
        var seen = Defenders(world, p, smart);
        float wobble = Choices.Wobble(smart);
        float nearest = seen.Where(d => d.Speed > 5f).Select(d => Vector2.Distance(d.At, Flat(p.Position))).DefaultIfEmpty(99f).Min();
        bool pressed = nearest < 2.6f;
        float held = world.Time - brain.GotAt;
        bool camping = held > 3f && p.Speed < 1.2f;   // stood still with the puck: time to let it go

        float carry = Choices.SpotValue(end, at) * (1f - Math.Min(0.35f, held / 12f)) * (camping ? 0.5f : 1f);
        float accuracy = Traits.Unit(p.Data.Traits.ShotAccuracy);
        var blockers = world.Players.Where(o => o.OnIce && o.Side != p.Side && !o.IsGoalie).Select(o => Flat(o.Position));
        float lane = Choices.ShotLane(end, at, blockers);
        float shoot = Choices.ShotQuality(end, at) * (0.75f + 0.5f * accuracy) * lane * (pressed ? 0.7f : 1f);
        shoot += wobble * PuckHandling.Gauss(rng);

        var pass = BestPass(world, p, end, at, seen, smart);
        float passValue = pass?.Value + wobble * PuckHandling.Gauss(rng) ?? float.MinValue;

        if (shoot > carry && shoot > passValue && shoot > (camping ? ShootFrom * 0.6f : ShootFrom)) Shoot(world, p, brain, end, at, pressed);
        else if (PointShot(end, at, lane, pressed, held) && passValue < carry) Shoot(world, p, brain, end, at, pressed);
        else if (pass is { } best && (passValue > carry * 1.05f + 0.01f || (camping && held > 6f))) Pass(p, brain, best, at);
        else if (camping && held > 6f && Choices.ShotQuality(end, at) > 0f) Shoot(world, p, brain, end, at, pressed);   // pinned too long: get rid of it
    }

    /// <summary>Now and then, a skater at the point with a clear lane and time winds up from distance, for a deflection or a rebound.</summary>
    bool PointShot(int end, Vector2 at, float lane, bool pressed, float held)
    {
        float distance = RinkSpec.GoalLineX - end * at.X;
        return !pressed && held > 1.2f && lane > 0.9f && distance is > 12f and < 20f && MathF.Abs(at.Y) < 8f && rng.NextSingle() < 0.15f;
    }

    /// <summary>The opponents' sticks as this carrier sees them: misread by up to a few metres for a poor passer, re-read every look.</summary>
    List<(Vector2 At, float Speed)> Defenders(World world, Player p, int smart)
    {
        float misread = Choices.Misread(smart);
        var seen = new List<(Vector2 At, float Speed)>();
        foreach (var o in world.Players)
        {
            if (!o.OnIce || o.Side == p.Side || o.Action == PlayerAction.Fallen) continue;
            var off = new Vector2(PuckHandling.Gauss(rng), PuckHandling.Gauss(rng)) * misread * 0.6f;
            seen.Add((Flat(o.IsGoalie ? o.Position : PuckHandling.Blade(o)) + off, o.IsGoalie ? 3f : SkatingModel.TopSpeed(o.Data.Traits, o.Energy)));
        }
        return seen;
    }

    /// <summary>The best pass the carrier sees, or null with nobody to pass to.</summary>
    static PassOption? BestPass(World world, Player p, int end, Vector2 at, List<(Vector2 At, float Speed)> seen, int smart)
    {
        PassOption? best = null;
        float vision = Choices.Vision(smart);
        var keep = p.Command;
        foreach (var mate in world.OnIce(p.Side))
        {
            if (mate == p || mate.IsGoalie || mate.Action == PlayerAction.Fallen) continue;
            var (target3, speed) = PuckHandling.Lead(world.Puck.Position, mate, smart);
            var target = Flat(target3);
            float d = Vector2.Distance(at, target);
            if (d < Choices.ShortestPass || d > vision) continue;
            if (smart >= 50 && Choices.Offside(end, at, Flat(mate.Position))) continue;

            // the stick has to point so puck handling picks this teammate
            p.Command = keep with { Move = Vector2.Normalize(target - at) };
            bool picked = PuckHandling.PickReceiver(world, p) == mate;
            p.Command = keep;
            if (!picked) continue;

            float success = Choices.LaneSuccess(at, target, speed, seen, smart);
            float value = success * Choices.SpotValue(end, target) * (0.5f + 0.5f * Choices.Room(target, seen)) - (1f - success) * 0.08f;
            if (best == null || value > best.Value.Value) best = new PassOption(mate, target, success, value);
        }
        return best;
    }

    void Shoot(World world, Player p, Brain brain, int end, Vector2 at, bool pressed)
    {
        var toNet = new Vector2(end * RinkSpec.GoalLineX, 0) - at;
        float distance = toNet.Length();
        var goalie = world.OnIce(p.Side == Side.Home ? Side.Away : Side.Home).FirstOrDefault(o => o.IsGoalie);
        int side = Choices.ShotSide(at, goalie?.Position.Z);

        // an accurate shooter points the stick at the side of the net to beat the goalie; anyone
        // else, and any slap shot, just aims at the net and lets the shot find the far side
        bool slap = distance > 13f && !pressed && rng.NextSingle() < 0.75f;
        var aim = Vector2.Normalize(new Vector2(end * 0.8f, side * 0.6f));
        bool picksCorner = !slap && Traits.Unit(p.Data.Traits.ShotAccuracy) >= 0.75f;
        if (!picksCorner || Vector2.Dot(aim, Vector2.Normalize(toNet)) < MathF.Cos(70f * MathF.PI / 180f)) aim = Vector2.Normalize(toNet);

        brain.ShootSteps = slap ? 28 + rng.Next(22) : 0;
        brain.ShootAim = aim;
        p.Command = p.Command with { Move = aim, Shoot = true, Sprint = false };
    }

    static void Pass(Player p, Brain brain, PassOption pass, Vector2 at)
    {
        brain.PassTo = pass.Receiver.Id;
        p.Command = p.Command with { Move = Vector2.Normalize(pass.Target - at), Pass = true };
    }

    /// <summary>The teammate a pass is meant for skates onto its line to meet it.</summary>
    static bool MeetPass(World world, DecisionBoard board, Player p)
    {
        var pk = world.Puck;
        if (board.Pass is not { } pass || pass.Receiver != p.Id || pk.Carrier >= 0) return false;
        var v = Flat(pk.Velocity);
        if (v.Length() < 1.5f) return false;
        var from = Flat(pk.Position);
        var blade = Flat(PuckHandling.Blade(p));
        var dir = Vector2.Normalize(v);
        float along = Math.Max(0.5f, Vector2.Dot(blade - from, dir));
        var meet = TeamShape.Inside(from + dir * along, 0.6f);
        p.Command = Steering.Toward(p, Steering.BodyForBlade(p, meet), 0.6f);
        return true;
    }

    /// <summary>
    /// The chaser close to a slow loose puck skates right over it, easing off, so the puck is
    /// taken at the skates or the blade; going for the blade at speed only circles the puck.
    /// </summary>
    static void Pounce(World world, Player p)
    {
        var pk = world.Puck;
        var puck = Flat(pk.Position);
        if (Flat(pk.Velocity).Length() > 4f || Vector2.Distance(puck, Flat(p.Position)) > 4f) return;
        p.Command = Steering.Toward(p, TeamShape.Inside(puck, SkatingModel.BodyRadius), 1.5f);
    }

    /// <summary>After a shot, the forward of the shooting side nearest the net who is not chasing goes to the net for a rebound.</summary>
    static int Crasher(World world, DecisionBoard board)
    {
        if (board.Shot is not { } shot || world.Time - shot.At > 1.6f || world.Puck.Carrier >= 0) return -1;
        int end = PuckHandling.AttackEnd(world, shot.Side);
        var net = new Vector3(end * RinkSpec.GoalLineX, 0, 0);
        return world.OnIce(shot.Side)
            .Where(o => !o.IsGoalie && o.Slot < 0 && !Roles.IsDefence(Positioning.RoleOf(world, o)) && Positioning.JobOf(world, o) != Job.Chase)
            .Where(o => Vector3.Distance(o.Position, net) < 14f)
            .MinBy(o => Vector3.Distance(o.Position, net))?.Id ?? -1;
    }

    static void CrashNet(World world, Player p)
    {
        int end = PuckHandling.AttackEnd(world, p.Side);
        float z = world.Puck.Position.Z > 0 ? -0.9f : 0.9f;
        p.Command = Steering.Toward(p, new Vector2(end * (RinkSpec.GoalLineX - 2.4f), z), 0.8f, sprint: true);
    }

    /// <summary>The skater pressing a carrier decides once per approach whether to hit, then skates through and checks.</summary>
    void Hit(World world, DecisionBoard board, Player p, Brain brain)
    {
        var pk = world.Puck;
        if (pk.Carrier < 0 || pk.Carrier >= world.Players.Count || p.Action != PlayerAction.None) return;
        var carrier = world.Players[pk.Carrier];
        if (carrier.Side == p.Side || carrier.IsGoalie || Positioning.JobOf(world, p) != Job.Pressure) return;
        float d = Vector3.Distance(p.Position, carrier.Position);

        if (world.Time >= brain.CheckReady && d < CheckFrom)
        {
            float checking = Traits.Unit(p.Data.Traits.Checking);
            brain.HitUntil = rng.NextSingle() < 0.04f + 0.15f * checking ? world.Time + 0.9f : float.NegativeInfinity;
            brain.CheckReady = world.Time + 4f;
        }
        if (world.Time > brain.HitUntil) return;

        var lead = carrier.Position + carrier.Velocity * 0.2f;
        p.Command = Steering.Toward(p, Flat(lead), 0.1f, sprint: true);
        if (CheckingModel.Target(world, p) == carrier)
        {
            p.Command = p.Command with { Check = true };
            brain.HitUntil = float.NegativeInfinity;
            board.Tally[(int)p.Side].Checks++;
        }
    }

    static Vector2 Flat(Vector3 v)
    {
        return new Vector2(v.X, v.Z);
    }
}
