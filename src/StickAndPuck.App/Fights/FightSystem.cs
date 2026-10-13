using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Hud;
using StickAndPuck.App.Players;
using StickAndPuck.App.Rules;

namespace StickAndPuck.App.Fights;

/// <summary>A fight may start after a hard check; accept or decline, punch and block timing, then the
/// rule set's fight penalty. See <see cref="Fighting"/> for the rules. Runs after the penalty rules, so it sees
/// this step's checks and the whistle it blows stops play before the referee and the bench look.
/// While the fight lasts the match stays stopped (no clock, no faceoff) and the dead puck lies by the
/// fighters, so the camera stays on them. Draws the offer prompt and the fight bar at the foot of the HUD.</summary>
internal sealed partial class FightSystem : ISystem, IDisposable
{
    static readonly Color Bar = HudArt.Hex("#555f6b");
    static readonly Color Sweet = HudArt.Hex("#2c6e4a");
    static readonly Color Centre = HudArt.Hex("#56965c");
    static readonly Color Marker = HudArt.Hex("#f9da93");
    static readonly Color Health = HudArt.Hex("#f2c14e");
    static readonly Color Hurt = HudArt.Hex("#a34e4d");

    Maybe<HudArt> art;

    public void Start(World world)
    {
        if (MatchFlow.IsRunning(world)) art = HudArt.Load();
    }

    public void Update(World world, float dt)
    {
        if (!MatchFlow.IsRunning(world)) return;
        var s = Fighting.State(world);
        if (float.IsNaN(s.FirstDrop) && world.Match.Phase == MatchPhase.Play) s.FirstDrop = world.Time;
        if (s.Fight.TryGet(out var fight))
        {
            Run(world, s, fight, dt);
            return;
        }
        if (!s.Offer.HasValue) Watch(world, s);
        if (!s.Offer.HasValue) Script(world, s);
        if (s.Offer.TryGet(out var offer)) Resolve(world, s, offer);
    }

    /// <summary>Every hard check this step may drop the gloves.</summary>
    static void Watch(World world, FightState s)
    {
        foreach (var check in world.Events.Of<CheckLanded>())
        {
            if (check.Strength < CheckingModel.HardHit) continue;
            var victim = world.Players
                .Where(p => p.Side != check.Side && p.OnIce && !p.IsGoalie)
                .MinBy(p => Vector3.DistanceSquared(p.Position, check.Position));
            if (victim is not { } || Vector3.DistanceSquared(victim.Position, check.Position) > 1f) continue;
            if (s.Rng.NextSingle() >= s.OfferChance) continue;
            if (Fighting.Offer(world, check.CheckerId, victim.Id)) return;
        }
    }

    /// <summary>The fight scene's scripted fight: a moment after the drop, the two centres drop the gloves.</summary>
    static void Script(World world, FightState s)
    {
        if (float.IsNaN(s.ScriptAfterDrop) || float.IsNaN(s.FirstDrop) || world.Time - s.FirstDrop < s.ScriptAfterDrop) return;
        s.ScriptAfterDrop = float.NaN;
        var home = world.OnIce(Side.Home).Where(p => !p.IsGoalie).MinBy(p => p.Position.LengthSquared());
        var away = world.OnIce(Side.Away).Where(p => !p.IsGoalie).MinBy(p => p.Position.LengthSquared());
        if (home is not { } || away is not { }) return;
        // the AI says yes to this one; a human still has to accept
        var answer = s.ForceAccept;
        s.ForceAccept = true;
        Fighting.Offer(world, away.Id, home.Id);
        s.ForceAccept = answer;
    }

    /// <summary>Waits for humans to accept; starts the fight when both are willing, or lets it pass.</summary>
    static void Resolve(World world, FightState s, FightOffer offer)
    {
        if (!Fighting.CanFight(world, offer.CheckerId, offer.VictimId)
            || Vector3.Distance(world.Players[offer.CheckerId].Position, world.Players[offer.VictimId].Position) > 6f)
        {
            s.Offer = Maybe<FightOffer>.None;
            return;
        }
        bool aiSaidNo = false;
        for (int i = 0; i < 2; i++)
        {
            if (offer.Slots[i] < 0) aiSaidNo |= !offer.Accepted[i];
            else if (world.Controlled(offer.Slots[i]).Is(c => c.Command.AcceptFight)) offer.Accepted[i] = true;
        }
        bool humans = offer.Slots[0] >= 0 || offer.Slots[1] >= 0;
        bool expired = world.Time - offer.Time >= FightTuning.OfferSeconds;
        if (offer.Accepted[0] && offer.Accepted[1])
        {
            s.Offer = Maybe<FightOffer>.None;
            Start(world, s, offer.CheckerId, offer.VictimId);
        }
        else if (!humans || expired || (aiSaidNo && (offer.Accepted[0] || offer.Accepted[1])))
        {
            s.Offer = Maybe<FightOffer>.None;
            s.Declined++;
            s.Log(world, "no fight: declined");
        }
    }

    static void Start(World world, FightState s, int checkerId, int victimId)
    {
        var a = world.Players[checkerId];
        var b = world.Players[victimId];
        var mid = (a.Position + b.Position) / 2;
        var spot = new Vector3(
            Math.Clamp(mid.X, -RinkSpec.HalfLength + 4f, RinkSpec.HalfLength - 4f), 0,
            Math.Clamp(mid.Z, -RinkSpec.HalfWidth + 1.5f, RinkSpec.HalfWidth - 1.5f));
        if (!MatchFlow.Whistle(world, MatchFlow.NearestDot(spot))) return;

        var fight = new Fight(new Fighter(a.Id, Fighting.HumanSlot(world, a.Side)), new Fighter(b.Id, Fighting.HumanSlot(world, b.Side)), spot);
        s.Fight = fight;
        s.Started++;
        s.LastFight = world.Time;
        // the checker squares up on the side he came from
        float dir = a.Position.X <= b.Position.X ? -1 : 1;
        Place(a, spot + new Vector3(dir * FightTuning.HalfGap, 0, 0), spot);
        Place(b, spot - new Vector3(dir * FightTuning.HalfGap, 0, 0), spot);
        foreach (var f in fight.Both) SetClip(world, f, "grab");

        var pk = world.Puck;
        pk.Carrier = -1;
        pk.Velocity = Vector3.Zero;
        pk.Position = spot + new Vector3(0, 0, 1.2f);
        world.Events.Publish(new FightStarted(world.Time, b.Side, b.Id, spot));
        s.Log(world, "fight!");
    }

    static void Place(Player p, Vector3 at, Vector3 facing)
    {
        p.Position = at;
        p.Velocity = Vector3.Zero;
        p.Heading = SimMath.Atan2(facing.X - at.X, facing.Z - at.Z);
        if (p.Action != PlayerAction.Fight) p.Begin(PlayerAction.Fight);
    }

    static void Run(World world, FightState s, Fight fight, float dt)
    {
        var m = world.Match;
        if (m.Phase != MatchPhase.Stopped)
        {
            // something ended the stoppage under us (a new period, the match end): the linesmen step in, nobody is boxed
            End(world, s, fight, penalise: false);
            return;
        }
        MatchFlow.State(world).Timer = MatchTuning.StoppageSeconds;     // hold the faceoff until the fight is over
        foreach (var f in fight.Both)
        {
            var p = world.Players[f.PlayerId];
            Place(p, fight.Spot + (fight.Spot.X < p.Position.X ? 1 : -1) * new Vector3(FightTuning.HalfGap, 0, 0), fight.Spot);
        }
        BackOff(world, fight, dt);

        fight.StageTime += dt;
        switch (fight.Stage)
        {
            case FightStage.Grab:
                if (fight.StageTime >= FightTuning.GrabSeconds) BeginExchange(world, s, fight);
                break;
            case FightStage.Exchange:
                Exchange(world, s, fight);
                break;
            default:
                if (fight.StageTime >= FightTuning.EndSeconds) End(world, s, fight, penalise: true);
                break;
        }
    }

    /// <summary>Everyone else gives the fighters room. A skater inside the ring around the fight glides
    /// out to its edge and faces the fighters; nobody is pulled from farther away.</summary>
    static void BackOff(World world, Fight fight, float dt)
    {
        foreach (var p in world.Players)
        {
            if (!p.OnIce || p.IsGoalie || fight.Of(p.Id).HasValue) continue;
            var away = p.Position - fight.Spot;
            away.Y = 0;
            float d = away.Length();
            if (d >= FightTuning.Room) continue;
            var dir = d > 0.01f ? away / d : new Vector3(p.Side == Side.Home ? -1 : 1, 0, 0);
            // undo any skating in toward the fight this step, then glide out
            float inward = Math.Max(0, -Vector3.Dot(p.Velocity, dir));
            float step = Math.Min(FightTuning.Room - d, (FightTuning.BackOffSpeed + inward) * dt);
            var to = p.Position + (dir * step);
            p.Position = new Vector3(
                Math.Clamp(to.X, -RinkSpec.HalfLength + 1f, RinkSpec.HalfLength - 1f), p.Position.Y,
                Math.Clamp(to.Z, -RinkSpec.HalfWidth + 0.6f, RinkSpec.HalfWidth - 0.6f));
            p.Velocity = dir * FightTuning.BackOffSpeed;
            p.Heading = SimMath.Atan2(-dir.X, -dir.Z);
        }
    }

    /// <summary>The fight is over: the rule set's fight penalty is handed out and the faceoff follows in the
    /// loser's end. Arcade: the loser serves two minutes, the winner stays on. SHL and NHL: both fighters
    /// get a 5 minute major, coincidental and substituted so neither club plays short; under SHL both also
    /// get a game misconduct and leave the squad.</summary>
    static void End(World world, FightState s, Fight fight, bool penalise)
    {
        s.Fight = Maybe<Fight>.None;
        s.Last = fight;
        s.LastEnded = world.Time;
        foreach (var f in fight.Both)
        {
            var p = world.Players[f.PlayerId];
            if (p.Action == PlayerAction.Fight) p.Begin(PlayerAction.None);
        }
        if (!penalise || fight.LoserId < 0) return;
        var loser = world.Players[fight.LoserId];
        var book = Penalties.Book(world);
        var rule = world.Setup.Rules.FightPenalty;
        if (rule == FightPenalty.LoserMinor)
        {
            Penalties.Assess(world, book, new DelayedPenalty(loser.Side, loser.Id, FightTuning.Infraction, world.Time));
        }
        else
        {
            var severity = rule == FightPenalty.MajorAndEjection ? Severity.MajorAndGameMisconduct : Severity.Major;
            foreach (var f in fight.Both)
            {
                var p = world.Players[f.PlayerId];
                Penalties.Assess(world, book, new DelayedPenalty(p.Side, p.Id, FightTuning.Infraction, world.Time, severity), substitute: true);
            }
        }
        MatchFlow.State(world).Dot = Penalties.FaceoffDot(world, loser.Side);
    }

    static void SetClip(World world, Fighter f, string clip)
    {
        f.Clip = clip;
        f.ClipSince = world.Time;
    }

    public void Dispose()
    {
        if (art.TryGet(out var hud)) hud.Unload();
        art = Maybe<HudArt>.None;
    }
}
