using StickAndPuck.Tests.Support;
using TUnit.Core.Interfaces;

// as many tests at once as the machine has cores; a test never starts threads of its own
[assembly: ParallelLimiter<CoreCount>]

namespace StickAndPuck.Tests.Support
{
    /// <summary>Runs as many tests at once as the machine has cores, so the long matches each get one to themselves.</summary>
    public sealed class CoreCount : IParallelLimit
    {
        /// <inheritdoc/>
        public int Limit => Environment.ProcessorCount;
    }
}
