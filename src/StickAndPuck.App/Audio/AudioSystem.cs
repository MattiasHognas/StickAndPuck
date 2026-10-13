using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Core;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Audio;

/// <summary>
/// W12: plays the generated 8-bit effects for each match event and puck contact. Runs after every
/// publisher, reads this step's <see cref="MatchEvents"/> and maps them through
/// <see cref="SoundCues"/>; it also watches the puck, so a pickup or a deflection nobody published
/// still clicks. With no audio device it stays silent and the game runs on.
/// SAP_SFX=demo plays every event's sounds in turn, for listening to the set in the game.
/// </summary>
internal sealed class AudioSystem : ISystem, IDisposable
{
    const int Voices = 4;              // copies of each effect, so quick repeats overlap instead of cutting off
    const float PanWidth = 18f;        // metres from the camera centre to full pan
    const float MaxPan = 0.6f;         // never hard left or right
    const float Deflection = 3f;       // m/s change in one step that counts as the puck touching something
    const float DemoGap = 1.6f;        // seconds between demo events

    static readonly float[] Detune = [1f, 1.03f, 0.97f, 1.05f];

    readonly Dictionary<Sfx, Sound[]> sounds = [];
    readonly Dictionary<Sfx, int> next = [];
    readonly Dictionary<Sfx, float> lastStart = [];
    readonly List<(float At, Cue Cue, float Pan)> delayed = [];
    float clock;
    int played;
    bool ready;
    bool demo;
    float demoTimer;
    int demoIndex;
    int lastCarrier = -1;
    Vector3 lastVelocity;

    public bool RunsWhilePaused => true;

    public void Start(World world)
    {
        lastCarrier = world.Puck.Carrier;
        lastVelocity = world.Puck.Velocity;
        demo = string.Equals(Environment.GetEnvironmentVariable("SAP_SFX"), "demo", StringComparison.OrdinalIgnoreCase);
        InitAudioDevice();
        if (!IsAudioDeviceReady())
        {
            Console.WriteLine("SAP: audio: no audio device, sounds off");
            return;
        }

        foreach (var sfx in Enum.GetValues<Sfx>())
        {
            string path = Path.Combine(SfxTable.Folder, SfxTable.Info(sfx).File);
            if (!File.Exists(path)) continue;
            var first = LoadSound(path);
            if (!IsSoundValid(first)) continue;
            var copies = new Sound[Voices];
            copies[0] = first;
            for (int i = 1; i < Voices; i++) copies[i] = LoadSoundAlias(first);
            sounds[sfx] = copies;
        }

        ready = true;
        Console.WriteLine($"SAP: audio: {sounds.Count} of {Enum.GetValues<Sfx>().Length} sounds loaded from {SfxTable.Folder}");
    }

    public void Update(World world, float dt)
    {
        clock += dt;
        if (!ready) return;

        bool puckEvent = false;
        foreach (var e in world.Events.Current)
        {
            puckEvent |= SoundCues.IsPuckContact(e.Kind);
            Queue(world, e);
        }

        WatchPuck(world, puckEvent);
        if (demo) Demo(world, dt);

        for (int i = delayed.Count - 1; i >= 0; i--)
        {
            if (delayed[i].At > clock) continue;
            Play(world, delayed[i].Cue, delayed[i].Pan);
            delayed.RemoveAt(i);
        }
    }

    public void Dispose()
    {
        if (!ready) return;
        foreach (var copies in sounds.Values)
        {
            for (int i = 1; i < copies.Length; i++) UnloadSoundAlias(copies[i]);
            UnloadSound(copies[0]);
        }

        sounds.Clear();
        CloseAudioDevice();
        Console.WriteLine($"SAP: audio: {played} sounds played");
    }

    void Queue(World world, MatchEvent e)
    {
        float pan = Pan(world, e.Position);
        foreach (var cue in SoundCues.For(e))
        {
            if (cue.Delay > 0) delayed.Add((clock + cue.Delay, cue, pan));
            else Play(world, cue, pan);
        }
    }

    // covers the puck contacts nobody published this step: the puck leaving a stick at speed plays
    // a shot, landing on a stick or changing course (a skate, a body, the net) plays a stick click
    void WatchPuck(World world, bool puckEvent)
    {
        var puck = world.Puck;
        if (!puckEvent && puck.Live)
        {
            if (puck.Carrier >= 0 && lastCarrier < 0)
            {
                Play(world, new Cue(Sfx.Stick), Pan(world, puck.Position));
            }
            else if (puck.Carrier < 0 && lastCarrier >= 0 && puck.Velocity.Length() > Deflection)
            {
                Queue(world, new MatchEvent(MatchEventKind.Shot, world.Time, Position: puck.Position, Strength: puck.Velocity.Length()));
            }
            else if (puck.Carrier < 0 && lastCarrier < 0)
            {
                float change = (puck.Velocity - lastVelocity).Length();
                if (change > Deflection) Play(world, new Cue(Sfx.Stick, SoundCues.Hardness(change, 15)), Pan(world, puck.Position));
            }
        }

        lastCarrier = puck.Carrier;
        lastVelocity = puck.Velocity;
    }

    void Demo(World world, float dt)
    {
        demoTimer -= dt;
        if (demoTimer > 0) return;
        demoTimer = DemoGap;
        var kinds = Enum.GetValues<MatchEventKind>();
        var kind = kinds[demoIndex++ % kinds.Length];
        var e = new MatchEvent(kind, world.Time, Position: world.Camera.Target);
        Console.WriteLine($"SAP: audio demo: {kind} -> {string.Join(", ", SoundCues.For(e).Select(c => c.Sound))}");
        Queue(world, e);
    }

    void Play(World world, Cue cue, float pan)
    {
        if (!sounds.TryGetValue(cue.Sound, out var copies)) return;
        var info = SfxTable.Info(cue.Sound);
        if (lastStart.TryGetValue(cue.Sound, out float last) && clock - last < info.Gap) return;
        lastStart[cue.Sound] = clock;

        int i = next.GetValueOrDefault(cue.Sound);
        next[cue.Sound] = (i + 1) % copies.Length;
        var sound = copies[i];
        SetSoundVolume(sound, Math.Clamp(world.Settings.Volume * info.Volume * cue.Volume, 0, 1));
        SetSoundPitch(sound, cue.Pitch * (info.Spatial ? Detune[i % Detune.Length] : 1));
        SetSoundPan(sound, info.Spatial ? pan : 0);
        PlaySound(sound);
        played++;
    }

    // left and right follow where on the rink it happened, relative to what the camera looks at
    static float Pan(World world, Vector3 position)
    {
        return Math.Clamp((position.X - world.Camera.Target.X) / PanWidth, -1, 1) * MaxPan;
    }
}
