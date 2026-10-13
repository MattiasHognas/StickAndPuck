using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.App.Fights;
using StickAndPuck.App.Hud;
using StickAndPuck.App.Rules;
using static Raylib_cs.Raylib;
using K = StickAndPuck.App.Core.Text.Keys;

namespace StickAndPuck.App.Momentum;

/// <summary>
/// W29: reads this step's goals, checks and fights, gives the club a boost, drains it during live play
/// and tops up the energy of its skaters on the ice meanwhile. Swells the crowd (its own copies of
/// W12's crowd sound, louder the bigger the boost) and draws a momentum bar under the scorebug.
/// </summary>
internal sealed class MomentumSystem : ISystem, IDisposable
{
    const int Voices = 3;

    readonly Sound[] crowd = new Sound[Voices];
    Maybe<Fight> credited;
    Maybe<HudArt> art;
    bool sound;
    int voice;
    float swell;

    /// <inheritdoc/>
    public void Start(World world)
    {
        Momentum.State(world);
        string path = Path.Combine(Audio.SfxTable.Folder, "crowd.wav");
        if (IsAudioDeviceReady() && File.Exists(path))
        {
            crowd[0] = LoadSound(path);
            sound = IsSoundValid(crowd[0]);
            for (int i = 1; sound && i < Voices; i++) crowd[i] = LoadSoundAlias(crowd[0]);
        }
        if (IsWindowReady() && File.Exists(Assets.UiLayout)) art = HudArt.Load();
    }

    /// <inheritdoc/>
    public void Update(World world, float dt)
    {
        Read(world);
        var s = Momentum.State(world);
        foreach (var side in new[] { Side.Home, Side.Away })
        {
            int i = (int)side;
            if (s.Level[i] <= 0f) continue;
            if (world.Puck.Live) s.Level[i] = Math.Max(0f, s.Level[i] - (dt / MomentumTuning.Seconds));
            foreach (var p in world.OnIce(side))
            {
                if (p.IsGoalie || p.Energy >= 1f) continue;
                float gain = Math.Min(1f - p.Energy, MomentumTuning.Recover * s.Level[i] * dt);
                p.Energy += gain;
                s.Energy[i] += gain;
            }
        }
        Swell(world, dt);
    }

    /// <inheritdoc/>
    public void DrawHud(World world, RenderContext render)
    {
        if (!art.TryGet(out var hud) || !MatchFlow.IsRunning(world) || world.Match.Phase is MatchPhase.Intro or MatchPhase.Shootout or MatchPhase.Final) return;
        if (!Momentum.Leader(world).TryGetSide(out var side)) return;
        float level = Momentum.Of(world, side);
        MomentumBar.Draw(world, hud, side, level);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (art.TryGet(out var hud)) hud.Unload();
        art = Maybe<HudArt>.None;
        if (!sound) return;
        sound = false;
        if (!IsAudioDeviceReady()) return;     // W12 closes the device first; then raylib has freed the sounds
        for (int i = 1; i < Voices; i++) UnloadSoundAlias(crowd[i]);
        UnloadSound(crowd[0]);
    }

    void Read(World world)
    {
        foreach (var e in world.Events.Current)
        {
            if (e is GoalScored goal) Boost(world, goal.Side, MomentumCause.Goal);
            else if (e is CheckLanded check && check.Strength >= MomentumTuning.BigHitStrength && !Fighting.Current(world).HasValue) Boost(world, check.Side, MomentumCause.BigHit);
        }

        if (Fighting.Current(world).TryGet(out var fight) && fight.LoserId >= 0 && !credited.Is(c => ReferenceEquals(c, fight)))
        {
            credited = fight;
            var won = fight.Of(fight.LoserId).TryGet(out var loser) ? fight.Other(loser) : fight.Checker;
            Boost(world, world.Players[won.PlayerId].Side, MomentumCause.FightWon);
        }
    }

    void Boost(World world, Side side, MomentumCause cause)
    {
        Momentum.Boost(world, side, cause);
        swell = 0f;     // the crowd answers at once
    }

    /// <summary>Gets the crowd's volume for a boost: louder the bigger the boost, and at a full one well over W12's crowd after a goal.</summary>
    /// <param name="level">The boost, 0 to 1.</param>
    /// <param name="volume">The player's sound volume, 0 to 1.</param>
    /// <returns>The volume, 0 to 1.</returns>
    public static float CrowdVolume(float level, float volume)
    {
        return Math.Clamp(volume * MomentumTuning.CrowdVolume * level, 0f, 1f);
    }

    // while a club has a boost the crowd keeps rising, louder the bigger the boost
    void Swell(World world, float dt)
    {
        swell -= dt;
        if (swell > 0f) return;
        float level = Momentum.Leader(world).TryGetSide(out var side) ? Momentum.Of(world, side) : 0f;
        if (level < MomentumTuning.SwellBelow) return;
        swell = MomentumTuning.SwellGap;
        var state = Momentum.State(world);
        state.Swells++;
        state.CrowdVolume = CrowdVolume(level, world.Settings.Volume);
        if (!sound) return;
        var s = crowd[voice];
        voice = (voice + 1) % Voices;
        SetSoundVolume(s, state.CrowdVolume);
        SetSoundPitch(s, 0.9f + (0.15f * level));
        PlaySound(s);
    }
}

/// <summary>W29: the momentum bar under the scorebug and any penalty rows (W48): the boosted club's crest and a bar in its colour that drains with the boost.</summary>
internal static class MomentumBar
{
    /// <summary>Draws the bar.</summary>
    /// <param name="world">The match.</param>
    /// <param name="art">The HUD art.</param>
    /// <param name="side">The boosted club.</param>
    /// <param name="level">Its boost, 0 to 1.</param>
    public static void Draw(World world, HudArt art, Side side, float level)
    {
        const int pad = 3, crest = 16, gap = 4, barH = 4;
        string label = Text.Get(K.Momentum);
        int x = art.BugPosition.X;
        // below the penalty box rows (20 px and a 1 px gap each), which share the spot under the scorebug
        int rows = world.Match.Penalties.Count + Penalties.Waiting(world).Count + Penalties.Misconducts(world).Count + Penalties.Delayed(world).Count
            + (Lines.GoaliePull.IsPulled(world, Side.Home) ? 1 : 0) + (Lines.GoaliePull.IsPulled(world, Side.Away) ? 1 : 0);
        int y = Math.Max(art.BugPosition.Y + art.BugSize.H + 3, HudSystem.PenaltyTop + (rows * 21) + (rows > 0 ? 2 : 0));
        int w = pad + crest + gap + art.Measure(label) + pad + 1;
        int h = crest + (pad * 2);
        bool fresh = world.Time - Momentum.State(world).Since[(int)side] < 1.5f && ((int)(world.Time * 6) % 2 == 0);
        art.DrawPanel(fresh ? "panel_gold" : "panel", x, y, w, h);
        art.DrawCrest(world.Setup.Club(side).Id, x + pad, y + pad);
        int tx = x + pad + crest + gap;
        art.DrawText(label, tx, y + pad - 2, fresh ? art.Text : art.Score);
        int barW = art.Measure(label);
        int by = y + h - pad - barH;
        DrawRectangle(tx, by, barW, barH, new Color(20, 18, 28, 255));
        DrawRectangle(tx, by, (int)MathF.Ceiling(barW * Math.Clamp(level, 0f, 1f)), barH, HudSystem.StripeColour(world, side));
    }
}
