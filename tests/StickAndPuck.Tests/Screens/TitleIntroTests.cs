using System.Globalization;
using StickAndPuck.App.Arena;
using StickAndPuck.App.Core;
using StickAndPuck.App.Screens;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Screens
{
    /// <summary>
    /// The title intro on launch: which runs play it (scripted and headless runs never do), that any
    /// key, button or click skips it straight to the start menu, its timeline (letters, logo rise, fade,
    /// camera move) and the random clubs behind it.
    /// </summary>
    public sealed class TitleIntroTests : CheckedTest
    {
        private static Func<string, string> Env(params (string Name, string Value)[] values)
        {
            Dictionary<string, string> set = values.ToDictionary(
                v => v.Name,
                v => v.Value,
                StringComparer.Ordinal
            );
            return name => set.GetValueOrDefault(name, "");
        }

        [Test]
        public void ScriptedRunsSkipTheIntro()
        {
            Check(TitleIntro.Plays(Env()), "a plain launch plays the intro");
            Check(
                TitleIntro.Plays(Env(("SAP_SCENE", "start"), ("SAP_SCREEN", "start"))),
                "the start scene and screen named outright still play it (the browser names the scene)"
            );
            foreach (
                (string name, string value) in new[]
                {
                    ("SAP_SCREEN_KEYS", "down enter"),
                    ("SAP_SCREEN", "stats"),
                    ("SAP_SCENE", "match"),
                    ("SAP_SCREENSHOT", "1"),
                    ("SAP_RECORD", "4"),
                    ("SAP_STEPS", "600"),
                    ("SAP_REPLAY", "game.sapreplay"),
                    ("SAP_INTRO", "0"),
                }
            )
            {
                Check(!TitleIntro.Plays(Env((name, value))), $"{name}={value} skips the intro");
            }

            Check(
                TitleIntro.Plays(Env(("SAP_INTRO", "1"), ("SAP_RECORD", "5"))),
                "SAP_INTRO=1 plays it even in a recording, for captures"
            );
        }

        [Test]
        public void AnyPressSkips()
        {
            HashSet<MenuButton> none = [];
            InputFrame idle = InputFrame.Empty;
            Check(!TitleIntro.AnyPress(idle, none), "nothing pressed: the intro plays on");
            Check(
                TitleIntro.AnyPress(idle, new HashSet<MenuButton> { MenuButton.Accept }),
                "enter or A skips"
            );
            Check(
                TitleIntro.AnyPress(idle, new HashSet<MenuButton> { MenuButton.Pause }),
                "escape or start skips"
            );
            Check(TitleIntro.AnyPress(idle with { Pressed = [65] }, none), "any key skips");
            Check(TitleIntro.AnyPress(idle with { Mouse = 1 }, none), "a click skips");
            Check(
                TitleIntro.AnyPress(
                    idle with
                    {
                        Pads =
                        [
                            PadFrame.None with
                            {
                                Available = true,
                                Pressed = 1u << 12,
                            },
                            .. idle.Pads[1..],
                        ],
                    },
                    none
                ),
                "any pad button skips"
            );
            Check(
                !TitleIntro.AnyPress(idle with { Keys = [65] }, none),
                "a key only held since before does not"
            );
        }

        [Test]
        public void ThePressGoesStraightToTheStartMenu()
        {
            AppState app = new();
            World world = new WorldBuilder().App(app).Build();
            world.Match.Paused = true;
            app.Screens.Push(new StartScreen(app));
            TitleIntroScreen intro = new(app);
            app.Screens.Push(intro);
            Check(
                TitleIntroScreen.Playing(world).HasValue,
                "the intro is the top screen, so the camera follows it"
            );

            intro.Update(world, new HashSet<MenuButton> { MenuButton.Accept });
            Check(intro.Timeline.Skipped && intro.Timeline.Done, "a press ends the intro at once");
            Check(
                app.Screens.Screens.Count == 1 && app.Screens.Screens[0] is StartScreen,
                "the start menu is left on top"
            );
            Check(!TitleIntroScreen.Playing(world).HasValue, "the camera is back on the menu view");
            Check(
                world.Crowd.Pose(CrowdSection.FarCentre) == CrowdPose.Sit,
                "the crowd sits down with the menu up"
            );
        }

        [Test]
        public void Timeline()
        {
            TitleIntro intro = new();
            Check(!intro.Drop(0, out _), "black at first, no letter yet");
            Check(
                intro.Reveal <= 0 && intro.LogoY == TitleIntro.CentreY,
                "all black, logo centred"
            );
            int steps = 0;
            bool shook = false,
                rose = false,
                overshoot = false;
            while (!intro.Done && steps < 1000)
            {
                intro.Update(Game.Step, pressed: false);
                steps++;
                shook |= intro.Shake == TitleIntro.ShakePixels;
                overshoot |= intro.Drop(0, out int dy) && dy > 0;
                if (intro.Elapsed < TitleIntro.MoveFrom)
                {
                    Check(intro.Reveal <= 0, "still black before the camera move");
                }

                rose |= intro.LogoY < TitleIntro.CentreY;
            }

            Check(
                intro is { Done: true, Skipped: false }
                    && Math.Abs(intro.Elapsed - TitleIntro.Seconds) < 0.02f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"plays out in {TitleIntro.Seconds} s ({intro.Elapsed:0.00} s, {steps} steps)"
                )
            );
            Check(shook, "the frame shakes as a word lands");
            Check(overshoot, "a letter bounces past its place");
            Check(
                rose && intro.LogoY == TitleIntro.FinalY,
                "the logo rises to the start screen's title row"
            );
            Check(
                intro.Reveal >= 1 && intro.CameraMove >= 0.9999f,
                "the arena is fully in and the camera at the broadcast view"
            );
            Check(
                Enumerable.Range(0, TitleIntro.Pieces).All(i => intro.Drop(i, out int d) && d == 0),
                "every letter is in its place"
            );
            Check(
                TitleIntro.DropStart(5) - TitleIntro.DropStart(4)
                    > TitleIntro.DropStart(4) - TitleIntro.DropStart(3),
                "a beat before the ampersand"
            );
        }

        [Test]
        public void RandomClubsBehind()
        {
            IReadOnlyList<ClubData> clubs = WorldBuilder.Clubs;
            for (int seed = 0; seed < 40; seed++)
            {
                MatchSetup setup = TitleIntro
                    .Backdrop(
                        clubs,
                        Env(("SAP_INTRO_SEED", seed.ToString(CultureInfo.InvariantCulture)))
                    )
                    .Or(WorldBuilder.DefaultSetup);
                Check(
                    !string.Equals(setup.Home.Id, setup.Away.Id, StringComparison.Ordinal),
                    string.Create(CultureInfo.InvariantCulture, $"seed {seed}: two different clubs")
                );
            }

            Func<string, string> fixedSeed = Env(("SAP_INTRO_SEED", "7"));
            Check(
                string.Equals(
                    TitleIntro.Backdrop(clubs, fixedSeed).Select(s => s.Home.Id + s.Away.Id).Or(""),
                    TitleIntro
                        .Backdrop(clubs, fixedSeed)
                        .Select(s => s.Home.Id + s.Away.Id)
                        .Or("-"),
                    StringComparison.Ordinal
                ),
                "SAP_INTRO_SEED fixes the draw"
            );
            Check(
                !TitleIntro.Backdrop(clubs, Env(("SAP_HOME", "frolunda"))).HasValue,
                "SAP_HOME picks the clubs instead"
            );
        }

        /// <summary>Plays the intro over the start menu to its end, a step at a time, the world's clock running
        /// with it, and reports each step.</summary>
        private static void PlayOut(World world, Action<TitleIntro> each)
        {
            AppState app = world.App;
            world.Match.Paused = true;
            app.Screens.Push(new StartScreen(app));
            TitleIntroScreen intro = new(app);
            app.Screens.Push(intro);
            HashSet<MenuButton> none = [];
            for (int step = 0; step < 1000 && !intro.Timeline.Done; step++)
            {
                world.Time += Game.Step;
                intro.Update(world, none);
                each(intro.Timeline);
            }
        }

        [Test]
        public void TheHomeTifoIsUpAsTheCameraComesDown()
        {
            World world = new WorldBuilder().App(new AppState()).Build();
            TifoState tifo = world.Crowd.Tifo;
            bool early = false,
                fullWhenClear = true,
                upThroughMove = true,
                seenClear = false;
            PlayOut(
                world,
                t =>
                {
                    float lift = tifo.Lift(world.Time);
                    early |= t.Elapsed < TitleIntro.TifoAt - Game.Step && tifo.Up;
                    if (t.Reveal >= 1 && !t.Done)
                    {
                        seenClear = true;
                        fullWhenClear &= tifo.Up && lift >= 1f;
                    }

                    if (t.Elapsed is >= TitleIntro.MoveFrom and < TitleIntro.Seconds)
                    {
                        upThroughMove &= tifo.Up && tifo.Section == TifoMoments.StartSection;
                    }
                }
            );
            Check(!early, "the tifo waits for its beat in the black");
            Check(
                seenClear && fullWhenClear,
                "all the way up the stand by the time the arena is fully in"
            );
            Check(
                upThroughMove,
                "up over the far centre stand the whole way down to the broadcast view"
            );
            Check(!tifo.Up, "on its way down as the start menu shows");
        }

        [Test]
        public void NoTifoWhenFlagsAndTifoIsOff()
        {
            World world = new WorldBuilder().App(new AppState()).Build();
            world.Settings = world.Settings with { Supporters = false };
            bool raised = false;
            PlayOut(world, _ => raised |= world.Crowd.Tifo.Up || world.Crowd.Tifo.Raises > 0);
            Check(!raised, "the setting off keeps the tifo in the seats");
        }

        [Test]
        public void TheLogoIsShadedOverTheStandsUntilTheMenu()
        {
            TitleIntro intro = new();
            bool shadedInMove = true;
            while (!intro.Done)
            {
                intro.Update(Game.Step, pressed: false);
                if (intro.Elapsed is > TitleIntro.MoveFrom and <= TitleIntro.BackingFrom)
                {
                    shadedInMove &= intro.Backing >= 1f;
                }
            }

            Check(shadedInMove, "full shade behind the logo while the tifo passes it");
            Check(intro.Backing <= 0.001f, "no shade left when the start menu shows");
        }
    }
}
