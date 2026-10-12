using System.Numerics;
using StickAndPuck.Core;
using StickAndPuck.Players;
using StickAndPuck.Rules;

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
/// <item>W22a: every look, pass and shot is rolled with <see cref="Jitter"/>: a fumbled look is
/// read like a much weaker player, a fumbled pass goes a beat late, a fumbled shot picks the
/// goalie's side; a lucky roll plays it like a star. In the attacking-zone cycle the carrier
/// values keeping the puck and fears a turnover more, so the club passes it round until a lane
/// to a good shot opens; passes never go through a net's cage.</item>
/// <item>W63: a carrier pinned in his own zone with no safe pass sometimes fires the puck out, hard
/// up the boards: untouched from his own half, that is icing. A poor or rushed skater does it far
/// more often than a smart one. A carrier coming up to the
/// attacking blue line with a teammate already over it may hold up short of the line so he can
/// tag up; a smart, unhurried one usually does, a poor or pressed one carries on in, offside.
/// Neither care applies with the rule switched off (Arcade).</item>
/// <item>W68: every write goes through <see cref="AiCommand"/>, which says who owns each field. Per
/// skater, one branch of <see cref="Update"/> runs, in this order: a committed shot is held; a
/// delayed pass goes; a carrier holds up at the line (unless special teams or tactics claimed him
/// this step) and looks at the play; a receiver meets the pass; the chaser pounces; the crasher
/// goes to the net; a presser hits. Candidate passes are weighed without touching anyone.</item>
/// </list>
/// </summary>
internal sealed class DecisionSystem : ISystem
{
    const float CheckFrom = 3.2f;   // distance at which a presser decides whether to hit
    const float ShootFrom = 0.64f;  // a shot worth less than this is not taken, unless the carrier has stood still with the puck
    const float SafePass = 0.55f;   // a pass the carrier gives at least this chance is a way out of his zone; with none he may clear it
    const float HoldFrom = 4f;      // metres short of the attacking blue line at which a carrier looks for teammates already over it
    const float HoldFor = 1.6f;     // longest a carrier holds up at the line before carrying on in

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
            if (pk.Carrier == p.Id && brain.LatePass != null) { LatePass(world, p, brain); continue; }
            if (pk.Carrier == p.Id)
            {
                if (!AiCommand.Held(world, p)) HoldLine(world, p, brain);   // W68: a dump taken wide stays wide
                Carry(world, p, brain);
                continue;
            }
            brain.LatePass = null;
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
                if (passer.PassTo >= 0 && passer.PassTo < world.Players.Count) Aimed(world, tally, world.Players[passer.PassTo]);
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

    /// <summary>Counts how open and how much on the move the teammate an AI pass was aimed at was.</summary>
    static void Aimed(World world, DecisionTally tally, Player receiver)
    {
        tally.Aimed++;
        if (receiver.Speed > 2f) tally.ToMoving++;
        float nearest = world.OnIce(receiver.Side == Side.Home ? Side.Away : Side.Home)
            .Where(o => !o.IsGoalie).Select(o => Vector3.Distance(o.Position, receiver.Position)).DefaultIfEmpty(99f).Min();
        if (nearest > 3f) tally.ToOpen++;
    }

    /// <summary>Keeps shoot held through a slap shot's wind-up, then lets go, with the stick on the aim until the puck leaves.</summary>
    static void HoldShot(World world, Player p, Brain brain)
    {
        if (world.Puck.Carrier != p.Id) { brain.ShootSteps = 0; return; }
        bool hold = brain.ShootSteps > 0;
        if (hold) brain.ShootSteps--;
        AiCommand.Act(world, p, p.Command with { Move = brain.ShootAim, Shoot = hold, Sprint = false }, "shot held");
    }

    /// <summary>The carrier's look at the play: carry on, shoot or pass, whichever is worth most.</summary>
    void Carry(World world, Player p, Brain brain)
    {
        if (p.Action != PlayerAction.None || world.Time < brain.NextThink) return;
        brain.NextThink = world.Time + Choices.ThinkEvery(p.Data.Traits.HockeyIq) * (0.8f + 0.4f * rng.NextSingle());

        // W22a: this look is rolled against hockey IQ; a fumble reads it like a much weaker player, luck like a star
        int smart = Jitter.Roll(world, p, rng).Iq;

        int end = PuckHandling.AttackEnd(world, p.Side);
        var at = Flat(world.Puck.Position);
        var seen = Defenders(world, p, smart);
        float wobble = Choices.Wobble(smart);
        float nearest = seen.Where(d => d.Speed > 5f).Select(d => Vector2.Distance(d.At, Flat(p.Position))).DefaultIfEmpty(99f).Min();
        bool pressed = nearest < 2.6f;
        float held = world.Time - brain.GotAt;
        bool cycling = Positioning.StationOf(world, p) != null;
        bool camping = held > (cycling ? 5f : 3f) && p.Speed < 1.2f;   // stood still with the puck: time to let it go

        float carry = Choices.SpotValue(end, at) * (1f - Math.Min(0.35f, held / 12f)) * (camping ? 0.5f : 1f);

        // W22a: in the cycle the puck kept is worth more than its spot: the next turn may open a lane
        if (cycling && !camping) carry += Choices.CycleKeep;
        float accuracy = Traits.Unit(p.Data.Traits.ShotAccuracy);
        var blockers = world.Players.Where(o => o.OnIce && o.Side != p.Side && !o.IsGoalie).Select(o => Flat(o.Position));
        float lane = Choices.ShotLane(end, at, blockers);
        float shoot = Choices.ShotQuality(end, at) * (0.75f + 0.5f * accuracy) * lane * (pressed ? 0.7f : 1f);
        shoot += wobble * PuckHandling.Gauss(rng);

        var pass = BestPass(world, p, end, at, seen, smart, cycling);
        if (Clear(world, p, brain, end, at, nearest, pass)) return;
        float passValue = pass?.Value + wobble * PuckHandling.Gauss(rng) ?? float.MinValue;

        if (shoot > carry && shoot > passValue && shoot > (camping ? ShootFrom * 0.6f : ShootFrom)) Shoot(world, p, end, at, pressed);
        else if (PointShot(end, at, lane, pressed, held) && passValue < carry) Shoot(world, p, end, at, pressed);
        else if (pass is { } best && (passValue > carry * 1.05f + 0.01f || (camping && held > 6f && (!cycling || best.Success > 0.35f))))
            Pass(world, p, brain, best, at);
        else if (camping && held > 6f && Choices.ShotQuality(end, at) > (cycling ? 0.2f : 0f)) Shoot(world, p, end, at, pressed);   // pinned too long: get rid of it (in the cycle only from a real chance; else skate on)
    }

    /// <summary>
    /// W63: a carrier in his own zone with a forechecker on him and no pass he trusts weighs, once
    /// while pinned, firing the puck out. Hockey IQ sets the odds (a poor skater panics far more often
    /// than a smart one, who would rather skate it out or wait for a lane), a fumbled read doubles
    /// them, and a tighter check makes it likelier. The clear goes hard up the boards on his side:
    /// untouched from behind the red line it goes the length, icing. With icing off (Arcade) there is
    /// nothing to fear, so the odds are higher still. The penalty kill clears its own way (W22c).
    /// </summary>
    /// <returns>True when the carrier clears.</returns>
    bool Clear(World world, Player p, Brain brain, int end, Vector2 at, float nearest, PassOption? pass)
    {
        bool pinned = end * at.X < -RinkSpec.BlueLineX && nearest < 2.6f && (pass == null || pass.Value.Success < SafePass);
        if (!pinned || Same(brain.ClearRun, brain.GotAt) || p.Slot >= 0) return false;
        brain.ClearRun = brain.GotAt;
        if (Penalties.PowerPlay(world) is { } pp && pp != p.Side) return false;   // a killer clears with special teams

        var (outcome, iq) = Jitter.Roll(world, p, rng);
        float odds = Traits.Scale(iq, 0.12f, 0.015f) * (outcome == ReadOutcome.Fumble ? 2f : 1f) * (nearest < 1.4f ? 1.4f : 1f) * (world.Setup.Rules.Icing == IcingCall.Off ? 1.5f : 1f);
        if (rng.NextSingle() >= odds) return false;

        // up the boards on his side, a little off them so the puck runs along the wall, never at the far net
        float side = MathF.Abs(at.Y) > 1f ? MathF.Sign(at.Y) : rng.NextSingle() < 0.5f ? -1f : 1f;
        var aim = Vector2.Normalize(new Vector2(end, side * (0.12f + 0.2f * rng.NextSingle())));
        AiCommand.Shoot(world, p, AiLayer.Decisions, aim, 3, "clear");   // a quick snap, a wrist shot's speed: plenty to go the length
        var tally = Decisions.Tally(world, p.Side);
        tally.Clears++;
        return true;
    }

    /// <summary>
    /// W63: a carrier coming up to the attacking blue line with a teammate already wholly over it
    /// decides once whether to hold up short of the line, so the teammate can tag up. A smart skater
    /// with time usually does; a poor one, a fumbled read or a defender on him and he carries on in,
    /// offside. Holding, he curls across the ice short of the line until everyone is back onside or
    /// his patience runs out. With offside off (Arcade) nobody holds up.
    /// </summary>
    void HoldLine(World world, Player p, Brain brain)
    {
        if (!world.Setup.Rules.Offside || p.Action != PlayerAction.None || brain.ShootSteps > 0) return;
        int end = PuckHandling.AttackEnd(world, p.Side);
        var at = Flat(world.Puck.Position);
        float toLine = RinkSpec.BlueLineX - end * at.X;
        if (toLine is < 0.2f or > HoldFrom) return;
        bool ahead = world.OnIce(p.Side).Any(m => m != p && !m.IsGoalie && OffsideRule.PlayerInZone(m.Position, end));
        if (!ahead) return;

        if (!Same(brain.HoldRun, brain.GotAt))
        {
            if (end * p.Velocity.X < 0.5f) return;   // not going in: nothing to hold up for yet
            brain.HoldRun = brain.GotAt;
            brain.HoldUntil = float.NegativeInfinity;
            Decisions.Tally(world, p.Side).HoldLooks++;
            var (outcome, iq) = Jitter.Roll(world, p, rng);
            bool pressed = world.OnIce(p.Side == Side.Home ? Side.Away : Side.Home)
                .Any(o => !o.IsGoalie && Vector3.Distance(o.Position, p.Position) < 2.6f);
            float odds = Traits.Scale(iq, 0.3f, 0.98f) * (pressed ? 0.6f : 1f);
            if (outcome == ReadOutcome.Fumble || rng.NextSingle() >= odds) return;
            brain.HoldUntil = world.Time + HoldFor;
            Decisions.Tally(world, p.Side).HoldUps++;
        }
        if (world.Time > brain.HoldUntil) return;

        // curl across the ice a couple of metres short of the line
        float across = MathF.Abs(at.Y) > 6f ? -MathF.Sign(at.Y) : at.Y >= 0 ? 1f : -1f;
        var spot = TeamShape.Inside(new Vector2(end * (RinkSpec.BlueLineX - 2.2f), at.Y + across * 4f), 1.5f);
        AiCommand.Steer(world, p, AiLayer.Decisions, new PlayerCommand { Move = Steering.Toward(p, spot, 1f).Move }, "hold up at the line");
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

            // W22a: a pass into a defender's skates is lost as surely as one into his stick
            if (!o.IsGoalie) seen.Add((Flat(o.Position) + off, SkatingModel.TopSpeed(o.Data.Traits, o.Energy)));
        }
        return seen;
    }

    /// <summary>The best pass the carrier sees, or null with nobody to pass to.</summary>
    internal static PassOption? BestPass(World world, Player p, int end, Vector2 at, List<(Vector2 At, float Speed)> seen, int smart, bool cycling)
    {
        PassOption? best = null;
        float vision = Choices.Vision(smart);
        foreach (var mate in world.OnIce(p.Side))
        {
            if (mate == p || mate.IsGoalie || mate.Action == PlayerAction.Fallen) continue;
            var (target3, speed) = PuckHandling.Lead(world.Puck.Position, mate, smart);
            var target = Flat(target3);
            float d = Vector2.Distance(at, target);
            if (d < Choices.ShortestPass || d > vision) continue;
            if (smart >= 50 && Choices.Offside(end, at, Flat(mate.Position))) continue;
            if (Cycle.ThroughNet(at, target)) continue;   // W37's cage stops a pass through it

            // the stick has to point so puck handling picks this teammate; asked without touching his command
            if (PuckHandling.PickReceiver(world, p, Vector2.Normalize(target - at)) != mate) continue;

            float success = Choices.LaneSuccess(at, target, speed, seen, smart);
            float value = success * Choices.SpotValue(end, target) * (0.5f + 0.5f * Choices.Room(target, seen)) - (1f - success) * (cycling ? Choices.CycleTurnover : 0.08f);
            if (best == null || value > best.Value.Value) best = new PassOption(mate, target, success, value);
        }
        return best;
    }

    void Shoot(World world, Player p, int end, Vector2 at, bool pressed)
    {
        var toNet = new Vector2(end * RinkSpec.GoalLineX, 0) - at;
        float distance = toNet.Length();
        var goalie = world.OnIce(p.Side == Side.Home ? Side.Away : Side.Home).FirstOrDefault(o => o.IsGoalie);
        int side = goalie != null && StickAndPuck.Goalie.GoalieModel.WeakSide(goalie, end) is var weak and not 0 && rng.NextSingle() < p.Data.Traits.Iq ? weak : Choices.ShotSide(at, goalie?.Position.Z);
        var roll = Jitter.Roll(world, p, rng).Outcome;

        // an accurate shooter points the stick at the side of the net to beat the goalie; anyone
        // else, and any slap shot, just aims at the net and lets the shot find the far side
        bool slap = distance > 13f && !pressed && rng.NextSingle() < 0.75f;
        var aim = Vector2.Normalize(new Vector2(end * 0.8f, side * 0.6f));
        bool picksCorner = !slap && (Traits.Unit(p.Data.Traits.ShotAccuracy) >= 0.75f || roll == ReadOutcome.Lucky);
        if (roll == ReadOutcome.Fumble && !slap)
        {
            // W22a: misread the goalie and pick the side he covers
            aim = Vector2.Normalize(new Vector2(end * 0.8f, -side * 0.6f));
            picksCorner = true;
        }
        if (!picksCorner || Vector2.Dot(aim, Vector2.Normalize(toNet)) < MathF.Cos(70f * MathF.PI / 180f)) aim = Vector2.Normalize(toNet);

        AiCommand.Shoot(world, p, AiLayer.Decisions, aim, slap ? 28 + rng.Next(22) : 0, slap ? "slap shot" : "wrist shot");
    }

    /// <summary>Makes a pass; a fumbled execution holds it a beat too long, so the lane may close and the receiver skate on.</summary>
    void Pass(World world, Player p, Brain brain, PassOption pass, Vector2 at)
    {
        if (Jitter.Roll(world, p, rng).Outcome == ReadOutcome.Fumble)
        {
            brain.LatePass = pass;
            brain.PassAt = world.Time + 0.2f + 0.3f * rng.NextSingle();
            return;
        }
        Release(world, p, brain, pass, at);
    }

    /// <summary>Lets a fumbled pass go once its beat is over, at the receiver as he is then, with the old read of the lane.</summary>
    static void LatePass(World world, Player p, Brain brain)
    {
        var pass = brain.LatePass!.Value;
        if (p.Action != PlayerAction.None || !pass.Receiver.OnIce) { brain.LatePass = null; return; }
        if (world.Time < brain.PassAt) return;
        brain.LatePass = null;
        var at = Flat(world.Puck.Position);
        var (target, _) = PuckHandling.Lead(world.Puck.Position, pass.Receiver, p.Data.Traits.HockeyIq);
        if (Vector2.DistanceSquared(Flat(target), at) < 1f) return;
        Release(world, p, brain, pass with { Target = Flat(target) }, at);
    }

    static void Release(World world, Player p, Brain brain, PassOption pass, Vector2 at)
    {
        brain.PassTo = pass.Receiver.Id;
        AiCommand.Act(world, p, p.Command with { Move = Vector2.Normalize(pass.Target - at), Pass = true }, "pass");
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
        float along = Intercept(p, blade, from, dir, v.Length()) ?? Math.Max(0.5f, Vector2.Dot(blade - from, dir));
        var meet = TeamShape.Inside(from + dir * along, 0.6f);
        AiCommand.Steer(world, p, AiLayer.Decisions, Steering.Toward(p, Steering.BodyForBlade(p, meet), 0.6f), "meet the pass");
        return true;
    }

    /// <summary>
    /// W66: the earliest point along the puck's slide the receiver's blade can reach before the puck
    /// does, in metres from the puck, or null when he cannot get to it in time anywhere. Going to
    /// the point across from where he stands instead braked him sideways while a pass led ahead of
    /// him ran on, so the more a smart passer led him the more passes he missed.
    /// </summary>
    static float? Intercept(Player p, Vector2 blade, Vector2 from, Vector2 dir, float speed)
    {
        const float step = 1 / 60f;
        float top = SkatingModel.TopSpeed(p.Data.Traits, p.Energy), along = 0f, t = 0f;
        while (speed > 0.1f && t < 5f)
        {
            along += speed * step;
            speed -= (Physics.PuckTuning.IceFriction + Physics.PuckTuning.Drag * speed) * step;
            t += step;
            if (along >= 0.5f && Vector2.Distance(blade, from + dir * along) <= top * t)
            {
                return along;
            }
        }
        return null;
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
        AiCommand.Steer(world, p, AiLayer.Decisions, Steering.Toward(p, TeamShape.Inside(puck, SkatingModel.BodyRadius), 1.5f), "pounce");
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
        AiCommand.Steer(world, p, AiLayer.Decisions, Steering.Toward(p, new Vector2(end * (RinkSpec.GoalLineX - 2.4f), z), 0.8f, sprint: true), "crash the net");
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
        AiCommand.Steer(world, p, AiLayer.Decisions, Steering.Toward(p, Flat(lead), 0.1f, sprint: true), "line up a hit");
        if (CheckingModel.Target(world, p) == carrier)
        {
            AiCommand.Act(world, p, p.Command with { Check = true }, "hit");
            brain.HitUntil = float.NegativeInfinity;
            board.Tally[(int)p.Side].Checks++;
        }
    }

    /// <summary>Gets whether a remembered possession is the current one (both are its <see cref="Brain.GotAt"/>).</summary>
    static bool Same(float run, float gotAt)
    {
        return MathF.Abs(run - gotAt) < 1e-4f;
    }

    static Vector2 Flat(Vector3 v)
    {
        return new Vector2(v.X, v.Z);
    }
}
