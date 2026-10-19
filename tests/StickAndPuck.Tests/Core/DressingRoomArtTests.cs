using StickAndPuck.App.Core;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Core
{
    /// <summary>A20's dressing room art is bundled at the sizes the dressing room screen draws it.</summary>
    public sealed class DressingRoomArtTests : CheckedTest
    {
        [Test]
        public void BackdropAndMasksFillTheFrame()
        {
            foreach (
                string path in new[]
                {
                    Assets.DressingRoomBackdrop,
                    Assets.DressingRoomJersey,
                    Assets.DressingRoomTrim,
                }
            )
            {
                Check(ResourceLoader.Exists(path), $"{path} is embedded");
                Check(Size(path) == (Game.Width, Game.Height), $"{path} is 480x270");
            }
        }

        [Test]
        public void CoachHasOneCellPerMood()
        {
            Check(Size(Assets.CoachPortrait) == (64, 80), "the portrait is 64x80");
            Check(
                Size(Assets.CoachMoods) == (64 * Enum.GetValues<CoachMood>().Length, 80),
                "the mood sheet has a 64x80 cell per CoachMood"
            );
        }

        /// <summary>A PNG's width and height from its IHDR chunk.</summary>
        private static (int W, int H) Size(string path)
        {
            using Stream stream = ResourceLoader.Open(path);
            byte[] head = new byte[24];
            stream.ReadExactly(head);
            return (Int(head, 16), Int(head, 20));
        }

        private static int Int(byte[] b, int at)
        {
            return (b[at] << 24) | (b[at + 1] << 16) | (b[at + 2] << 8) | b[at + 3];
        }
    }
}
