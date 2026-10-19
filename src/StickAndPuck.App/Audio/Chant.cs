using System.Runtime.InteropServices;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Audio
{
    /// <summary>One chant to start, and whose fans sing it.</summary>
    /// <param name="Cue">The chant effect, its level and delay.</param>
    /// <param name="Fans">The club whose fans sing it: the away chant comes from the visitors' corner.</param>
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct Chant(Cue Cue, Side Fans);
}
