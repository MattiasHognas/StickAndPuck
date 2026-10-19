using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Arena;
using StickAndPuck.App.Core;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Audio
{
    /// <summary>Plays the generated 8-bit effects for each match event and puck contact, and the music
    /// (<see cref="MusicPlayer"/>) under the overall volume. Runs after every
    /// publisher, reads this step's <see cref="MatchEvents"/> and maps them through
    /// <see cref="SoundCues"/>; it also watches the puck, so a pickup or a deflection nobody published
    /// still clicks. Over it the fans chant (<see cref="CrowdChants"/>). With no audio device it stays silent and the game runs on.
    /// SAP_SFX=demo plays every event's sounds in turn, for listening to the set in the game.</summary>
    internal sealed class AudioSystem : ISystem, IDisposable
    {
        private const int Voices = 4; // copies of each effect, so quick repeats overlap instead of cutting off
        private const float PanWidth = 18f; // metres from the camera centre to full pan
        private const float MaxPan = 0.6f; // never hard left or right
        private const float Deflection = 3f; // m/s change in one step that counts as the puck touching something
        private const float DemoGap = 1.6f; // seconds between demo events

        private static readonly ImmutableArray<float> Detune = [1f, 1.03f, 0.97f, 1.05f];

        private readonly Dictionary<Sfx, Sound[]> sounds = [];
        private readonly Dictionary<Sfx, int> next = [];
        private readonly Dictionary<Sfx, float> lastStart = [];
        private readonly List<(float At, Cue Cue, float Pan)> delayed = [];
        private readonly MusicPlayer music = new();
        private readonly CrowdChants chants = new();
        private float clock;
        private int played;
        private bool ready;
        private bool demo;
        private float demoTimer;
        private int demoIndex;
        private int lastCarrier = -1;
        private Vector3 lastVelocity;

        public bool RunsWhilePaused => true;

        public void Start(World world)
        {
            // Fast replay verification can finish before a Web Audio resume promise settles.
            // It checks simulation only; leave audio unopened until a normal Start/Play gesture.
            if (OperatingSystem.IsBrowser() && Env.Is("SAP_SCREENSHOT", "1"))
            {
                return;
            }

            lastCarrier = world.Puck.Carrier;
            lastVelocity = world.Puck.Velocity;
            demo = string.Equals(Env.Get("SAP_SFX"), "demo", StringComparison.OrdinalIgnoreCase);
            InitAudioDevice();
            if (!IsAudioDeviceReady())
            {
                Console.WriteLine("SAP: audio: no audio device, sounds off");
                return;
            }

            foreach (Sfx sfx in Enum.GetValues<Sfx>())
            {
                string filename = SfxTable.Info(sfx).File;
                string embeddedPath = $"{SfxTable.ResourcePath}.{filename}";
                Sound first = ResourceLoader.Sound(embeddedPath);
                if (!IsSoundValid(first))
                {
                    continue;
                }

                Sound[] copies = new Sound[Voices];
                copies[0] = first;
                for (int i = 1; i < Voices; i++)
                {
                    copies[i] = LoadSoundAlias(first);
                }

                sounds[sfx] = copies;
            }

            music.Load();
            ready = true;
            Console.WriteLine(
                $"SAP: audio: {sounds.Count} of {Enum.GetValues<Sfx>().Length} sounds loaded from embedded assembly resources"
            );
        }

        public void Update(World world, float dt)
        {
            clock += dt;
            if (!ready)
            {
                return;
            }

            SetMasterVolume(Math.Clamp(world.Settings.OverallVolume, 0, 1));
            music.Update(world);

            bool puckEvent = false;
            foreach (MatchEvent e in world.Events.Current)
            {
                puckEvent |= SoundCues.IsPuckContact(e);
                Queue(world, e);
            }

            WatchPuck(world, puckEvent);
            foreach (Chant chant in chants.Update(world, dt))
            {
                // the home end fills the arena; the away chant comes from the visitors' corner
                float pan =
                    chant.Fans == Side.Away
                        ? Pan(world, CrowdReactions.Middle(CrowdFans.AwaySection))
                        : 0f;
                if (chant.Cue.Delay > 0)
                {
                    delayed.Add((clock + chant.Cue.Delay, chant.Cue, pan));
                }
                else
                {
                    Play(world, chant.Cue, pan);
                }
            }

            if (demo)
            {
                Demo(world, dt);
            }

            for (int i = delayed.Count - 1; i >= 0; i--)
            {
                if (delayed[i].At > clock)
                {
                    continue;
                }

                Play(world, delayed[i].Cue, delayed[i].Pan);
                delayed.RemoveAt(i);
            }
        }

        public void Dispose()
        {
            if (!ready)
            {
                return;
            }

            foreach (Sound[] copies in sounds.Values)
            {
                for (int i = 1; i < copies.Length; i++)
                {
                    UnloadSoundAlias(copies[i]);
                }

                UnloadSound(copies[0]);
            }

            sounds.Clear();
            music.Dispose();
            CloseAudioDevice();
            Console.WriteLine(
                string.Create(CultureInfo.InvariantCulture, $"SAP: audio: {played} sounds played")
            );
        }

        private void Queue(World world, MatchEvent e)
        {
            float pan = Pan(world, SoundCues.Where(e));
            foreach (Cue cue in SoundCues.For(e))
            {
                if (cue.Delay > 0)
                {
                    delayed.Add((clock + cue.Delay, cue, pan));
                }
                else
                {
                    Play(world, cue, pan);
                }
            }
        }

        // covers the puck contacts nobody published this step: the puck leaving a stick at speed plays
        // a shot, landing on a stick or changing course (a skate, a body, the net) plays a stick click
        private void WatchPuck(World world, bool puckEvent)
        {
            Puck puck = world.Puck;
            if (!puckEvent && puck.Live)
            {
                if (puck.Carrier >= 0 && lastCarrier < 0)
                {
                    Play(world, new Cue(Sfx.Stick), Pan(world, puck.Position));
                }
                else if (
                    puck.Carrier < 0
                    && lastCarrier >= 0
                    && puck.Velocity.Length() > Deflection
                )
                {
                    Play(world, SoundCues.Shot(puck.Velocity.Length()), Pan(world, puck.Position));
                }
                else if (puck.Carrier < 0 && lastCarrier < 0)
                {
                    float change = (puck.Velocity - lastVelocity).Length();
                    if (change > Deflection)
                    {
                        Play(
                            world,
                            new Cue(Sfx.Stick, SoundCues.Hardness(change, 15)),
                            Pan(world, puck.Position)
                        );
                    }
                }
            }

            lastCarrier = puck.Carrier;
            lastVelocity = puck.Velocity;
        }

        private void Demo(World world, float dt)
        {
            demoTimer -= dt;
            if (demoTimer > 0)
            {
                return;
            }

            demoTimer = DemoGap;
            MatchEvent[] samples = SoundCues.Samples(world.Time, world.Camera.Target);
            MatchEvent e = samples[demoIndex++ % samples.Length];
            Console.WriteLine(
                $"SAP: audio demo: {e.GetType().Name} -> {string.Join(", ", SoundCues.For(e).Select(c => c.Sound))}"
            );
            Queue(world, e);
        }

        private void Play(World world, Cue cue, float pan)
        {
            if (!sounds.TryGetValue(cue.Sound, out Sound[] copies))
            {
                return;
            }

            SfxInfo info = SfxTable.Info(cue.Sound);
            if (lastStart.TryGetValue(cue.Sound, out float last) && clock - last < info.Gap)
            {
                return;
            }

            lastStart[cue.Sound] = clock;

            int i = next.GetValueOrDefault(cue.Sound);
            next[cue.Sound] = (i + 1) % copies.Length;
            Sound sound = copies[i];
            SetSoundVolume(
                sound,
                Math.Clamp(world.Settings.EffectsVolume * info.Volume * cue.Volume, 0, 1)
            );
            SetSoundPitch(sound, cue.Pitch * (info.Spatial ? Detune[i % Detune.Length] : 1));
            SetSoundPan(sound, info.Spatial ? pan : 0);
            PlaySound(sound);
            played++;
        }

        // left and right follow where on the rink it happened, relative to what the camera looks at
        private static float Pan(World world, Vector3 position)
        {
            return Math.Clamp((position.X - world.Camera.Target.X) / PanWidth, -1, 1) * MaxPan;
        }
    }
}
