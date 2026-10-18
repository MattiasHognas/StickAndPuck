namespace StickAndPuck.Tests.Support
{
    /// <summary>One or more checks of a <see cref="CheckedTest"/> failed.</summary>
    public sealed class CheckFailedException(string message) : Exception(message);
}
