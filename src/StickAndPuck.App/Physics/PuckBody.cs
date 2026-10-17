using System.Numerics;

namespace StickAndPuck.App.Physics;

/// <summary>A body the puck can bounce off: a standing cylinder.</summary>
public readonly record struct PuckBody(int PlayerId, Vector3 Position, Vector3 Velocity);
