using System.Text.Json;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.News
{
    /// <summary>The season news deck: the weighted cards in data/season/news.json.</summary>
    public sealed record NewsDeck
    {
        /// <summary>The deck's data file.</summary>
        public const string File = "data/season/news.json";

        private static readonly Lazy<NewsDeck> Bundled = new(() =>
            Read(ResourceLoader.ReadText(File))
        );

        /// <summary>Gets the deck the game ships with.</summary>
        public static NewsDeck Default => Bundled.Value;

        /// <summary>Gets the cards.</summary>
        public IReadOnlyList<NewsCard> Cards { get; init; } = [];

        /// <summary>Reads a deck from its JSON.</summary>
        public static NewsDeck Read(string json)
        {
            return JsonSerializer.Deserialize(json, NewsJsonContext.Default.NewsDeck)
                ?? throw new InvalidDataException($"{File} is empty");
        }

        /// <summary>Gets a card by id.</summary>
        public Maybe<NewsCard> Card(string id)
        {
            return Cards.FirstOrNone(c => string.Equals(c.Id, id, StringComparison.Ordinal));
        }
    }
}
