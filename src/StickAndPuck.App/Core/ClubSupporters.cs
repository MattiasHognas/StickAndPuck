namespace StickAndPuck.App.Core
{
    /// <summary>A19: what a club's supporters write on their tifo, scarves and banners, in the club's own language
    /// (Swedish for the SHL, English, or French where natural, for the NHL). art/scripts/make_supporters.py
    /// paints it into the club's supporter sheet (art/crowd/supporters/{league}/{id}.png) in the HUD pixel
    /// font's capitals; it never plays a match, so the replay club fingerprint leaves it out.</summary>
    public sealed record ClubSupporters
    {
        /// <summary>Gets the tifo's one or two lines under the crest.</summary>
        public IReadOnlyList<string> Tifo { get; init; } = [];

        /// <summary>Gets the text knitted into the scarves.</summary>
        public string Scarf { get; init; } = "";

        /// <summary>Gets the slogan on the banner two fans hold up.</summary>
        public string Banner { get; init; } = "";
    }
}
