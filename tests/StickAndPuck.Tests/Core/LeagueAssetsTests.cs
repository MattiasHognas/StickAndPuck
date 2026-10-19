using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Core
{
    /// <summary>League folders keep bundled clubs and kit textures separate.</summary>
    public sealed class LeagueAssetsTests : CheckedTest
    {
        [Test]
        public void ClubFoldersContainOnlyTheirLeague()
        {
            foreach (League league in Leagues.All)
            {
                string folder = $"data/clubs/{Leagues.Id(league)}";
                string[] files = [.. ResourceLoader.List(folder, ".json")];
                IReadOnlyList<ClubData> clubs = Clubs.Load(league);
                Check(
                    files.Length > 0 && files.Length == clubs.Count,
                    $"{folder} loads every club"
                );
                Check(clubs.All(c => c.League == league), $"{folder} contains only {league} clubs");
                foreach (string id in clubs.Select(club => club.Id))
                {
                    Check(ResourceLoader.Exists($"{folder}/{id}.json"), $"{id} is in {folder}");
                    Check(
                        !ResourceLoader.Exists($"data/clubs/{id}.json"),
                        $"{id} has no root-level duplicate"
                    );
                }
            }
        }

        [Test]
        public void ShlKitsAndPlayerNumbersResolveFromLeagueFolder()
        {
            foreach (ClubData club in Clubs.Load(League.Shl))
            {
                foreach (Side side in new[] { Side.Home, Side.Away })
                {
                    foreach (PlayerData player in club.Roster)
                    {
                        string role = player.Position == PlayingPosition.G ? "goalie" : "skater";
                        string kit = side == Side.Home ? "home" : "away";
                        string number = player.Number.ToString(CultureInfo.InvariantCulture);
                        string path = Assets.KitAtlas(club, side, role, player.Number);
                        Check(
                            string.Equals(
                                path,
                                $"art.kits.shl.numbers.{club.Id}_{kit}_{role}_{number}_64.png",
                                StringComparison.Ordinal
                            ) && ResourceLoader.Exists(path),
                            $"{club.Id} {kit} #{number} resolves its numbered kit"
                        );
                    }
                    foreach (string role in new[] { "skater", "goalie", "skater_bare" })
                    {
                        string path = Assets.KitAtlas(club, side, role);
                        Check(
                            path.StartsWith("art.kits.shl.", StringComparison.Ordinal)
                                && ResourceLoader.Exists(path),
                            $"{club.Id} {side} {role} resolves its club kit"
                        );
                    }
                }
            }
        }

        [Test]
        public void MissingKitFallsBackWithoutUsingAnotherLeague()
        {
            ClubData shl = Clubs.Load(League.Shl)[0];
            ClubData sameIdInNhl = shl with { League = League.Nhl };
            string path = Assets.KitAtlas(sameIdInNhl, Side.Home, "skater", shl.Roster[0].Number);
            Check(
                string.Equals(path, "art.player.skater_home_64.png", StringComparison.Ordinal)
                    && ResourceLoader.Exists(path),
                "a missing NHL kit uses the generic texture even when SHL has the same club id"
            );
        }
    }
}
