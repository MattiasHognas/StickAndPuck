namespace StickAndPuck.App.Core
{
    /// <summary>A kit's colours as palette hex strings such as "#c8574a".</summary>
    public sealed record KitColours
    {
        /// <summary>Gets the main jersey colour.</summary>
        public string Primary { get; init; } = "#c8574a";

        /// <summary>Gets the second colour: stripes, shoulders, numbers.</summary>
        public string Secondary { get; init; } = "#efe0c2";

        /// <summary>Gets the trim colour.</summary>
        public string Accent { get; init; } = "#2a2c40";

        /// <summary>Gets the helmet colour.</summary>
        public string Helmet { get; init; } = "#2a2c40";
    }
}
