using System.Globalization;
using System.Text;

namespace StickAndPuck.App.Hud
{
    /// <summary>Swaps the letters a pixel font lacks for the plain letters it has, so names from any club file
    /// draw (W118): Á becomes A, Č becomes C, Ø becomes O, Æ becomes AE. A letter the font has stays, so
    /// glyphs added to the font later show without a code change; a sign with no plain form stays too
    /// and draws as the font's fallback.</summary>
    internal static class Letters
    {
        private static readonly Dictionary<char, string> NoMarks = new()
        {
            ['Ø'] = "O",
            ['Æ'] = "AE",
            ['Œ'] = "OE",
            ['ß'] = "SS",
            ['ẞ'] = "SS",
            ['Ł'] = "L",
            ['Đ'] = "D",
            ['Ð'] = "D",
            ['Þ'] = "TH",
            ['Ħ'] = "H",
        };

        /// <summary>Gets the text with each letter <paramref name="has"/> rejects swapped for its plain form.</summary>
        public static string Plain(string text, Func<char, bool> has)
        {
            StringBuilder sb = new(text.Length);
            foreach (char c in text)
            {
                if (has(c))
                {
                    _ = sb.Append(c);
                    continue;
                }

                if (NoMarks.TryGetValue(c, out string swap))
                {
                    _ = sb.Append(swap);
                    continue;
                }

                int before = sb.Length;
                foreach (char d in c.ToString().Normalize(NormalizationForm.FormD))
                {
                    if (
                        CharUnicodeInfo.GetUnicodeCategory(d) != UnicodeCategory.NonSpacingMark
                        && has(d)
                    )
                    {
                        _ = sb.Append(d);
                    }
                }

                if (sb.Length == before)
                {
                    _ = sb.Append(c); // nothing plain to show: the font's fallback, never a gap
                }
            }
            return sb.ToString();
        }
    }
}
