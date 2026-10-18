using System.Runtime.InteropServices;

namespace StickAndPuck.App.Players.Checks
{
    /// <summary>How far a check reaches.</summary>
    /// <param name="Contact">Body centre to body centre at which it connects, in metres.</param>
    /// <param name="Cone">Half the angle around the checker's way where it can land, in radians.</param>
    [StructLayout(LayoutKind.Auto)]
    public readonly record struct CheckReach(float Contact, float Cone);
}
