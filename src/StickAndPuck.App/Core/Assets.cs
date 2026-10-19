using System.Globalization;

namespace StickAndPuck.App.Core
{
    /// <summary>Every bundled asset name in one place, resolved by <see cref="ResourceLoader"/>.</summary>
    public static class Assets
    {
        /// <summary>The SHL club data files.</summary>
        public const string ShlClubFolder = "data.clubs.shl";

        /// <summary>The NHL club data files.</summary>
        public const string NhlClubFolder = "data.clubs.nhl";

        /// <summary>Gets the club data folder for a league.</summary>
        public static string ClubFolder(League league)
        {
            return $"data.clubs.{Leagues.Id(league)}";
        }

        /// <summary>Gets the kit atlas folder for a league.</summary>
        public static string KitFolder(League league)
        {
            return $"art.kits.{Leagues.Id(league)}";
        }

        /// <summary>The skater model with its idle, skate and shoot clips.</summary>
        public const string Skater = "art.player.skater.glb";

        /// <summary>The goalie model.</summary>
        public const string Goalie = "art.player.goalie.glb";

        /// <summary>The referee model.</summary>
        public const string Referee = "art.player.referee.glb";

        /// <summary>The referee's kit atlas.</summary>
        public const string RefereeKit = "art.player.referee_ref_64.png";

        /// <summary>The arena model (A1).</summary>
        public const string Arena = "art.scene.arena.glb";

        /// <summary>The arena mesh list in Raylib load order, with each mesh's part and material.</summary>
        public const string ArenaMeshes = "art.scene.arena_meshes.json";

        /// <summary>One seat per spectator in the stands: position, row and section (A18, art/scripts/export_arena.py).</summary>
        public const string CrowdSeats = "art.scene.crowd_seats.json";

        /// <summary>The crowd's pose frames and shirt masks (A18, art/scripts/build_crowd.py and crowd_atlas.py).</summary>
        public const string CrowdAtlas = "art.crowd.crowd_256.png";

        /// <summary>The ad board text strip.</summary>
        public const string AdStrip = "art.ads.ads_53x80.png";

        /// <summary>The skate scratch mask over the ice (A4, art/scripts/ice_scratches.py).</summary>
        public const string IceScratches = "art.scene.ice_scratches.png";

        /// <summary>The palette every frame snaps to.</summary>
        public const string Palette = "art.palette.stickandpuck_dusk.gpl";

        /// <summary>The HUD layout from A3: font, scorebug slots, panels, icons and crests, each naming its PNG under art/ui/.</summary>
        public const string UiLayout = "art.ui.ui.json";

        /// <summary>The dressing room backdrop behind the screen between season games, 480x270 (A20). Until it is
        /// drawn the screen dims the rink instead.</summary>
        public const string DressingRoomBackdrop = "art.ui.dressing_room.png";

        /// <summary>The coach's portrait on the dressing room screen, 64x80 (A20). Until it is drawn the
        /// screen shows a framed placeholder.</summary>
        public const string CoachPortrait = "art.ui.coach_portrait.png";

        /// <summary>The coach in every <see cref="CoachMood"/>, 64x80 cells left to right in the enum's order (A20).</summary>
        public const string CoachMoods = "art.ui.coach_moods.png";

        /// <summary>White ink mask of the dressing room jerseys' body, 480x270, tinted with the club's primary kit colour (A20).</summary>
        public const string DressingRoomJersey = "art.ui.dressing_room_jersey.png";

        /// <summary>White ink mask of the dressing room jerseys' stripes and numbers, 480x270, tinted with the club's secondary colour (A20).</summary>
        public const string DressingRoomTrim = "art.ui.dressing_room_trim.png";

        /// <summary>The pre-game stats card's versus panel, 300x196 (A22, art/scripts/make_intro_card.py).</summary>
        public const string IntroCard = "art.ui.intro_card.png";

        /// <summary>The stats card's stat icons, form dots and playstyle badges (A22).</summary>
        public const string IntroIcons = "art.ui.intro_icons.png";

        /// <summary>The stats card's layout: slots, rows and icon cells, card-local (A22).</summary>
        public const string IntroCardLayout = "art.ui.intro_card.json";

        /// <summary>The shader folder.</summary>
        public const string Shaders = "shaders";

        /// <summary>The 64x64 kit atlas for a club, side and role, from art/kits/{league}/.
        /// A numbered atlas from its numbers/ folder carries the player's number on the back (A4); without one
        /// it falls back to the club kit, then to the generic home or away kit in art/player/.</summary>
        public static string KitAtlas(ClubData club, Side side, string role, int number = 0)
        {
            string kit = side == Side.Home ? "home" : "away";
            string folder = KitFolder(club.League);
            string numbered = string.Create(
                CultureInfo.InvariantCulture,
                $"{folder}.numbers.{club.Id}_{kit}_{role}_{number}_64.png"
            );
            if (number > 0 && ResourceLoader.Exists(numbered))
            {
                return numbered;
            }

            string atlas = $"{folder}.{club.Id}_{kit}_{role}_64.png";
            return ResourceLoader.Exists(atlas) ? atlas : $"art.player.{role}_{kit}_64.png";
        }
    }
}
