using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players;

namespace StickAndPuck.App.Rules;

internal sealed partial class OvertimeRules
{
    void ShootoutStep(World world, OvertimeState s, float dt)
    {
        if (!s.Shootout.HasValue) Begin(world, s);
        var so = s.Live;
        switch (s.Stage)
        {
            case ShootoutStage.Break:
                if (!s.First.IsSet())
                {
                    // The human home club picks; nothing else runs until it does (or the time runs out)
                    s.Timer -= dt;
                    var pick = ShootoutOrder.Pick(world);
                    s.First = pick.IsSet() ? pick : s.Timer <= -ShootoutOrder.ChooseSeconds ? SideOrNone.Home : SideOrNone.None;
                    if (s.First.TryGetSide(out var picked)) { s.Timer = 0.5f; Log(world, $"{world.Setup.Home.ShortName} choose to shoot {(picked == Side.Home ? "first" : "second")}"); }
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
                if (so.Decided(so.Rounds).TryGetSide(out var winner)) End(world, s, winner);
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
        if (!s.First.IsSet()) s.Timer = 0;
        world.Puck.Live = false;
        foreach (var p in world.Players.Where(p => !p.IsGoalie)) Bench(p);
        foreach (var side in new[] { Side.Home, Side.Away })
        {
            s.Order[(int)side] = [.. world.Players
                .Where(p => p.Side == side && !p.IsGoalie)
                .OrderByDescending(p => (2 * p.Data.Traits.ShotAccuracy) + p.Data.Traits.PuckControl + p.Data.Traits.ShotPower)
                .ThenBy(p => p.Id)];
        }

        Log(world, $"shootout, {ShootoutOrder.Shooters(world.Setup)} shooters each, {(s.First.TryGetSide(out var f) ? $"{world.Setup.Club(f).ShortName} first" : $"{world.Setup.Home.ShortName} to choose")}");
    }

    /// <summary>Brings out the next shooter: the clubs in turn, the first club opening every round, round after round through the order.</summary>
    static void NextShooter(World world, OvertimeState s)
    {
        var so = s.Live;
        if (so.ShooterId >= 0) Bench(world.Players[so.ShooterId]);
        var side = ShootoutOrder.Next(so, s.First.TryGetSide(out var first) ? first : Side.Home);
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
        var shooter = world.Players[s.Live.ShooterId];
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
        var shooter = world.Players[s.Live.ShooterId];
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
        world.Events.Publish(new PuckDropped(world.Time, RinkSpec.CentreDot));
    }

    /// <summary>Watches one attempt to its end; skates and shoots for an AI shooter.</summary>
    void Attempt(World world, OvertimeState s, float dt)
    {
        var so = s.Live;
        var shooter = world.Players[so.ShooterId];
        var pk = world.Puck;
        s.AttemptTime += dt;

        // physics ran earlier this step, so a goal shows up here
        if (world.Events.Of<GoalScored>().Any(e => e.Side == shooter.Side)) { Finish(world, s, true); return; }

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
        world.Events.Publish(new ShotTaken(world.Time, shooter.Side, shooter.Id, from, velocity.Length()));
    }

    static void Finish(World world, OvertimeState s, bool scored)
    {
        var so = s.Live;
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
        var so = s.Live;
        so.Winner = winner.OrNone();
        so.ShooterId = -1;
        s.Stage = ShootoutStage.Done;
        var m = world.Match;
        m.Score[(int)winner]++;
        MatchRules.EndMatch(world, MatchFlow.State(world));
        Log(world, $"final after the shootout, {world.Setup.Club(winner).ShortName} win it");
    }
}
