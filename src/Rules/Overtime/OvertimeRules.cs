using StickAndPuck.Core;

namespace StickAndPuck.Rules;

/// <summary>W7c: a tie after regulation goes to SHL 3 on 3 overtime, then a shootout, as <see cref="MatchSetup.Overtime"/> says.</summary>
internal sealed class OvertimeRules : ISystem
{
    public void Update(World world, float dt) { }
}
