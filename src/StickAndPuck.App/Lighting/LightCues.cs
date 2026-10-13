using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Lighting;

/// <summary>What the cues do to the preset this step.</summary>
/// <param name="Tone">Multiplies the preset's three tones.</param>
/// <param name="Bands">Whether the ice bands show (they are reflections of the arena lights).</param>
/// <param name="Flash">Whether a goal flash is lit this step, which turns bands and glass gold.</param>
public readonly record struct CueLight(Vector3 Tone, bool Bands, bool Flash);

/// <summary>
/// The light cues. The intro plays with the arena lights down; at the opening faceoff they come
/// back bank by bank in a few hard steps. A goal strobes the lights: bright, gold flashes
/// against near-dark, then settles. Steps are hard so the pixel frame never smears a fade.
/// </summary>
public sealed class LightCues
{
    /// <summary>How dark the intro is: lit surfaces times this.</summary>
    public static readonly Vector3 IntroDim = new(0.50f, 0.46f, 0.60f);

    /// <summary>How bright a goal flash is.</summary>
    public static readonly Vector3 FlashBright = new(1.35f, 1.28f, 1.12f);

    /// <summary>How dark the gaps between goal flashes are.</summary>
    public static readonly Vector3 FlashDark = new(0.50f, 0.44f, 0.58f);

    /// <summary>The time the lights take to come back after the intro, in seconds.</summary>
    public const float LightsUpSeconds = 1.2f;

    /// <summary>The number of hard steps the lights come back in.</summary>
    public const int LightsUpSteps = 4;

    /// <summary>The length of the goal strobe, in seconds; inside W7a's goal celebration.</summary>
    public const float FlashSeconds = 2.4f;

    /// <summary>The length of one flash or one gap, in seconds.</summary>
    public const float FlashPeriod = 0.2f;

    float lightsUp = LightsUpSeconds;     // time since the intro ended; starts finished
    float flash = FlashSeconds;           // time since the last goal; starts finished
    bool wasIntro;

    /// <summary>Gets a value indicating whether the intro dim is on.</summary>
    public bool Intro { get; private set; }

    /// <summary>Gets a value indicating whether a goal strobe is running.</summary>
    public bool Flashing => flash < FlashSeconds;

    /// <summary>Advances the cues from the match phase and this step's events.</summary>
    /// <param name="world">The match state.</param>
    /// <param name="dt">The step length.</param>
    public void Update(World world, float dt)
    {
        Intro = world.Match.Phase == MatchPhase.Intro;
        if (wasIntro && !Intro) lightsUp = 0;
        wasIntro = Intro;
        if (!Intro) lightsUp = Math.Min(lightsUp + dt, LightsUpSeconds);

        if (world.Events.Of<GoalScored>().Any()) flash = 0;
        else flash = Math.Min(flash + dt, FlashSeconds);
    }

    /// <summary>Gets what the cues do this step.</summary>
    /// <returns>The tone multiplier and band state.</returns>
    public CueLight Current()
    {
        if (Flashing)
        {
            bool lit = (int)(flash / FlashPeriod) % 2 == 0;
            return lit ? new CueLight(FlashBright, true, true) : new CueLight(FlashDark, false, false);
        }
        if (Intro) return new CueLight(IntroDim, false, false);
        if (lightsUp < LightsUpSeconds)
        {
            // bank by bank: 1/4, 2/4, 3/4 of the way, then full
            float step = MathF.Floor(lightsUp / LightsUpSeconds * LightsUpSteps + 1) / LightsUpSteps;
            return new CueLight(Vector3.Lerp(IntroDim, Vector3.One, step), step >= 0.5f, false);
        }
        return new CueLight(Vector3.One, true, false);
    }
}
