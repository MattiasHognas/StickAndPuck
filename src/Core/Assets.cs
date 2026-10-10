namespace StickAndPuck.Core;

/// <summary>Every asset path in one place. Paths are relative to the asset folder that <see cref="AssetRoot"/> moves into.</summary>
public static class Assets
{
    /// <summary>The club data files.</summary>
    public const string ClubFolder = "data/clubs";

    /// <summary>The skater model with its idle, skate and shoot clips.</summary>
    public const string Skater = "art/player/skater.glb";

    /// <summary>The goalie model.</summary>
    public const string Goalie = "art/player/goalie.glb";

    /// <summary>The referee model.</summary>
    public const string Referee = "art/player/referee.glb";

    /// <summary>The referee's kit atlas.</summary>
    public const string RefereeKit = "art/player/referee_ref_64.png";

    /// <summary>The arena model (A1).</summary>
    public const string Arena = "art/scene/arena.glb";

    /// <summary>The arena mesh list in Raylib load order, with each mesh's part and material.</summary>
    public const string ArenaMeshes = "art/scene/arena_meshes.json";

    /// <summary>The ad board text strip.</summary>
    public const string AdStrip = "art/ads/ads_53x80.png";

    /// <summary>The skate scratch mask over the ice (A4, art/scripts/ice_scratches.py).</summary>
    public const string IceScratches = "art/scene/ice_scratches.png";

    /// <summary>The palette every frame snaps to.</summary>
    public const string Palette = "art/palette/stickandpuck_dusk.gpl";

    /// <summary>The HUD layout from A3: font, scorebug slots, panels, icons and crests, each naming its PNG under art/ui/.</summary>
    public const string UiLayout = "art/ui/ui.json";

    /// <summary>The shader folder.</summary>
    public const string Shaders = "shaders";

    /// <summary>
    /// The 64x64 kit atlas for a club, side and role, from art/kits/ (made by the kit script).
    /// A numbered atlas from art/kits/numbers/ carries the player's number on the back (A4); without one
    /// it falls back to the club kit, then to the generic home or away kit in art/player/.
    /// </summary>
    /// <param name="clubId">The club id, as in its data file.</param>
    /// <param name="side">Home or away kit.</param>
    /// <param name="role">"skater" or "goalie".</param>
    /// <param name="number">The player's jersey number; 0 for the club kit without one.</param>
    /// <returns>A path to an existing texture.</returns>
    public static string KitAtlas(string clubId, Side side, string role, int number = 0)
    {
        string kit = side == Side.Home ? "home" : "away";
        string numbered = $"art/kits/numbers/{clubId}_{kit}_{role}_{number}_64.png";
        if (number > 0 && File.Exists(numbered)) return numbered;
        string club = $"art/kits/{clubId}_{kit}_{role}_64.png";
        return File.Exists(club) ? club : $"art/player/{role}_{kit}_64.png";
    }
}
