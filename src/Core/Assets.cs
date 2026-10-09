namespace StickAndPuck.Core;

/// <summary>Every asset path in one place. Paths are relative to the executable, where the build copies them.</summary>
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

    /// <summary>The palette every frame snaps to.</summary>
    public const string Palette = "art/palette/stickandpuck_dusk.gpl";

    /// <summary>The shader folder.</summary>
    public const string Shaders = "shaders";

    /// <summary>
    /// The 64x64 kit atlas for a club, side and role, from art/kits/ (made by the kit script).
    /// Falls back to the generic home or away kit in art/player/ until that club's kits exist.
    /// </summary>
    /// <param name="clubId">The club id, as in its data file.</param>
    /// <param name="side">Home or away kit.</param>
    /// <param name="role">"skater" or "goalie".</param>
    /// <returns>A path to an existing texture.</returns>
    public static string KitAtlas(string clubId, Side side, string role)
    {
        string kit = side == Side.Home ? "home" : "away";
        string club = $"art/kits/{clubId}_{kit}_{role}_64.png";
        return File.Exists(club) ? club : $"art/player/{role}_{kit}_64.png";
    }
}
