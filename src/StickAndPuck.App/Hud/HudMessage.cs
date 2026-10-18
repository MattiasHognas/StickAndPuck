namespace StickAndPuck.App.Hud
{
    /// <summary>A centre-screen banner.</summary>
    /// <param name="Panel">"panel", "panel_gold" or "panel_red".</param>
    /// <param name="Title">The big first line.</param>
    /// <param name="Detail">The second line, or empty.</param>
    /// <param name="Crest">The club id whose crest sits left of the text, or empty for none.</param>
    /// <param name="Until">World time the banner goes away; infinity keeps it up.</param>
    /// <param name="Icon">An icon from the icon sheet drawn left of the title, or empty for none.</param>
    /// <param name="Intermission">True for an end-of-period banner, which stays up for the whole intermission.</param>
    internal sealed record HudMessage(
        string Panel,
        string Title,
        string Detail,
        string Crest,
        float Until,
        string Icon = "",
        bool Intermission = false
    )
    {
        /// <summary>Gets the extra gold lines under the detail: game-winning goal, shutout, first goal of the season.</summary>
        public IReadOnlyList<string> Notes { get; init; } = [];

        public IReadOnlyList<string> Lines => Notes;
    }
}
