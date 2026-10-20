using System.Globalization;
using System.Threading.Channels;

namespace StickAndPuck.App.Core
{
    /// <summary>A log file written off the game thread. Writers only put the text in a bounded queue and never
    /// wait: when the queue is full the line is dropped and counted. One background task drains the queue into
    /// the buffered file and flushes whenever the queue runs empty; <see cref="Dispose"/> writes what is left.</summary>
    public sealed class LogFile : IDisposable
    {
        /// <summary>How many writes the queue holds before it drops lines.</summary>
        public const int DefaultCapacity = 8192;

        private readonly Channel<Chunk> queue;
        private readonly TextWriter target;
        private readonly Task drain;
        private long dropped;
        private int disposed;

        /// <summary>Initializes a new instance of the <see cref="LogFile"/> class that writes to a writer it then owns.</summary>
        public LogFile(TextWriter target, int capacity = DefaultCapacity)
        {
            this.target = target;
            // Wait mode makes TryWrite fail on a full queue (DropWrite would report success), so drops are counted.
            queue = Channel.CreateBounded<Chunk>(
                new BoundedChannelOptions(capacity)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = true,
                }
            );
            drain = Task.Run(Drain);
        }

        /// <summary>Gets how many writes were dropped because the queue was full.</summary>
        public long Dropped => Interlocked.Read(ref dropped);

        /// <summary>Queues text.</summary>
        public void Write(string text)
        {
            Enqueue(new Chunk(text, Line: false));
        }

        /// <summary>Queues a line.</summary>
        public void WriteLine(string text)
        {
            Enqueue(new Chunk(text, Line: true));
        }

        /// <summary>Writes the queued lines, then closes the file. Waits at most two seconds.</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 1)
            {
                return;
            }

            _ = queue.Writer.TryComplete();
            if (drain.Wait(TimeSpan.FromSeconds(2)))
            {
                target.Dispose();
            }
        }

        private void Enqueue(Chunk chunk)
        {
            if (!queue.Writer.TryWrite(chunk))
            {
                _ = Interlocked.Increment(ref dropped);
            }
        }

        private async Task Drain()
        {
            ChannelReader<Chunk> reader = queue.Reader;
            long reported = 0;
            bool broken = false;
            while (await reader.WaitToReadAsync().ConfigureAwait(false))
            {
                try
                {
                    while (reader.TryRead(out Chunk chunk))
                    {
                        if (broken)
                        {
                            continue;
                        }

                        if (chunk.Line)
                        {
                            await target.WriteLineAsync(chunk.Text).ConfigureAwait(false);
                        }
                        else
                        {
                            await target.WriteAsync(chunk.Text).ConfigureAwait(false);
                        }
                    }

                    long lost = Dropped;
                    if (lost != reported && !broken)
                    {
                        await target
                            .WriteLineAsync(
                                string.Create(
                                    CultureInfo.InvariantCulture,
                                    $"SAP: log: {lost - reported} write(s) dropped, the log could not keep up"
                                )
                            )
                            .ConfigureAwait(false);
                        reported = lost;
                    }

                    if (!broken)
                    {
                        await target.FlushAsync().ConfigureAwait(false);
                    }
                }
                catch (Exception e) when (e is IOException or ObjectDisposedException)
                {
                    broken = true; // a full disk or a closed file: the rest is drained and dropped
                }
            }
        }

        private readonly record struct Chunk(string Text, bool Line);
    }
}
