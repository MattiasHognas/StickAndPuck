using System.Text.Json;
using Raylib_cs;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Arena
{
    /// <summary>A19: the layout every club's supporter sheet shares (art/crowd/supporters.json from
    /// art/scripts/make_supporters.py): one 256x256 sheet per club, art/crowd/supporters/{league}/{id}.png, with
    /// the tifo (flat, ripple), the scarf (held, two waving phases), the hand flag (three waving phases) and the
    /// slogan banner (taut, sagging), each frame's rect in texels and a suggested size in metres.</summary>
    internal sealed class SupporterArt
    {
        /// <summary>The big cloth raised over a stand section.</summary>
        public const string Tifo = "tifo";

        /// <summary>A scarf held up over the head.</summary>
        public const string Scarf = "scarf";

        /// <summary>A hand flag on a stick.</summary>
        public const string Flag = "flag";

        /// <summary>A sign two fans hold up between two sticks.</summary>
        public const string Banner = "banner";

        private readonly Dictionary<string, Piece> pieces;

        /// <summary>One kind of supporter art: its frames' rects in the sheet and its size in the stands.</summary>
        /// <param name="Frames">The frames' rects, in frame order.</param>
        /// <param name="Width">The suggested width in metres.</param>
        /// <param name="Height">The suggested height in metres.</param>
        internal readonly record struct Piece(Rectangle[] Frames, float Width, float Height);

        private SupporterArt(Dictionary<string, Piece> pieces)
        {
            this.pieces = pieces;
        }

        /// <summary>Gets the layout bundled with the game.</summary>
        public static SupporterArt Bundled =>
            Parse(ResourceLoader.ReadText(Assets.SupporterLayout));

        /// <summary>Reads a layout file.</summary>
        /// <param name="json">art/crowd/supporters.json.</param>
        /// <returns>The layout.</returns>
        internal static SupporterArt Parse(string json)
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            Dictionary<string, Piece> read = [with(StringComparer.Ordinal)];
            foreach (JsonProperty p in doc.RootElement.GetProperty("pieces").EnumerateObject())
            {
                Rectangle[] frames =
                [
                    .. p
                        .Value.GetProperty("rects")
                        .EnumerateArray()
                        .Select(r => new Rectangle(
                            r[0].GetInt32(),
                            r[1].GetInt32(),
                            r[2].GetInt32(),
                            r[3].GetInt32()
                        )),
                ];
                JsonElement m = p.Value.GetProperty("metres");
                read[p.Name] = new Piece(frames, m[0].GetSingle(), m[1].GetSingle());
            }

            return new SupporterArt(read);
        }

        /// <summary>Gets one kind of supporter art.</summary>
        /// <param name="name"><see cref="Tifo"/>, <see cref="Scarf"/>, <see cref="Flag"/> or <see cref="Banner"/>.</param>
        /// <returns>Its frames and size.</returns>
        public Piece this[string name] => pieces[name];

        /// <summary>Gets the sheet of a club's supporter art; check <see cref="ResourceLoader.Exists"/>, a club
        /// added without running make_supporters.py has none.</summary>
        /// <param name="club">The club.</param>
        /// <returns>The resource path.</returns>
        public static string Sheet(ClubData club)
        {
            return $"art.crowd.supporters.{Leagues.Id(club.League)}.{club.Id}.png";
        }
    }
}
