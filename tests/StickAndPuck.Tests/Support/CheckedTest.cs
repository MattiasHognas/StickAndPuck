namespace StickAndPuck.Tests.Support;

/// <summary>
/// Base for the tests moved from the SAP_SCENE self-tests. A test makes many small checks in a
/// row, like the scenes did: each one prints PASS or FAIL with what it checked, a failing check does
/// not stop the test, and the test fails at the end listing every failed check.
/// </summary>
public abstract class CheckedTest
{
    readonly List<string> failed = [];

    /// <summary>Prints and counts one check.</summary>
    /// <param name="ok">Whether the check passed.</param>
    /// <param name="what">What was checked, with the values seen.</param>
    protected void Check(bool ok, string what)
    {
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {what}");
        if (!ok) failed.Add(what);
    }

    /// <summary>Fails the test when any check failed.</summary>
    [After(Test)]
    public void ThrowOnFailedChecks()
    {
        if (failed.Count > 0)
            throw new CheckFailedException($"{failed.Count} check(s) failed:{Environment.NewLine}{string.Join(Environment.NewLine, failed.Select(f => $"  FAIL  {f}"))}");
    }
}

/// <summary>One or more checks of a <see cref="CheckedTest"/> failed.</summary>
public sealed class CheckFailedException(string message) : Exception(message);
