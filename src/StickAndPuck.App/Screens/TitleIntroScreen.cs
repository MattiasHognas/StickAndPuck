using StickAndPuck.App.Arena;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Screens
{
    /// <summary>The title intro over the start screen when the game launches (<see cref="TitleIntro"/> has the
    /// timeline): it draws the black, the dropping letters and their shine, while the camera
    /// (<see cref="Camera.TitleCamera"/>) comes down from the rafters and the crowd gets up
    /// (<see cref="IntroCrowd"/>). When it has played, or on any key, button or click, it closes and the start
    /// menu shows as always, the logo already in its place.</summary>
    internal sealed class TitleIntroScreen(AppState app) : Screen(app)
    {
        private Maybe<TitleLetters> letters;
        private bool roused;
        private bool tifo;

        /// <summary>Gets the intro's timeline.</summary>
        public TitleIntro Timeline { get; } = new();

        /// <inheritdoc/>
        public override bool Dims => false;

        /// <summary>Gets the intro playing in a world's screens, or None when there is none.</summary>
        public static Maybe<TitleIntro> Playing(World world)
        {
            return world.App.Screens.Top.TryGet(out Screen top) && top is TitleIntroScreen intro
                ? intro.Timeline
                : Maybe.None<TitleIntro>();
        }

        /// <inheritdoc/>
        public override void Update(World world, IReadOnlySet<MenuButton> press)
        {
            Timeline.Update(Game.Step, TitleIntro.AnyPress(DeviceInput.Current, press));
            if (Timeline.TifoTime && !tifo)
            {
                tifo = true;
                IntroCrowd.RaiseTifo(world); // up the stand before the picture comes in, so the first shot is full of it
            }

            if (Timeline.Moving && !roused)
            {
                roused = true;
                IntroCrowd.Rise(world);
            }

            if (!Timeline.Done)
            {
                return;
            }

            IntroCrowd.Settle(world);
            if (letters.TryGet(out TitleLetters art))
            {
                art.Unload();
                letters = Maybe.None<TitleLetters>();
            }

            Flow.Pop();
            Console.WriteLine("SAP: title intro done");
        }

        /// <inheritdoc/>
        public override void Draw(ScreenArt art, World world)
        {
            if (!letters.TryGet(out TitleLetters loaded))
            {
                loaded = TitleLetters.Load();
                letters = loaded;
            }

            loaded.DrawBlack(Timeline.Reveal);
            int x = (Game.Width - loaded.Width) / 2,
                y = Timeline.LogoY + Timeline.Shake;
            loaded.DrawBacking(Timeline.Backing, x, y);
            loaded.DrawLetters(Timeline, x, y);
            loaded.DrawShine(Timeline.ShineBand, x, y);
        }
    }
}
