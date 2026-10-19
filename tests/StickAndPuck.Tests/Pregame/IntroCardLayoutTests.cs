using System.Text.Json;
using StickAndPuck.App.AI.Tactics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Pregame;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Pregame
{
    /// <summary>The A22 stats card layout in intro_card.json: bundled, every rect inside the card, and a
    /// stat icon for every row and a playstyle badge for every style, forecheck and entry.</summary>
    public sealed class IntroCardLayoutTests : CheckedTest
    {
        [Test]
        public void CardArtIsBundled()
        {
            foreach (
                string path in new[] { Assets.IntroCard, Assets.IntroIcons, Assets.IntroCardLayout }
            )
            {
                Check(ResourceLoader.Exists(path), $"{path} is embedded");
            }
        }

        [Test]
        public void EveryRectFitsTheCard()
        {
            using JsonDocument doc = JsonDocument.Parse(
                ResourceLoader.ReadText(Assets.IntroCardLayout)
            );
            JsonElement card = doc.RootElement.GetProperty("card");
            int w = card.GetProperty("size")[0].GetInt32();
            int h = card.GetProperty("size")[1].GetInt32();
            List<(string Name, JsonElement Rect)> all =
            [
                .. card.GetProperty("slots").EnumerateObject().Select(s => (s.Name, s.Value)),
            ];
            foreach (JsonElement row in card.GetProperty("rows").EnumerateArray())
            {
                string name = row.GetProperty("name").GetString() ?? "";
                foreach (JsonProperty p in row.EnumerateObject())
                {
                    if (p.Value.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    if (p.Value[0].ValueKind == JsonValueKind.Array)
                    {
                        all.AddRange(p.Value.EnumerateArray().Select(r => ($"{name}.{p.Name}", r)));
                    }
                    else
                    {
                        all.Add(($"{name}.{p.Name}", p.Value));
                    }
                }
            }

            foreach ((string name, JsonElement r) in all)
            {
                int x = r[0].GetInt32(),
                    y = r[1].GetInt32(),
                    rw = r[2].GetInt32(),
                    rh = r[3].GetInt32();
                Check(
                    x >= 0 && y >= 0 && rw > 0 && rh > 0 && x + rw <= w && y + rh <= h,
                    $"{name} lies inside the card"
                );
            }
        }

        [Test]
        public void EveryRowAndStyleHasAnIcon()
        {
            using JsonDocument doc = JsonDocument.Parse(
                ResourceLoader.ReadText(Assets.IntroCardLayout)
            );
            JsonElement icons = doc.RootElement.GetProperty("icons");
            JsonElement stat = icons.GetProperty("stat");
            JsonElement style = icons.GetProperty("style");
            foreach (
                JsonElement row in doc
                    .RootElement.GetProperty("card")
                    .GetProperty("rows")
                    .EnumerateArray()
            )
            {
                string name = row.GetProperty("name").GetString() ?? "";
                Check(stat.TryGetProperty(name, out _), $"row {name} has a stat icon");
            }

            List<string> keys =
            [
                .. Enum.GetValues<PlayStyle>().Select(IntroCardArt.StyleKey),
                .. Enum.GetValues<Forecheck>().Select(IntroCardArt.StyleKey),
                .. Enum.GetValues<ZoneEntry>().Select(IntroCardArt.StyleKey),
            ];
            foreach (string key in keys)
            {
                Check(style.TryGetProperty(key, out _), $"{key} has a playstyle badge");
            }

            foreach (string result in new[] { "win", "loss", "otloss", "none" })
            {
                Check(
                    icons.GetProperty("form").TryGetProperty(result, out _),
                    $"form dot {result}"
                );
            }
        }
    }
}
