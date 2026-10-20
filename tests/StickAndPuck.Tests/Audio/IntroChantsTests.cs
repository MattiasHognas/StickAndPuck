using System.Globalization;
using StickAndPuck.App.Audio;
using StickAndPuck.App.Core;
using StickAndPuck.App.Screens;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Audio
{
    /// <summary>
    /// The crowd through the title intro (W144), against the intro's timeline: silent while the letters
    /// drop, the swell rising with the tifo and the camera move, full while the tifo is up, settled to
    /// nothing as the start menu shows, cut at once on a skip, and the menu music ducked under it.
    /// </summary>
    public sealed class IntroChantsTests : CheckedTest
    {
        // plays the intro step by step as the screen does, the chants following it, until it closes
        private static List<(float At, Cue Cue, float Level)> Play(
            IntroChants chants,
            TitleIntro intro,
            float skipAt = float.MaxValue
        )
        {
            List<(float, Cue, float)> heard = [];
            while (!intro.Done)
            {
                intro.Update(Game.Step, intro.Elapsed >= skipAt);
                Maybe<TitleIntro> onScreen = intro.Done ? Maybe.None<TitleIntro>() : intro;
                foreach (Cue cue in chants.Update(onScreen, Game.Step))
                {
                    heard.Add((intro.Elapsed, cue, chants.Level));
                }
            }

            return heard;
        }

        [Test]
        public void TheSwellFollowsTheTimeline()
        {
            Check(IntroChants.Swell(0f) == 0f, "silent as the intro opens");
            Check(
                IntroChants.Swell(TitleIntro.TifoAt - 0.01f) == 0f,
                "silent while the letters drop, up to the tifo"
            );
            Check(
                IntroChants.Swell(TitleIntro.MoveFrom) is > 0f and < 1f,
                "rising as the camera starts down and the arena fades in"
            );
            Check(
                IntroChants.Swell(TitleIntro.RiseFrom + 0.1f) > 0.9999f,
                "full while the tifo is up and the camera arrives"
            );
            Check(IntroChants.Swell(TitleIntro.Seconds) == 0f, "settled as the menu shows");

            float last = 0f;
            for (float t = 0f; t <= IntroChants.PeakFrom; t += Game.Step)
            {
                float now = IntroChants.Swell(t);
                Check(
                    now >= last - 1e-5f,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"it only rises on the way up ({t:0.00} s)"
                    )
                );
                last = now;
            }

            for (float t = IntroChants.PeakFrom; t <= TitleIntro.Seconds; t += Game.Step)
            {
                float now = IntroChants.Swell(t);
                Check(
                    now <= last + 1e-5f,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"it only settles after the peak ({t:0.00} s)"
                    )
                );
                last = now;
            }
        }

        [Test]
        public void TheChantsStartOnTheirBeatsAndSettleAtTheMenu()
        {
            IntroChants chants = new();
            List<(float At, Cue Cue, float Level)> heard = Play(chants, new TitleIntro());

            Check(heard.Count == IntroChants.Track.Length, "every chant of the track starts once");
            for (int i = 0; i < heard.Count; i++)
            {
                string n = i.ToString(CultureInfo.InvariantCulture);
                Check(heard[i].Cue == IntroChants.Track[i].Cue, $"chant {n} in the track's order");
                Check(
                    heard[i].At >= IntroChants.Track[i].At
                        && heard[i].At < IntroChants.Track[i].At + Game.Step + 1e-4f,
                    $"chant {n} starts on its beat"
                );
            }

            Check(heard[0].At >= TitleIntro.TifoAt, "nothing before the tifo goes up");
            Check(
                heard.Exists(h =>
                    h.Cue.Sound == Sfx.ChantClap && h.At < TitleIntro.MoveFrom + 0.1f
                ),
                "the home end chants as the camera starts down"
            );
            Check(chants.Level == 0f, "settled to quiet as the menu shows");
            Check(!chants.Sounding, "and stopped");
            Check(
                chants.Update(Maybe.None<TitleIntro>(), Game.Step).Count == 0,
                "nothing more on the menu"
            );
        }

        [Test]
        public void ASkipCutsItCleanly()
        {
            IntroChants chants = new();
            TitleIntro intro = new();
            float skipAt = TitleIntro.RiseFrom + 0.05f;
            int heard = Play(chants, intro, skipAt).Count;
            Check(intro.Skipped, "pressed mid-swell");
            Check(heard == 2, "the chants due before the press started, the rest never do");
            Check(chants.Level > 0.5f, "loud as the press lands");
            Check(chants.Sounding, "still fading on the press's step");

            int steps = 0;
            while (chants.Sounding)
            {
                Check(
                    chants.Update(Maybe.None<TitleIntro>(), Game.Step).Count == 0,
                    "nothing starts after a skip"
                );
                steps++;
            }

            Check(
                steps * Game.Step <= IntroChants.CutSeconds + Game.Step,
                "silent within the cut, a few hundredths of a second"
            );
            Check(chants.Level == 0f, "nothing left sounding");
        }

        [Test]
        public void TheMenuMusicDucksUnderTheSwell()
        {
            Check(IntroChants.MusicGain(0f) > 0.9999f, "full music with no crowd");
            Check(
                IntroChants.MusicGain(1f) is > 0.2f and < 0.5f,
                "about a third of it at the crowd's peak"
            );
            Check(
                IntroChants.MusicGain(IntroChants.Swell(TitleIntro.Seconds)) > 0.9999f,
                "back to full as the menu shows"
            );
        }
    }
}
