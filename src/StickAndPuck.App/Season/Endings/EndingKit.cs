using StickAndPuck.App.Core;
using StickAndPuck.App.Hud;

namespace StickAndPuck.App.Season.Endings
{
    /// <summary>What every ending draws with: the people, the props, the HUD art and effects, and the two clubs.</summary>
    internal sealed class EndingKit(World world, EndingStage stage) : IDisposable
    {
        public World World { get; } = world;

        public EndingStage Stage { get; } = stage;

        /// <summary>Gets the user's club.</summary>
        public ClubData Club => World.Setup.Home;

        /// <summary>Gets the other club.</summary>
        public ClubData Other => World.Setup.Away;

        public FigureRenderer Figures { get; } = new();

        public EndingProps Props { get; } = new();

        public EndingHud Fx { get; } = new(17 + (int)stage.Kind);

        public Maybe<Screens.ScreenArt> Art { get; private set; }

        /// <summary>Gets the HUD art (font, crests).</summary>
        public HudArt Hud =>
            Art.TryGet(out Screens.ScreenArt a)
                ? a.Hud
                : throw new InvalidOperationException("the ending's art is not loaded");

        public void Load()
        {
            Figures.Load();
            Props.Load();
            Art = Screens.ScreenArt.Load();
        }

        /// <summary>A club's skaters, the forwards first, centres leading.</summary>
        public static IReadOnlyList<PlayerData> Skaters(ClubData club)
        {
            return
            [
                .. club
                    .Roster.Where(p => p.Position != PlayingPosition.G)
                    .OrderBy(p =>
                        p.Position == PlayingPosition.C ? 0
                        : p.Position is PlayingPosition.LW or PlayingPosition.RW ? 1
                        : 2
                    ),
            ];
        }

        /// <summary>A player's numbered kit atlas.</summary>
        public static string Kit(ClubData club, Side kit, PlayerData player)
        {
            return Assets.KitAtlas(club, kit, "skater", player.Number);
        }

        /// <summary>A club's street clothes for the staff model (art/scripts/make_street.py), the trainer's tracksuit until there are some.</summary>
        public static string Street(ClubData club)
        {
            string path = $"{Assets.KitFolder(club.League)}.{club.Id}_street_64.png";
            return ResourceLoader.Exists(path) ? path : Benches.BenchStaffArt.Trainer;
        }

        public void Dispose()
        {
            Figures.Dispose();
            Props.Dispose();
            if (Art.TryGet(out Screens.ScreenArt a))
            {
                a.Unload();
            }
        }
    }
}
