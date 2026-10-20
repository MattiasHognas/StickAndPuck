using StickAndPuck.App.Core;

namespace StickAndPuck.App.Screens
{
    /// <summary>The title intro's timeline, played once when the game launches (<see cref="TitleIntroScreen"/>):
    /// black, the logo's letters drop in one by one with a bounce (a small shake as each word lands), a gold
    /// shine sweeps across them, then the camera comes down from the rafters into the arena while the logo
    /// rises to its place over the start menu. Any press skips to the menu. Rendering only: it counts its own
    /// seconds and nothing in the simulation reads it.</summary>
    internal sealed class TitleIntro
    {
        /// <summary>The logo's pieces in the order they drop: S T I C K, a beat, &amp;, P U C K.</summary>
        public const int Pieces = 10;

        /// <summary>When the first letter starts to drop, in seconds.</summary>
        public const float FirstDrop = 0.4f;

        /// <summary>Seconds between one letter and the next.</summary>
        public const float DropEvery = 0.11f;

        /// <summary>The extra beat before the ampersand, in seconds.</summary>
        public const float WordBeat = 0.25f;

        /// <summary>How long one letter takes to fall into place, in seconds.</summary>
        public const float DropSeconds = 0.28f;

        /// <summary>How far above its place a letter starts, in pixels.</summary>
        public const int DropHeight = 80;

        /// <summary>How far the frame shakes when the last letter of a word lands, in pixels, and for how long.</summary>
        public const int ShakePixels = 2;

        /// <summary>How long the landing shake lasts, in seconds.</summary>
        public const float ShakeSeconds = 0.05f;

        /// <summary>When the gold shine starts its sweep, in seconds, and how fast it moves, in pixels a second.</summary>
        public const float ShineAt = 2f;

        /// <summary>The shine's speed, in pixels a second.</summary>
        public const float ShineSpeed = 420f;

        /// <summary>When the camera starts down from the rafters, and when it reaches the broadcast view, in seconds.</summary>
        public const float MoveFrom = 2.4f;

        /// <summary>When the camera reaches the broadcast view, in seconds.</summary>
        public const float MoveTo = 4f;

        /// <summary>When the home tifo starts up the far stand, in seconds (W142): a beat before the camera moves,
        /// so the cloth is still unrolling as the arena fades in and is all the way up by the time the picture is
        /// clear, filling the camera's first shot; it stays up as the camera pulls back to the broadcast view
        /// and comes down as the start menu takes over.</summary>
        public const float TifoAt = MoveFrom - 1f;

        /// <summary>When the shade behind the logo starts to fade, in seconds: it is gone as the menu shows.</summary>
        public const float BackingFrom = MoveTo - 0.4f;

        /// <summary>How much faster than the camera move the picture fades in from black.</summary>
        public const float FadeSpeed = 2.5f;

        /// <summary>When the logo starts to rise from the centre, and how long it takes, in seconds.</summary>
        public const float RiseFrom = MoveFrom + 0.6f;

        /// <summary>How long the logo takes to rise to its place, in seconds.</summary>
        public const float RiseSeconds = 1f;

        /// <summary>When the intro hands over to the start menu, in seconds.</summary>
        public const float Seconds = 4.2f;

        /// <summary>Where the logo's top sits while the letters drop, and where it ends (the start screen's title row).</summary>
        public const int CentreY = 111;

        /// <summary>The start screen's title row.</summary>
        public const int FinalY = 22;

        /// <summary>Gets the seconds the intro has played.</summary>
        public float Elapsed { get; private set; }

        /// <summary>Gets a value indicating whether the intro has played out or was skipped.</summary>
        public bool Done { get; private set; }

        /// <summary>Gets a value indicating whether a press cut the intro short.</summary>
        public bool Skipped { get; private set; }

        /// <summary>Gets a value indicating whether the camera has started its move into the arena.</summary>
        public bool Moving => Elapsed >= MoveFrom;

        /// <summary>Gets a value indicating whether it is time for the home tifo to go up (<see cref="TifoAt"/>).</summary>
        public bool TifoTime => Elapsed >= TifoAt;

        /// <summary>Advances the intro by one step; any press ends it at once.</summary>
        /// <param name="dt">The step length in seconds.</param>
        /// <param name="pressed">Whether any key, button or click went down this step.</param>
        public void Update(float dt, bool pressed)
        {
            if (Done)
            {
                return;
            }

            if (pressed)
            {
                Skipped = true;
                Done = true;
                Console.WriteLine("SAP: title intro skipped");
                return;
            }

            Elapsed += dt;
            Done = Elapsed >= Seconds;
        }

        /// <summary>When a piece starts to drop, in seconds.</summary>
        public static float DropStart(int piece)
        {
            return FirstDrop + (piece * DropEvery) + (piece >= 5 ? WordBeat : 0);
        }

        /// <summary>Gets whether a piece has started to drop and how far above its place it is now, in pixels (negative is above; it overshoots below on the bounce).</summary>
        public bool Drop(int piece, out int dy)
        {
            float t = Elapsed - DropStart(piece);
            dy = 0;
            if (t < 0)
            {
                return false;
            }

            float u = Math.Min(1, t / DropSeconds);
            dy = (int)MathF.Round((1 - EaseOutBack(u)) * -DropHeight, MidpointRounding.ToEven);
            return true;
        }

        /// <summary>Gets how far the frame is shaken down now, in pixels: briefly after S T I C K and after P U C K land.</summary>
        public int Shake
        {
            get
            {
                foreach (int last in (ReadOnlySpan<int>)[4, Pieces - 1])
                {
                    float after = Elapsed - DropStart(last) - DropSeconds;
                    if (after is > 0 and < ShakeSeconds)
                    {
                        return ShakePixels;
                    }
                }

                return 0;
            }
        }

        /// <summary>Gets the shine band's position along the logo, in pixels from its left edge (it slants back a pixel every two rows).</summary>
        public float ShineBand => ((Elapsed - ShineAt) * ShineSpeed) - 40;

        /// <summary>Gets how far the camera has come from the rafters to the broadcast view, 0 to 1, eased.</summary>
        public float CameraMove => EaseInOut((Elapsed - MoveFrom) / (MoveTo - MoveFrom));

        /// <summary>Gets how much of the arena shows through the black, 0 (all black) to 1 (no black).</summary>
        public float Reveal =>
            Math.Clamp((Elapsed - MoveFrom) / (MoveTo - MoveFrom) * FadeSpeed, 0, 1);

        /// <summary>Gets how strongly the arena is shaded behind the logo, 0 to 1: full while the camera comes down
        /// past the tifo, easing out as the logo settles in its place, so the start menu shows as always.</summary>
        public float Backing => 1 - EaseInOut((Elapsed - BackingFrom) / (Seconds - BackingFrom));

        /// <summary>Gets the logo's top row now: centred while the letters drop, rising to the start screen's title row.</summary>
        public int LogoY
        {
            get
            {
                float rise = EaseInOut((Elapsed - RiseFrom) / RiseSeconds);
                return (int)
                    MathF.Round(CentreY + ((FinalY - CentreY) * rise), MidpointRounding.ToEven);
            }
        }

        /// <summary>Whether the intro plays on this launch: SAP_INTRO=1 always, SAP_INTRO=0 never; otherwise only on
        /// a plain launch, so scripted and headless runs (SAP_SCREEN_KEYS, an SAP_SCREEN or SAP_SCENE other than
        /// start, SAP_SCREENSHOT, SAP_RECORD, SAP_STEPS, SAP_REPLAY) open on the start menu as before.</summary>
        /// <param name="env">Reads an environment variable ("" when unset).</param>
        public static bool Plays(Func<string, string> env)
        {
            string intro = env("SAP_INTRO");
            if (intro.Length > 0)
            {
                return !string.Equals(intro, "0", StringComparison.Ordinal);
            }

            static bool OtherThanStart(string value)
            {
                return value.Length > 0
                    && !string.Equals(value, "start", StringComparison.OrdinalIgnoreCase);
            }

            return env("SAP_SCREEN_KEYS").Length == 0
                && !OtherThanStart(env("SAP_SCREEN"))
                && !OtherThanStart(env("SAP_SCENE"))
                && env("SAP_SCREENSHOT").Length == 0
                && env("SAP_RECORD").Length == 0
                && env("SAP_STEPS").Length == 0
                && env("SAP_REPLAY").Length == 0;
        }

        /// <summary>Whether anything went down this step: a menu press, any key, a click or any pad button.</summary>
        public static bool AnyPress(InputFrame frame, IReadOnlySet<MenuButton> menu)
        {
            return menu.Count > 0
                || frame.Pressed.Length > 0
                || frame.Mouse != 0
                || frame.Pads.Any(p => p.Pressed != 0);
        }

        /// <summary>The clubs on the rink behind the intro and the start menu: two different clubs drawn at random
        /// (SAP_INTRO_SEED fixes the draw), or None when SAP_HOME or SAP_AWAY picks them (or there are too few).</summary>
        /// <param name="clubs">The league's clubs.</param>
        /// <param name="env">Reads an environment variable ("" when unset).</param>
        public static Maybe<MatchSetup> Backdrop(
            IReadOnlyList<ClubData> clubs,
            Func<string, string> env
        )
        {
            if (clubs.Count < 2 || env("SAP_HOME").Length > 0 || env("SAP_AWAY").Length > 0)
            {
                return Maybe.None<MatchSetup>();
            }

            Random dice = int.TryParse(
                env("SAP_INTRO_SEED"),
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out int seed
            )
                ? Seeds.Dice(seed)
                : Random.Shared;
            int home = dice.Next(clubs.Count);
            int away = (home + 1 + dice.Next(clubs.Count - 1)) % clubs.Count;
            return new MatchSetup { Home = clubs[home], Away = clubs[away] };
        }

        /// <summary>Overshoots and settles back: the letters' bounce.</summary>
        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f,
                c3 = c1 + 1;
            float u = t - 1;
            return 1 + (c3 * u * u * u) + (c1 * u * u);
        }

        /// <summary>A smooth start and stop, clamped to 0..1.</summary>
        public static float EaseInOut(float t)
        {
            return 0.5f - (0.5f * MathF.Cos(MathF.PI * Math.Clamp(t, 0, 1)));
        }
    }
}
