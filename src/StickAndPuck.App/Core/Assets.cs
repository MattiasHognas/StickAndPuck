using System.Globalization;

namespace StickAndPuck.App.Core
{
    /// <summary>Every bundled asset name in one place, resolved by <see cref="ResourceLoader"/>.</summary>
    public static class Assets
    {
        /// <summary>The club data files.</summary>
        public const string ClubFolder = "data.clubs";

        /// <summary>The NHL club data files (W115), kept apart from the SHL clubs in <see cref="ClubFolder"/>.</summary>
        public const string NhlClubFolder = "data.clubs.nhl";

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

        /// <summary>The shader folder.</summary>
        public const string Shaders = "shaders";

        /// <summary>The 64x64 kit atlas for a club, side and role, from art/kits/ (made by the kit script).
        /// A numbered atlas from art/kits/numbers/ carries the player's number on the back (A4); without one
        /// it falls back to the club kit, then to the generic home or away kit in art/player/.</summary>
        public static string KitAtlas(string clubId, Side side, string role, int number = 0)
        {
            string kit = side == Side.Home ? "home" : "away";
            string numbered = string.Create(
                CultureInfo.InvariantCulture,
                $"art.kits.numbers.{clubId}_{kit}_{role}_{number}_64.png"
            );
            if (number > 0 && ResourceLoader.Exists(numbered))
            {
                return numbered;
            }

            string club = $"art.kits.{clubId}_{kit}_{role}_64.png";
            return ResourceLoader.Exists(club) ? club : $"art.player.{role}_{kit}_64.png";
        }
    }
}
