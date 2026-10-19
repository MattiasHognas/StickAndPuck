using System.Globalization;
using System.Text.Json;
using StickAndPuck.App.Core;
using StickAndPuck.App.Render;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Core
{
    /// <summary>A16: every NHL club has its own kits and crest, and the palette grown for their kit
    /// colours still fits the post shaders.</summary>
    public sealed class NhlKitsTests : CheckedTest
    {
        [Test]
        public void EveryClubHasKitsAndACrest()
        {
            using JsonDocument ui = JsonDocument.Parse(ResourceLoader.ReadText(Assets.UiLayout));
            JsonElement cells = ui.RootElement.GetProperty("crests").GetProperty("cells");
            IReadOnlyList<ClubData> clubs = [.. Clubs.Load(League.Shl), .. Clubs.Load(League.Nhl)];
            foreach (ClubData club in clubs)
            {
                Check(cells.TryGetProperty(club.Id, out _), $"{club.Id} has a crest cell");
                foreach (Side side in new[] { Side.Home, Side.Away })
                {
                    foreach (string role in new[] { "skater", "goalie", "skater_bare" })
                    {
                        string path = Assets.KitAtlas(club, side, role);
                        Check(
                            path.StartsWith(
                                Assets.KitFolder(club.League) + ".",
                                StringComparison.Ordinal
                            ),
                            $"{club.Id} {side} {role} has its own kit atlas ({path})"
                        );
                    }

                    Player(club, side);
                }
            }
        }

        [Test]
        public void PaletteFitsThePostShaders()
        {
            Check(
                Palette.Current.Count <= Palette.MaxColours,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the palette has {Palette.Current.Count} colours, the post shader takes {Palette.MaxColours}"
                )
            );
            Check(
                Palette.KitStart < Palette.Current.Count,
                "the palette keeps a kit block after the arena colours"
            );
            string array = string.Create(
                CultureInfo.InvariantCulture,
                $"uniform vec3 palette[{Palette.MaxColours}];"
            );
            foreach (string shader in new[] { "shaders/post.fs", "shaders/web/post.fs" })
            {
                Check(
                    ResourceLoader.ReadText(shader).Contains(array, StringComparison.Ordinal),
                    $"{shader} declares {array}"
                );
            }
        }

        /// <summary>Every rostered player wears his own numbered atlas.</summary>
        private void Player(ClubData club, Side side)
        {
            foreach (PlayerData p in club.Roster)
            {
                string role = p.Position == PlayingPosition.G ? "goalie" : "skater";
                string path = Assets.KitAtlas(club, side, role, p.Number);
                Check(
                    path.StartsWith(
                        Assets.KitFolder(club.League) + ".numbers.",
                        StringComparison.Ordinal
                    ),
                    $"{club.Id} {side} #{p.Number.ToString(CultureInfo.InvariantCulture)} has a numbered atlas"
                );
            }
        }
    }
}
