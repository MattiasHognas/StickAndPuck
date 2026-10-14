using System.Globalization;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Replays;

/// <summary>SAP_CHECKSUMS=file writes "tick checksum" for every step, played or replayed, to compare two
/// runs line by line. Game opens it once per run; without the variable it writes nothing.</summary>
public sealed class ChecksumLog : IDisposable
{
    Maybe<StreamWriter> sums;

    ChecksumLog(Maybe<StreamWriter> sums)
    {
        this.sums = sums;
    }

    /// <summary>Opens the SAP_CHECKSUMS log, when it is set.</summary>
    public static ChecksumLog FromEnvironment()
    {
        if (Env.Get("SAP_CHECKSUMS") is not { Length: > 0 } f) return new ChecksumLog(Maybe<StreamWriter>.None);
        var log = new ChecksumLog(new StreamWriter(AssetRoot.Output(f)) { AutoFlush = false });
        Console.WriteLine($"SAP: replay: checksum of every step to {AssetRoot.Output(f)}");
        return log;
    }

    /// <summary>Writes one step's checksum, when the log is open.</summary>
    public void Write(long tick, ulong sum)
    {
        if (sums.TryGet(out var writer)) writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{tick} {WorldChecksum.Text(sum)}"));
    }

    /// <summary>Closes the log.</summary>
    public void Dispose()
    {
        if (sums.TryGet(out var writer)) writer.Dispose();
        sums = Maybe<StreamWriter>.None;
    }
}
