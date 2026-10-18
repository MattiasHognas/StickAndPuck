using Raylib_cs;
using StickAndPuck.App.Audio;
using StickAndPuck.App.Core;
using StickAndPuck.App.Fights;
using StickAndPuck.App.Hud;
using StickAndPuck.App.Rules.Match;
using static Raylib_cs.Raylib;

namespace StickAndPuck.App.Momentum
{
    /// <summary>Reads this step's goals, checks and fights, gives the club a boost, drains it during live play
    /// and tops up the energy of its skaters on the ice meanwhile. Swells the crowd (its own copies of
    /// the crowd sound, louder the bigger the boost) and draws a momentum bar under the scorebug.</summary>
    internal sealed class MomentumSystem : ISystem, IDisposable
    {
        private const int Voices = 3;

        private readonly Sound[] crowd = new Sound[Voices];
        private Maybe<Fight> credited;
        private Maybe<HudArt> art;
        private bool sound;
        private int voice;
        private float swell;

        /// <inheritdoc/>
        public void Start(World world)
        {
            _ = MomentumMeter.State(world);
            if (IsAudioDeviceReady())
            {
                string filename = SfxTable.Info(Sfx.Crowd).File;
                Sound first = ResourceLoader.Sound($"{SfxTable.ResourcePath}.{filename}");
                if (IsSoundValid(first))
                {
                    crowd[0] = first;
                    sound = true;
                    for (int i = 1; i < Voices; i++)
                    {
                        crowd[i] = LoadSoundAlias(first);
                    }
                }
            }
            if (IsWindowReady())
            {
                art = HudArt.Load();
            }
        }

        /// <inheritdoc/>
        public void Update(World world, float dt)
        {
            Read(world);
            MomentumState s = MomentumMeter.State(world);
            foreach (Side side in new[] { Side.Home, Side.Away })
            {
                int i = (int)side;
                if (s.Level[i] <= 0f)
                {
                    continue;
                }

                if (world.Puck.Live)
                {
                    s.Level[i] = Math.Max(0f, s.Level[i] - (dt / MomentumTuning.Seconds));
                }

                foreach (Player p in world.OnIce(side))
                {
                    if (p.IsGoalie || p.Energy >= 1f)
                    {
                        continue;
                    }

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
            if (
                !art.TryGet(out HudArt hud)
                || !MatchFlow.IsRunning(world)
                || world.Match.Phase is MatchPhase.Intro or MatchPhase.Shootout or MatchPhase.Final
            )
            {
                return;
            }

            if (!MomentumMeter.Leader(world).TryGetSide(out Side side))
            {
                return;
            }

            float level = MomentumMeter.Of(world, side);
            MomentumBar.Draw(world, hud, side, level);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (art.TryGet(out HudArt hud))
            {
                hud.Unload();
            }

            art = Maybe.None<HudArt>();
            if (!sound)
            {
                return;
            }

            sound = false;
            if (!IsAudioDeviceReady())
            {
                return; // closes the device first; then raylib has freed the sounds
            }

            for (int i = 1; i < Voices; i++)
            {
                UnloadSoundAlias(crowd[i]);
            }

            UnloadSound(crowd[0]);
        }

        private void Read(World world)
        {
            foreach (MatchEvent e in world.Events.Current)
            {
                if (e is GoalScored goal)
                {
                    Boost(world, goal.Side, MomentumCause.Goal);
                }
                else if (
                    e is CheckLanded check
                    && check.Strength >= MomentumTuning.BigHitStrength
                    && !Fighting.Current(world).HasValue
                )
                {
                    Boost(world, check.Side, MomentumCause.BigHit);
                }
            }

            if (
                Fighting.Current(world).TryGet(out Fight fight)
                && fight.LoserId >= 0
                && !credited.Is(c => ReferenceEquals(c, fight))
            )
            {
                credited = fight;
                Fighter won = fight.Of(fight.LoserId).TryGet(out Fighter loser)
                    ? fight.Other(loser)
                    : fight.Checker;
                Boost(world, world.Players[won.PlayerId].Side, MomentumCause.FightWon);
            }
        }

        private void Boost(World world, Side side, MomentumCause cause)
        {
            MomentumMeter.Boost(world, side, cause);
            swell = 0f; // the crowd answers at once
        }

        /// <summary>Gets the crowd's volume for a boost: louder the bigger the boost, and at a full one well over the crowd after a goal.</summary>
        public static float CrowdVolume(float level, float volume)
        {
            return Math.Clamp(volume * MomentumTuning.CrowdVolume * level, 0f, 1f);
        }

        // while a club has a boost the crowd keeps rising, louder the bigger the boost
        private void Swell(World world, float dt)
        {
            swell -= dt;
            if (swell > 0f)
            {
                return;
            }

            float level = MomentumMeter.Leader(world).TryGetSide(out Side side)
                ? MomentumMeter.Of(world, side)
                : 0f;
            if (level < MomentumTuning.SwellBelow)
            {
                return;
            }

            swell = MomentumTuning.SwellGap;
            MomentumState state = MomentumMeter.State(world);
            state.Swells++;
            state.CrowdVolume = CrowdVolume(level, world.Settings.EffectsVolume);
            if (!sound)
            {
                return;
            }

            Sound s = crowd[voice];
            voice = (voice + 1) % Voices;
            SetSoundVolume(s, state.CrowdVolume);
            SetSoundPitch(s, 0.9f + (0.15f * level));
            PlaySound(s);
        }
    }
}
