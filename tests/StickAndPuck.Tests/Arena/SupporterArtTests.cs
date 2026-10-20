using System.Globalization;
using System.Numerics;
using System.Text.Json;
using StickAndPuck.App.Arena;
using StickAndPuck.App.Core;
using StickAndPuck.App.Replay;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Arena
{
    /// <summary>
    /// A19 supporter art: every club of both leagues has its slogans in its data file and a supporter sheet in
    /// the shared layout, the slogans never change the replay club fingerprint, and the tifo lies over a
    /// stand section the right way round.
    /// </summary>
    public sealed class SupporterArtTests : CheckedTest
    {
        private static IEnumerable<ClubData> AllClubs()
        {
            return Clubs.Load(League.Shl).Concat(Clubs.Load(League.Nhl));
        }

        [Test]
        public void EveryClubHasSlogansAndASheet()
        {
            foreach (ClubData club in AllClubs())
            {
                Check(
                    club.Supporters.TryGet(out ClubSupporters s)
                        && s.Tifo.Count is 1 or 2
                        && s.Tifo.All(l => l.Length > 0)
                        && s.Scarf.Length > 0
                        && s.Banner.Length > 0,
                    $"{club.Id} has a tifo, scarf and banner slogan"
                );
                Check(
                    ResourceLoader.Exists(SupporterArt.Sheet(club)),
                    $"{club.Id} has its supporter sheet ({SupporterArt.Sheet(club)})"
                );
            }
        }

        [Test]
        public void SlogansStayOutOfTheReplayFingerprint()
        {
            foreach (ClubData club in AllClubs())
            {
                ClubData bare = club with { Supporters = Maybe.None<ClubSupporters>() };
                Check(
                    string.Equals(
                        ReplayRecorder.ClubsHash(club, club),
                        ReplayRecorder.ClubsHash(bare, bare),
                        StringComparison.Ordinal
                    ),
                    $"{club.Id}'s slogans leave the club fingerprint as it was"
                );
                Check(
                    !Clubs.ToJson(bare).Contains("\"supporters\"", StringComparison.Ordinal),
                    $"{club.Id} without slogans writes no supporters block"
                );
                string json = Clubs.ToJson(club);
                ClubData back = JsonSerializer.Deserialize(json, ClubJsonContext.Default.ClubData);
                Check(
                    string.Equals(Clubs.ToJson(back), json, StringComparison.Ordinal),
                    $"{club.Id} with slogans round-trips"
                );
            }
        }

        [Test]
        public void LayoutHasEveryPiece()
        {
            SupporterArt art = SupporterArt.Bundled;
            SupporterArt.Piece tifo = art[SupporterArt.Tifo];
            Check(tifo.Frames.Length == 2, "the tifo is flat and rippled");
            Check(
                tifo.Frames.All(f => (int)f.Width == 160 && (int)f.Height == 120),
                "tifo frames are 160x120"
            );
            Check(art[SupporterArt.Scarf].Frames.Length == 3, "the scarf is held and waves");
            Check(art[SupporterArt.Flag].Frames.Length == 3, "the flag waves in three frames");
            Check(art[SupporterArt.Banner].Frames.Length == 2, "the banner is taut and sags");
            foreach (
                string piece in (string[])
                    [SupporterArt.Tifo, SupporterArt.Scarf, SupporterArt.Flag, SupporterArt.Banner]
            )
            {
                Check(
                    art[piece]
                        .Frames.All(f =>
                            f.X >= 0 && f.Y >= 0 && f.X + f.Width <= 256 && f.Y + f.Height <= 256
                        ),
                    $"every {piece} frame is inside the 256x256 sheet"
                );
            }
        }

        [Test]
        public void TifoCoversTheFarCentreStandFacingTheRink()
        {
            (CrowdSection Section, TifoRenderer.Cloth Cloth)[] cloths =
            [
                .. TifoRenderer.Cloths(ResourceLoader.ReadText(Assets.CrowdSeats), 4f / 3f),
            ];
            Check(
                cloths.Length == Enum.GetValues<CrowdSection>().Length,
                "a cloth for every section"
            );
            TifoRenderer.Cloth c = cloths.Single(x => x.Section == CrowdSection.FarCentre).Cloth;
            float width = Vector3.Distance(c.TopLeft, c.TopRight);
            float height = Vector3.Distance(c.TopLeft, c.BottomLeft);
            Check(
                width is > 12f and < 17f,
                $"as wide as the section ({width.ToString("F1", CultureInfo.InvariantCulture)} m)"
            );
            Check(MathF.Abs((width / height) - (4f / 3f)) < 0.01f, "keeps the art's 4:3 shape");
            Check(c.TopRight.X > c.TopLeft.X, "the art's left is on the left seen from the rink");
            Check(
                c.TopLeft.Y > c.BottomLeft.Y && c.TopLeft.Z < c.BottomLeft.Z,
                "its top runs up the stand, away from the rink"
            );
            Check(c.BottomLeft.Y > 2f, "held over the heads of the front row");
        }

        [Test]
        public void SapTifoNamesASection()
        {
            Check(
                TifoRenderer.Section("1") == Maybe.Some(CrowdSection.FarCentre),
                "1 is the far centre stand"
            );
            Check(
                TifoRenderer.Section("left_end") == Maybe.Some(CrowdSection.LeftEnd),
                "a section by its seats-file name"
            );
            Check(TifoRenderer.Section("") == Maybe.None<CrowdSection>(), "unset raises nothing");
            Check(
                TifoRenderer.Section("7") == Maybe.None<CrowdSection>(),
                "a number is not a section"
            );
            Check(
                TifoRenderer.Frame(0f) == 0 && TifoRenderer.Frame(0.4f) == 1,
                "the cloth ripples between its two frames"
            );
        }
    }
}
