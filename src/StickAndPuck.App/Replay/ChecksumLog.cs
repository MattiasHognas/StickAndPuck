using System.Globalization;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Replay
{
    /// <summary>SAP_CHECKSUMS=file writes "tick checksum" for every step, played or replayed, to compare two
    /// runs line by line. Game opens it once per run; without the variable it writes nothing.</summary>
    public sealed class ChecksumLog : IDisposable
    {
        private Maybe<StreamWriter> sums;

        private ChecksumLog(Maybe<StreamWriter> sums)
        {
            this.sums = sums;
        }

        /// <summary>Opens the SAP_CHECKSUMS log, when it is set.</summary>
        public static ChecksumLog FromEnvironment()
        {
            if (Env.Get("SAP_CHECKSUMS") is not { Length: > 0 } f)
            {
                return new ChecksumLog(Maybe.None<StreamWriter>());
            }

            ChecksumLog log = new(new StreamWriter(AssetRoot.Output(f)) { AutoFlush = false });
            Console.WriteLine($"SAP: replay: checksum of every step to {AssetRoot.Output(f)}");
            return log;
        }

        /// <summary>Writes one step's checksum, when the log is open.</summary>
        public void Write(long tick, ulong sum)
        {
            if (sums.TryGet(out StreamWriter writer))
            {
                writer.WriteLine(
                    string.Create(CultureInfo.InvariantCulture, $"{tick} {WorldChecksum.Text(sum)}")
                );
            }
        }

        /// <summary>Closes the log.</summary>
        public void Dispose()
        {
            if (sums.TryGet(out StreamWriter writer))
            {
                writer.Dispose();
            }

            sums = Maybe.None<StreamWriter>();
        }
    }
}
