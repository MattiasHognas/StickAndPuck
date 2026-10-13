using System.Numerics;
using System.Runtime.CompilerServices;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Rules;

/// <summary>
/// W7c: a tie after regulation goes to overtime and then a shootout as the mode's <see cref="OvertimeFormat"/>
/// says (W19): SHL 3 on 3 then a shootout in the season, 5 on 5 until a goal in the playoffs.
/// <list type="bullet">
/// <item>Overtime: when W7a hands over (<see cref="MatchPhase.Overtime"/>), each club keeps the
/// format's skaters (centre and two defence where it can, for 3 on 3), the rest go to the bench, and
/// after a short break <see cref="MatchFlow.StartPeriod"/> starts a sudden death period of
/// <see cref="PeriodLength"/>. A goal ends the match there (W7a); a scoreless period hands over to
/// the shootout, or to another overtime period when the format plays on until a goal.</item>
/// <item>Shootout: every skater leaves the ice and each goalie stays in their own net. Clubs take turns,
/// the rules' first club first (W45, <see cref="ShootoutOrder"/>: SHL away, NHL home chooses), each shooter
/// alone from centre with the puck against the other club's goalie; the rules' shooters each, then sudden
/// death rounds opened by the same club. An attempt ends on a goal,
/// the goalie covering the puck, the puck going dead or behind the goal line, or time running out; no rebounds.
/// A human slot shoots for its club; otherwise the shooter skates in and shoots by itself.</item>
/// <item>The winner gets one goal added to the final score (as in the SHL) and MatchEnd is published.</item>
/// </list>
/// <see cref="Shootout"/> gives the tally for the HUD.
/// </summary>
internal sealed class OvertimeRules : ISystem
{
    static readonly ConditionalWeakTable<World, OvertimeState> States = new();

    /// <summary>The dice seed without a match seed (W67).</summary>
    internal const int Fallback = 9;

    readonly Random rng;

    public OvertimeRules()
        : this(Seeds.Dice(Seeds.Environment, "overtime", Fallback))
    {
    }

    /// <summary>Initializes the rules with their own randomness, so tests repeat.</summary>
    /// <param name="rng">Randomness for how AI shooters skate in and shoot.</param>
    public OvertimeRules(Random rng)
    {
        this.rng = rng;
    }

    /// <summary>Gets or sets the length of an overtime period in game seconds for formats that give none (the SHL 3 on 3); scenes shorten it.</summary>
    public static float PeriodSeconds { get; set; } = MatchTuning.OvertimeSeconds;

    /// <summary>Gets the length of an overtime period in this match: the format's own, or <see cref="PeriodSeconds"/>.</summary>
    /// <param name="world">The match.</param>
    /// <returns>Game seconds.</returns>
    public static float PeriodLength(World world)
    {
        return OvertimeFormat.Of(world.Setup).PeriodSeconds ?? PeriodSeconds;
    }

    /// <summary>Gets the shootout of a match, or null before it starts.</summary>
    /// <param name="world">The match.</param>
    /// <returns>The tally.</returns>
    public static ShootoutState? Shootout(World world)
    {
        return States.TryGetValue(world, out var s) ? s.Shootout : null;
    }

    public void Update(World world, float dt)
    {
        if (!MatchFlow.IsRunning(world)) return;
        var s = States.GetOrCreateValue(world);
        if (world.Match.Phase == MatchPhase.Overtime) Overtime(world, s, dt);
        else if (world.Match.Phase == MatchPhase.Shootout) ShootoutStep(world, s, dt);
    }

    public void DrawHud(World world, RenderContext render)
    {
        if (States.TryGetValue(world, out var st) && st is { Shootout: not null, First: null }) DrawChoice(world);

        // a plain shootout line under W7a's debug line, until W10's scorebug shows the tally
        if (!MatchRules.ShowLine || Shootout(world) is not { } so) return;
        string Marks(Side side) => string.Concat(so.Shots.Where(x => x.Side == side).Select(x => x.Scored ? "o" : "x"));
        string text = $"SO  {world.Setup.Home.ShortName} {Marks(Side.Home)}   {world.Setup.Away.ShortName} {Marks(Side.Away)}";
        DrawRectangle(4, 236, MeasureText(text, 10) + 8, 14, new Raylib_cs.Color(24, 19, 31, 255));
        DrawText(text, 8, 238, 10, new Raylib_cs.Color(239, 224, 194, 255));
    }

    /// <summary>W45: the home human's choice, blinking at the top until they pick.</summary>
    static void DrawChoice(World world)
    {
        string text = $"{world.Setup.Home.ShortName} SHOOTS   {Key(world, GameAction.Shoot)} FIRST   {Key(world, GameAction.Pass)} SECOND";
        int w = MeasureText(text, 10);
        DrawRectangle((Game.Width - w) / 2 - 4, 48, w + 8, 14, new Raylib_cs.Color(24, 19, 31, 255));
        if ((int)(world.Time * 2) % 2 == 0) DrawText(text, (Game.Width - w) / 2, 50, 10, new Raylib_cs.Color(239, 224, 194, 255));
    }

    static string Key(World world, GameAction action)
    {
        return ShootoutOrder.HomeHuman(world.Setup) is InputDevice.KeyboardMouse ? KeyBindings.Of(world.Settings, action).Key.ToString().ToUpperInvariant() : action == GameAction.Shoot ? "SHOOT" : "PASS";
    }

    /// <summary>The break after a tied period: the format's skaters each, then the overtime faceoff of the next period.</summary>
    static void Overtime(World world, OvertimeState s, float dt)
    {
        if (!s.InBreak)
        {
            s.InBreak = true;
            s.Timer = OvertimeTuning.BreakSeconds;
            world.Puck.Live = false;
            int skaters = OvertimeFormat.Of(world.Setup).Skaters;
            KeepSkaters(world, skaters);
            int number = world.Match.Period - world.Setup.Periods + 1;
            Log(world, $"overtime{(number > 1 ? $" {number}" : "")}, {skaters} on {skaters}");
        }

        s.Timer -= dt;
        if (s.Timer > 0) return;
        s.InBreak = false;
        MatchFlow.StartPeriod(world, world.Match.Period + 1, PeriodLength(world));
    }

    /// <summary>Sends all but <see cref="OvertimeTuning.OvertimeSkaters"/> skaters of each club to the bench, keeping a human's control on the ice.</summary>
    internal static void ThreeOnThree(World world)
    {
        KeepSkaters(world, OvertimeTuning.OvertimeSkaters);
    }

    /// <summary>Sends all but <paramref name="count"/> skaters of each club to the bench (centre and defence stay first), keeping a human's control on the ice.</summary>
    internal static void KeepSkaters(World world, int count)
    {
        foreach (var side in new[] { Side.Home, Side.Away })
        {
            var skaters = world.OnIce(side).Where(p => !p.IsGoalie).ToList();
            if (skaters.Count <= count) continue;
            var keep = new List<Player>();
            foreach (var pos in new[] { PlayingPosition.C, PlayingPosition.D, PlayingPosition.D })
                if (keep.Count < count && skaters.Find(p => p.Data.Position == pos && !keep.Contains(p)) is { } found) keep.Add(found);
            keep.AddRange(skaters.Where(p => !keep.Contains(p)).Take(count - keep.Count));

            foreach (var p in skaters.Where(p => !keep.Contains(p)))
            {
                if (p.Slot >= 0 && keep.Count > 0 && keep[0].Slot < 0) keep[0].Slot = p.Slot;
                Bench(p);
            }
        }
    }

    void ShootoutStep(World world, OvertimeState s, float dt)
    {
        if (s.Shootout == null) Begin(world, s);
        var so = s.Shootout!;
        switch (s.Stage)
        {
            case ShootoutStage.Break:
                if (s.First == null)
                {
                    // W45: the human home club picks; nothing else runs until it does (or the time runs out)
                    s.Timer -= dt;
                    s.First = ShootoutOrder.Pick(world) ?? (s.Timer <= -ShootoutOrder.ChooseSeconds ? Side.Home : null);
                    if (s.First is { } picked) { s.Timer = 0.5f; Log(world, $"{world.Setup.Home.ShortName} choose to shoot {(picked == Side.Home ? "first" : "second")}"); }
                    break;
                }

                if (Wait(s, dt)) NextShooter(world, s);
                break;
            case ShootoutStage.Ready:
                Hold(world, s);
                if (Wait(s, dt)) Go(world, s);
                break;
            case ShootoutStage.Attempt:
                Attempt(world, s, dt);
                break;
            case ShootoutStage.Result:
                if (!Wait(s, dt)) break;
                if (so.Decided(so.Rounds) is { } winner) End(world, s, winner);
                else NextShooter(world, s);
                break;
            default:
                break;
        }
    }

    /// <summary>Clears the ice for the shootout and picks each club's shooting order: best hands first.</summary>
    static void Begin(World world, OvertimeState s)
    {
        s.Shootout = new ShootoutState { Rounds = ShootoutOrder.Shooters(world.Setup) };
        s.Stage = ShootoutStage.Break;
        s.Timer = OvertimeTuning.BreakSeconds;
        s.InBreak = false;
        s.First = ShootoutOrder.First(world.Setup);
        if (s.First == null) s.Timer = 0;
        world.Puck.Live = false;
        foreach (var p in world.Players.Where(p => !p.IsGoalie)) Bench(p);
        foreach (var side in new[] { Side.Home, Side.Away })
        {
            s.Order[(int)side] = [.. world.Players
                .Where(p => p.Side == side && !p.IsGoalie)
                .OrderByDescending(p => (2 * p.Data.Traits.ShotAccuracy) + p.Data.Traits.PuckControl + p.Data.Traits.ShotPower)
                .ThenBy(p => p.Id)];
        }

        Log(world, $"shootout, {ShootoutOrder.Shooters(world.Setup)} shooters each, {(s.First is { } f ? $"{world.Setup.Club(f).ShortName} first" : $"{world.Setup.Home.ShortName} to choose")}");
    }

    /// <summary>Brings out the next shooter: the clubs in turn, the first club opening every round, round after round through the order.</summary>
    static void NextShooter(World world, OvertimeState s)
    {
        var so = s.Shootout!;
        if (so.ShooterId >= 0) Bench(world.Players[so.ShooterId]);
        var side = ShootoutOrder.Next(so, s.First ?? Side.Home);
        var order = s.Order[(int)side];
        if (order.Count == 0)
        {
            // a club with no skaters (should not happen) misses its turn
            so.Add(new ShootoutShot(side, -1, false));
            s.Stage = ShootoutStage.Result;
            s.Timer = 0;
            return;
        }

        var shooter = order[s.Next[(int)side]++ % order.Count];
        so.ShooterId = shooter.Id;
        so.Shooting = side;
        shooter.OnIce = true;
        shooter.Begin(PlayerAction.None);

        // a human slot playing for this club shoots
        for (int slot = 0; slot < world.Setup.Inputs.Count; slot++)
        {
            var input = world.Setup.Inputs[slot];
            if (input.Device != InputDevice.None && input.Side == side) { shooter.Slot = slot; break; }
        }

        s.Stage = ShootoutStage.Ready;
        s.Timer = OvertimeTuning.ReadySeconds;
        Hold(world, s);
    }

    /// <summary>Keeps the shooter and the dead puck at centre until the attempt starts.</summary>
    static void Hold(World world, OvertimeState s)
    {
        var shooter = world.Players[s.Shootout!.ShooterId];
        int attack = PuckHandling.AttackEnd(world, shooter.Side);
        shooter.Position = RinkSpec.CentreDot - new Vector3(attack * 1.2f, 0, 0);
        shooter.Velocity = Vector3.Zero;
        shooter.Heading = attack > 0 ? MathF.PI / 2 : -MathF.PI / 2;
        var pk = world.Puck;
        pk.Live = false;
        pk.Carrier = -1;
        pk.Position = RinkSpec.CentreDot;
        pk.Velocity = Vector3.Zero;
    }

    /// <summary>Starts the attempt: the puck is live on the shooter's stick; an AI shooter picks a lane and a distance to shoot from.</summary>
    void Go(World world, OvertimeState s)
    {
        var shooter = world.Players[s.Shootout!.ShooterId];
        var pk = world.Puck;
        pk.Live = true;
        pk.Carrier = shooter.Id;
        pk.LastTouch = shooter.Id;
        pk.Position = PuckHandling.Blade(shooter);
        s.Stage = ShootoutStage.Attempt;
        s.AttemptTime = 0;
        s.LooseTime = 0;
        s.Lane = (rng.NextSingle() * 2 - 1) * OvertimeTuning.LaneWidth;
        s.ShootFrom = OvertimeTuning.ShootNear + (rng.NextSingle() * (OvertimeTuning.ShootFar - OvertimeTuning.ShootNear));
        s.Deke = 0;
        float deke = OvertimeTuning.DekeChance + (OvertimeTuning.DekeChanceBest - OvertimeTuning.DekeChance) * Traits.Unit(shooter.Data.Traits.PuckControl);
        if (rng.NextSingle() < deke)
        {
            // start wide on one side and cut across to the other
            s.Deke = (rng.Next(2) * 2) - 1;
            s.Lane = -s.Deke * 1.6f;
            s.ShootFrom = 1.8f;
        }
        world.Events.Publish(new MatchEvent(MatchEventKind.Faceoff, world.Time, shooter.Side, shooter.Id, RinkSpec.CentreDot));
    }

    /// <summary>Watches one attempt to its end; skates and shoots for an AI shooter.</summary>
    void Attempt(World world, OvertimeState s, float dt)
    {
        var so = s.Shootout!;
        var shooter = world.Players[so.ShooterId];
        var pk = world.Puck;
        s.AttemptTime += dt;

        // physics ran earlier this step, so a goal shows up here
        if (world.Events.Of(MatchEventKind.Goal).Any(e => e.Side == shooter.Side)) { Finish(world, s, true); return; }

        bool carried = pk.Carrier == shooter.Id;
        if (pk.Carrier >= 0 && !carried) { Finish(world, s, false); return; }  // the goalie covered it
        if (carried && s.LooseTime > 0) { Finish(world, s, false); return; }  // no rebounds
        if (!carried) s.LooseTime += dt;

        int attack = PuckHandling.AttackEnd(world, shooter.Side);
        bool behind = attack * pk.Position.X > RinkSpec.GoalLineX + 0.3f;
        bool dead = !carried && s.LooseTime > 0.3f && pk.Velocity.Length() < OvertimeTuning.DeadSpeed;
        bool away = !carried && s.LooseTime > 0.3f && attack * pk.Velocity.X < -1f;
        if (behind || dead || away || s.LooseTime > OvertimeTuning.LooseSeconds || s.AttemptTime > OvertimeTuning.AttemptSeconds)
        {
            Finish(world, s, false);
            return;
        }

        if (carried && shooter.Slot < 0) SkateIn(world, s, shooter, attack, dt);
    }

    /// <summary>An AI shooter skates at the net along its lane and shoots from its distance.</summary>
    void SkateIn(World world, OvertimeState s, Player shooter, int attack, float dt)
    {
        var traits = shooter.Data.Traits;
        float toLine = RinkSpec.GoalLineX - (attack * shooter.Position.X);
        var net = new Vector2(attack * RinkSpec.GoalLineX, 0);
        var here = new Vector2(shooter.Position.X, shooter.Position.Z);
        // a deke cuts across the crease and shoots to the side it is moving to, behind the goalie's shuffle
        bool cutting = s.Deke != 0 && toLine <= OvertimeTuning.DekeFrom;
        if (toLine <= s.ShootFrom || (cutting && s.Deke * shooter.Position.Z >= OvertimeTuning.DekeShootZ))
        {
            Shoot(world, shooter, net, here, s.Deke != 0 ? s.Deke : 0);
            return;
        }

        // skate the lane toward where the shot or the cut starts
        var target = cutting
            ? new Vector2(attack * (RinkSpec.GoalLineX - 1.5f), s.Deke * 1.1f)
            : new Vector2(attack * (RinkSpec.GoalLineX - Math.Max(s.ShootFrom, s.Deke != 0 ? OvertimeTuning.DekeFrom : 0) + 1f), s.Lane);
        var dir = Vector2.Normalize(target - here);
        float top = SkatingModel.TopSpeed(traits, shooter.Energy) * OvertimeTuning.SkateIn;
        float speed = Math.Min(top, shooter.Speed + (SkatingModel.Push(traits, shooter.Energy) * dt));
        shooter.Velocity = new Vector3(dir.X * speed, 0, dir.Y * speed);
        shooter.Heading = SimMath.Atan2(dir.X, dir.Y);
    }

    /// <summary>A wrist shot at a corner: the far side when coming in from a wing, or the given side after a deke.</summary>
    void Shoot(World world, Player shooter, Vector2 net, Vector2 here, int side)
    {
        var toNet = Vector2.Normalize(net - here);
        shooter.Heading = SimMath.Atan2(toNet.X, toNet.Y);
        // the puck leaves the blade lifted, so the stick that fired it cannot take it straight back
        // (puck handling guards only the shots it fires itself)
        var pk = world.Puck;
        var from = pk.Position with { Y = PuckHandling.StickHeight + 0.02f };
        float corner = side != 0 ? side : MathF.Abs(from.Z) > 0.5f ? -MathF.Sign(from.Z) : (rng.Next(2) * 2) - 1;
        // aim like a human pushing the stick toward that corner
        shooter.Command = shooter.Command with { Move = Vector2.Normalize(toNet + new Vector2(0, corner * 0.8f)) };
        var velocity = PuckHandling.ShotVelocity(world, shooter, from, ShotKind.Wrist, PuckHandling.WristSpeed(shooter.Data.Traits), rng);
        shooter.Command = default;
        velocity.Y = Math.Max(velocity.Y, 1.5f);
        pk.Position = from;
        pk.Carrier = -1;
        pk.LastTouch = shooter.Id;
        pk.Velocity = velocity;
        shooter.Begin(PlayerAction.Shoot);
        world.Events.Publish(new MatchEvent(MatchEventKind.Shot, world.Time, shooter.Side, shooter.Id, from, velocity.Length()));
    }

    static void Finish(World world, OvertimeState s, bool scored)
    {
        var so = s.Shootout!;
        var shooter = world.Players[so.ShooterId];
        so.Add(new ShootoutShot(shooter.Side, shooter.Id, scored));
        world.Puck.Live = false;
        s.Stage = ShootoutStage.Result;
        s.Timer = OvertimeTuning.ResultSeconds;
        Log(world, $"shootout #{shooter.Data.Number} {world.Setup.Club(shooter.Side).ShortName} {(scored ? "scores" : "misses")}, {so.Goals(Side.Home)}-{so.Goals(Side.Away)}");
    }

    /// <summary>The shootout is decided: the winner gets one goal on the final score and the match ends.</summary>
    static void End(World world, OvertimeState s, Side winner)
    {
        var so = s.Shootout!;
        so.Winner = winner;
        so.ShooterId = -1;
        s.Stage = ShootoutStage.Done;
        var m = world.Match;
        m.Score[(int)winner]++;
        MatchRules.EndMatch(world, MatchFlow.State(world));
        Log(world, $"final after the shootout, {world.Setup.Club(winner).ShortName} win it");
    }

    static void Bench(Player p)
    {
        p.OnIce = false;
        p.Slot = -1;
        p.Velocity = Vector3.Zero;
        p.Command = default;
        p.Begin(PlayerAction.None);
    }

    static bool Wait(OvertimeState s, float dt)
    {
        s.Timer -= dt;
        return s.Timer <= 0;
    }

    static void Log(World world, string what)
    {
        if (!MatchFlow.State(world).Log) return;
        var m = world.Match;
        Console.WriteLine($"SAP: {what}: {world.Setup.Home.ShortName} {m.Score[0]}-{m.Score[1]} {world.Setup.Away.ShortName}");
    }
}

/// <summary>Where the shootout is.</summary>
internal enum ShootoutStage
{
    /// <summary>The break before the first shooter.</summary>
    Break,

    /// <summary>The shooter waits at centre.</summary>
    Ready,

    /// <summary>The attempt is on.</summary>
    Attempt,

    /// <summary>The pause after an attempt.</summary>
    Result,

    /// <summary>Decided.</summary>
    Done,
}

/// <summary>Overtime and shootout state for one world.</summary>
internal sealed class OvertimeState
{
    public bool InBreak { get; set; }

    public float Timer { get; set; }

    public ShootoutState? Shootout { get; set; }

    /// <summary>Gets or sets the club that shoots first (W45), or null while the home human chooses.</summary>
    public Side? First { get; set; }

    public ShootoutStage Stage { get; set; }

    /// <summary>Gets each club's shooting order, indexed by <see cref="Side"/>.</summary>
    public List<Player>[] Order { get; } = [[], []];

    /// <summary>Gets each club's next place in its order.</summary>
    public int[] Next { get; } = new int[2];

    public float AttemptTime { get; set; }

    /// <summary>Gets or sets how long the puck has been off the shooter's stick.</summary>
    public float LooseTime { get; set; }

    /// <summary>Gets or sets the AI shooter's lane (z).</summary>
    public float Lane { get; set; }

    /// <summary>Gets or sets the AI shooter's distance to the goal line when shooting.</summary>
    public float ShootFrom { get; set; }

    /// <summary>Gets or sets the side an AI shooter's deke cuts to (-1 or +1), or 0 for a straight shot.</summary>
    public int Deke { get; set; }
}
