using Raylib_cs;
using StickAndPuck.App.Core;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Core
{
    public sealed class KeyBindingsTests : CheckedTest
    {
        [Test]
        public void KnownKeyboardKeysHaveDisplayNames()
        {
            foreach (KeyboardKey key in Enum.GetValues<KeyboardKey>())
            {
                Check(KeyBindings.KeyName(key).Length > 0, $"{key} has a display name");
            }
        }

        [Test]
        public void UnknownKeyboardKeysAreRejected()
        {
            bool rejected = false;
            try
            {
                _ = KeyBindings.KeyName((KeyboardKey)int.MaxValue);
            }
            catch (NotSupportedException)
            {
                rejected = true;
            }
            Check(rejected, "an undefined keyboard key is rejected");
        }
    }
}
