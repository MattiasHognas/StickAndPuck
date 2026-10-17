using StickAndPuck.App.Core;

namespace StickAndPuck.App.AI;

/// <summary>A pass on its way: who sent it, who it is meant for, and when.</summary>
/// <param name="Passer">The passer.</param>
/// <param name="Receiver">The teammate it is meant for, or -1 into space.</param>
/// <param name="Side">The passer's club.</param>
/// <param name="At">When it left the stick.</param>
internal sealed record InFlight(int Passer, int Receiver, Side Side, float At);
