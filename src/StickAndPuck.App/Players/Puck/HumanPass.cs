using System.Numerics;

namespace StickAndPuck.App.Players;

/// <summary>A human's pass to a teammate: who threw it, who it is for, the stick held as it went, and when.</summary>
/// <param name="Passer">The passer.</param>
/// <param name="Receiver">The teammate it is meant for.</param>
/// <param name="Stick">The passer's stick as the pass left; control follows the pass with it still held.</param>
/// <param name="At">When it left the stick.</param>
public readonly record struct HumanPass(int Passer, int Receiver, Vector2 Stick, float At);
