using System.Globalization;
using System.Text;

namespace StickAndPuck.App.Core
{
    /// <summary>Every device on one step, as <see cref="DeviceInput"/> hands it to the game and the replay file
    /// stores it, one line per change.</summary>
    /// <param name="Keys">Held keys, sorted.</param>
    /// <param name="Pressed">Keys that went down since the last step, sorted.</param>
    /// <param name="Mouse">Held mouse buttons: bit 0 left, 1 right, 2 middle.</param>
    /// <param name="Pads">The four pad slots.</param>
    /// <param name="MenuPress">A scripted menu press, or empty.</param>
    public sealed record InputFrame(
        int[] Keys,
        int[] Pressed,
        byte Mouse,
        PadFrame[] Pads,
        string MenuPress
    )
    {
        /// <summary>Gets the frame with nothing connected or held.</summary>
        public static InputFrame Empty { get; } =
            new([], [], 0, [PadFrame.None, PadFrame.None, PadFrame.None, PadFrame.None], "");

        /// <summary>Gets this frame one step later with nothing new pressed: the same keys, buttons and sticks held.</summary>
        public InputFrame Held()
        {
            return Pressed.Length == 0 && MenuPress.Length == 0 && Pads.All(p => p.Pressed == 0)
                ? this
                : (
                    this with
                    {
                        Pressed = [],
                        MenuPress = "",
                        Pads = [.. Pads.Select(p => p with { Pressed = 0 })],
                    }
                );
        }

        /// <summary>Writes the frame as one line of text: "k:32,265 p:32 m:1 g0:down/pressed/x,y,... s:pause", empty parts left out.</summary>
        public string Write()
        {
            StringBuilder b = new();
            if (Keys.Length > 0)
            {
                _ = b.Append(" k:").AppendJoin(',', Keys);
            }

            if (Pressed.Length > 0)
            {
                _ = b.Append(" p:").AppendJoin(',', Pressed);
            }

            if (Mouse != 0)
            {
                _ = b.Append(" m:").Append(Mouse);
            }

            for (int i = 0; i < Pads.Length; i++)
            {
                PadFrame pad = Pads[i];
                if (!pad.Available)
                {
                    continue;
                }

                _ = b.Append(" g")
                    .Append(i)
                    .Append(':')
                    .Append(pad.Down.ToString("x", CultureInfo.InvariantCulture))
                    .Append('/')
                    .Append(pad.Pressed.ToString("x", CultureInfo.InvariantCulture))
                    .Append('/')
                    .AppendJoin(
                        ',',
                        pad.Axes.Select(a =>
                            ((int)(a * 4096f)).ToString(CultureInfo.InvariantCulture)
                        )
                    );
            }
            if (MenuPress.Length > 0)
            {
                _ = b.Append(" s:").Append(MenuPress);
            }

            return b.Length == 0 ? "-" : b.ToString(1, b.Length - 1);
        }

        /// <summary>Reads a frame <see cref="Write"/> wrote.</summary>
        public static InputFrame Read(string text)
        {
            int[] keys = [],
                pressed = [];
            byte mouse = 0;
            string menu = "";
            PadFrame[] pads = new PadFrame[DeviceInput.Pads];
            Array.Fill(pads, PadFrame.None);
            foreach (string part in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.Equals(part, "-", StringComparison.Ordinal))
                {
                    continue;
                }

                int colon = part.IndexOf(':', StringComparison.Ordinal);
                string tag = part[..colon],
                    value = part[(colon + 1)..];
                switch (tag)
                {
                    case "k":
                        keys = Ints(value);
                        break;
                    case "p":
                        pressed = Ints(value);
                        break;
                    case "m":
                        mouse = byte.Parse(value, CultureInfo.InvariantCulture);
                        break;
                    case "s":
                        menu = value;
                        break;
                    case ['g', ..]:
                        string[] bits = value.Split('/');
                        float[] axes = [.. Ints(bits[2]).Select(a => a / 4096f)];
                        pads[int.Parse(tag[1..], CultureInfo.InvariantCulture)] = new PadFrame(
                            Available: true,
                            uint.Parse(
                                bits[0],
                                NumberStyles.HexNumber,
                                CultureInfo.InvariantCulture
                            ),
                            uint.Parse(
                                bits[1],
                                NumberStyles.HexNumber,
                                CultureInfo.InvariantCulture
                            ),
                            axes
                        );
                        break;
                    default:
                        throw new FormatException($"replay input '{part}' is not a known part");
                }
            }
            return new InputFrame(keys, pressed, mouse, pads, menu);
        }

        private static int[] Ints(string text)
        {
            return
            [
                .. text.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => int.Parse(t, CultureInfo.InvariantCulture)),
            ];
        }
    }
}
