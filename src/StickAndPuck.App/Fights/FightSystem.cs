using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Hud;
using StickAndPuck.App.Players;
using StickAndPuck.App.Rules;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Fights;

/// <summary>
/// W15: a fight may start after a hard check; accept or decline, punch and block timing, then the
/// rule set's fight penalty (W48). See <see cref="Fighting"/> for the rules. Runs after the penalty rules, so it sees
/// this step's checks and the whistle it blows stops play before the referee and the bench look.
/// While the fight lasts the match stays stopped (no clock, no faceoff) and the dead puck lies by the
/// fighters, so the camera stays on them. Draws the offer prompt and the fight bar at the foot of the HUD.
/// </summary>
internal sealed class FightSystem : ISystem, IDisposable
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

    /// <summary>
    /// W30: everyone else gives the fighters room. A skater inside the ring around the fight glides
    /// out to its edge and faces the fighters; nobody is pulled from farther away.
    /// </summary>
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

    static void BeginExchange(World world, FightState s, Fight fight)
    {
        fight.Stage = FightStage.Exchange;
        fight.StageTime = 0;
        foreach (var f in fight.Both)
        {
            f.Punched = false;
            f.PunchAt = float.NaN;
            f.AiPunchAt = f.Slot < 0 && s.AiPunches ? FightModel.AiPunchAt(world.Players[f.PlayerId], s.Rng) : float.NaN;
        }
    }

    static void Exchange(World world, FightState s, Fight fight)
    {
        float t = fight.Sweep;
        foreach (var f in fight.Both)
        {
            bool block, punch;
            if (f.Slot >= 0)
            {
                var cmd = world.Controlled(f.Slot).TryGet(out var me) ? me.Command : default;
                block = cmd.Block;
                punch = cmd.Punch;
            }
            else
            {
                block = !float.IsNaN(f.AiBlockAt) && world.Time >= f.AiBlockAt;
                if (block && world.Time >= f.AiBlockAt + 0.5f) { f.AiBlockAt = float.NaN; block = false; }
                punch = !float.IsNaN(f.AiPunchAt) && t >= f.AiPunchAt;
            }

            if (block && !f.Blocking) { f.BlockSince = world.Time; SetClip(world, f, "block"); }
            if (!block && f.Blocking && float.IsNaN(f.LandsAt)) SetClip(world, f, "grab");
            f.Blocking = block;

            if (punch && !f.Punched && !block && t < 1f) Throw(world, s, fight, f, t);
        }

        foreach (var f in fight.Both)
        {
            if (!float.IsNaN(f.LandsAt) && world.Time >= f.LandsAt) Land(world, s, fight, f);
            if (fight.Stage != FightStage.Exchange) return;
            if (string.Equals(f.Clip, "punch", StringComparison.Ordinal) && world.Time - f.ClipSince > 0.6f) SetClip(world, f, f.Blocking ? "block" : "grab");
        }

        if (fight.StageTime < FightTuning.ExchangeSeconds || fight.Both.Any(f => !float.IsNaN(f.LandsAt))) return;
        fight.Exchange++;
        if (fight.Exchange < FightTuning.MaxExchanges)
        {
            BeginExchange(world, s, fight);
            return;
        }
        // the linesmen step in: less balance loses, the instigating checker on a tie
        var loser = fight.Victim.Balance < fight.Checker.Balance ? fight.Victim : fight.Checker;
        Decide(world, s, fight, loser, knockdown: false);
    }

    static void Throw(World world, FightState s, Fight fight, Fighter f, float t)
    {
        f.Punched = true;
        f.PunchAt = t;
        f.LandsAt = world.Time + FightTuning.Windup;
        SetClip(world, f, "punch");
        var other = fight.Other(f);
        if (other.Slot < 0 && s.AiBlocks && s.Rng.NextSingle() < FightModel.AiBlockChance(world.Players[other.PlayerId]))
            other.AiBlockAt = world.Time + 0.04f + (s.Rng.NextSingle() * 0.14f);
    }

    static void Land(World world, FightState s, Fight fight, Fighter f)
    {
        f.LandsAt = float.NaN;
        var other = fight.Other(f);
        float quality = FightModel.Quality(f.PunchAt);
        if (quality <= 0)
        {
            f.Missed++;
            return;
        }
        if (other.Blocking && world.Time - other.BlockSince <= FightTuning.BlockFresh)
        {
            f.Blocked++;
            return;
        }
        var puncher = world.Players[f.PlayerId];
        var target = world.Players[other.PlayerId];
        other.Balance -= FightModel.Damage(puncher, target, quality);
        f.Landed++;
        fight.LandedAt = world.Time;
        // a landed punch sounds like a body check (audio is the only reader after this system)
        world.Events.Publish(new CheckLanded(world.Time, puncher.Side, puncher.Id, target.Position, 0.6f + (0.6f * quality)));
        if (other.Balance <= 0) Decide(world, s, fight, other, knockdown: true);
    }

    static void Decide(World world, FightState s, Fight fight, Fighter loser, bool knockdown)
    {
        fight.Stage = FightStage.Over;
        fight.StageTime = 0;
        fight.LoserId = loser.PlayerId;
        fight.Knockdown = knockdown;
        var winner = fight.Other(loser);
        SetClip(world, winner, "celebrate");
        SetClip(world, loser, knockdown ? "fall" : "idle");
        s.Log(world, $"{Penalties.Name(world, world.Players[winner.PlayerId])} wins {(knockdown ? "by knockdown" : "on balance")}");
    }

    /// <summary>
    /// The fight is over: the rule set's fight penalty (W48) is handed out and the faceoff follows in the
    /// loser's end. Arcade: the loser serves two minutes, the winner stays on. SHL and NHL: both fighters
    /// get a 5 minute major, coincidental and substituted so neither club plays short; under SHL both also
    /// get a game misconduct and leave the squad.
    /// </summary>
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

    public void DrawHud(World world, RenderContext render)
    {
        if (!art.TryGet(out var hud) || !MatchFlow.IsRunning(world)) return;
        var s = Fighting.State(world);
        if (s.Fight.TryGet(out var fight)) DrawFight(world, hud, fight);
        else if (s.Offer.TryGet(out var offer) && (offer.Slots[0] >= 0 || offer.Slots[1] >= 0)) DrawOffer(world, hud, offer);
    }

    static void DrawOffer(World world, HudArt art, FightOffer offer)
    {
        string title = Text.Get(K.GlovesOff), hint = Text.Get(K.FightHint);
        int w = Math.Max(art.Measure(title), art.Measure(hint)) + 16, h = 30;
        int x = (Game.Width - w) / 2, y = Game.Height - h - 8;
        art.DrawPanel("panel_red", x, y, w, h);
        art.DrawText(title, x + ((w - art.Measure(title)) / 2), y + 3, art.Score);
        art.DrawText(hint, x + ((w - art.Measure(hint)) / 2), y + 15, art.Text);
        // how long is left to accept, as a shrinking line under the panel
        float left = 1f - Math.Clamp((world.Time - offer.Time) / FightTuning.OfferSeconds, 0f, 1f);
        Raylib.DrawRectangle(x + 4, y + h - 3, (int)((w - 8) * left), 1, art.Text);
    }

    static void DrawFight(World world, HudArt art, Fight fight)
    {
        const int w = 300, h = 34, barW = 76;
        int x = (Game.Width - w) / 2, y = Game.Height - h - 6;
        art.DrawPanel("panel", x, y, w, h);
        DrawFighter(world, art, fight.Checker, x + 4, y + 4, w / 2 - 4 - (barW / 2) - 6, left: true);
        DrawFighter(world, art, fight.Victim, x + (w / 2) + (barW / 2) + 6, y + 4, w / 2 - 4 - (barW / 2) - 6, left: false);

        // the timing bar: the sweet spot in the middle, the marker sweeping across
        int bx = x + ((w - barW) / 2), by = y + 6;
        Raylib.DrawRectangle(bx, by, barW, 8, Bar);
        int sweet = (int)(barW * FightTuning.SweetHalf * 2);
        Raylib.DrawRectangle(bx + ((barW - sweet) / 2), by, sweet, 8, Sweet);
        Raylib.DrawRectangle(bx + (barW / 2) - 1, by, 2, 8, Centre);
        if (fight.Stage == FightStage.Exchange) Raylib.DrawRectangle(bx + (int)((barW - 2) * fight.Sweep), by - 2, 2, 12, Marker);

        string label = fight.Stage switch
        {
            FightStage.Grab => Text.Get(K.Fight),
            FightStage.Exchange => Text.Format(K.FightRound, fight.Exchange + 1),
            _ => Text.Get(fight.Knockdown ? K.FightDown : K.FightBrokenUp),
        };
        art.DrawText(label, bx + ((barW - art.Measure(label)) / 2), by + 11, fight.Stage == FightStage.Over ? art.Score : art.Text);
        if (fight.Both.Any(f => f.Slot >= 0) && fight.Stage != FightStage.Over)
        {
            string keys = Text.Get(K.FightKeys);
            art.DrawText(keys, (Game.Width - art.Measure(keys)) / 2, y - 12, art.Text);
        }
    }

    static void DrawFighter(World world, HudArt art, Fighter f, int x, int y, int w, bool left)
    {
        var p = world.Players[f.PlayerId];
        string name = Text.Format(K.PlayerTag, p.Data.Number, p.Data.LastName);
        while (name.Length > 4 && art.Measure(name) > w - 20) name = name[..^1];
        int crestX = left ? x : x + w - 16;
        art.DrawCrest(world.Setup.Club(p.Side).Id, crestX, y);
        int tx = left ? x + 20 : x + w - 20 - art.Measure(name);
        art.DrawText(name, tx, y - 1, art.Text);
        // balance: gold while standing, red as it runs out
        int bw = w - 20, filled = (int)(bw * Math.Clamp(f.Balance, 0f, 1f));
        int bx = left ? x + 20 : x;
        Raylib.DrawRectangle(bx, y + 12, bw, 4, Bar);
        Raylib.DrawRectangle(left ? bx : bx + bw - filled, y + 12, filled, 4, f.Balance > 0.35f ? Health : Hurt);
    }

    public void Dispose()
    {
        if (art.TryGet(out var hud)) hud.Unload();
        art = Maybe<HudArt>.None;
    }
}
