using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace YtStream
{
    public delegate void StreamGoneHandler(object sender, Stream stream);

    /// <summary>
    /// A stream that writes data written to it to two underlying streams
    /// </summary>
    public class TeeStream : Stream
    {
        private class StreamInfo : IDisposable, IAsyncDisposable
        {
            public bool Ready { get; set; }
            public Stream Stream { get; }

            public StreamInfo(Stream S)
            {
                Ready = true;
                Stream = S;
            }

            public ValueTask DisposeAsync()
            {
                return Stream.DisposeAsync();
            }

            public void Dispose()
            {
                Stream.Dispose();
            }
        }

        private readonly List<StreamInfo> Streams;
        private readonly bool ownsStreams;
        private long bytesWritten = 0;

        public event StreamGoneHandler StreamGone = delegate { };

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => bytesWritten;

        public override long Position { get => bytesWritten; set => throw new InvalidOperationException(); }

        /// <summary>
        /// Initializes the TeeStream with the given streams
        /// </summary>
        /// <param name="OwnsStreams">true to disposr of the streams when this instance is disposed of</param>
        /// <param name="Streams">Streams to write to</param>
        /// <remarks>
        /// This instance is safe to dispose even when <paramref name="OwnsStreams"/> is set to true
        /// </remarks>
        public TeeStream(bool OwnsStreams, params Stream[] Streams)
        {
            if (Streams == null || Streams.Length == 0)
            {
                throw new ArgumentException("At least one stream must be supplied");
            }
            if (Streams.Any(m => m == null || !m.CanWrite))
            {
                throw new ArgumentException("Null or unwritable stream passed to " + nameof(TeeStream));
            }
            this.Streams = new List<StreamInfo>(Streams.Select(m => new StreamInfo(m)));
            ownsStreams = OwnsStreams;
        }

        /// <summary>
        /// Gets all streams assigned to this instance
        /// </summary>
        /// <returns>Streams</returns>
        public Stream[] GetStreams()
        {
            return Streams.Select(m => m.Stream).ToArray();
        }

        /// <summary>
        /// Gets all streams with the given ready state
        /// </summary>
        /// <param name="ReadyStatus">Ready state</param>
        /// <returns>Streams</returns>
        /// <remarks>
        /// Ready state for a stream is true unless a write failes,
        /// then it becomes false and no further write attempts are performed
        /// </remarks>
        public Stream[] GetReadyStreams(bool ReadyStatus)
        {
            return Streams.Where(m => m.Ready == ReadyStatus).Select(m => m.Stream).ToArray();
        }

        /// <summary>
        /// Resets the ready status of a stream
        /// </summary>
        /// <param name="S">Stream</param>
        /// <remarks>
        /// This will almost never accomplish anything
        /// unless the stream can deal with recovering after an error
        /// </remarks>
        public void ResetStream(Stream S)
        {
            if (S == null)
            {
                throw new ArgumentNullException(nameof(S));
            }

            var SI = Streams.FirstOrDefault(m => m.Stream == S);
            if (SI == null)
            {
                throw new ArgumentException($"Supplied stream not part of this {nameof(TeeStream)} instance");
            }
            SI.Ready = true;
        }

        public override void Flush()
        {
            foreach (var S in Streams)
            {
                S.Stream.Flush();
            }
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return Task.WhenAll(Streams.Where(m => m.Ready).Select(m => m.Stream.FlushAsync(cancellationToken)).ToArray());
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException("Cannot read from a write-only stream");
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new InvalidOperationException("Cannot seek a write-only stream");
        }

        public override void SetLength(long value)
        {
            throw new InvalidOperationException("Cannot set length of a write-only stream");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            foreach (var S in Streams)
            {
                if (S.Ready)
                {
                    try
                    {
                        S.Stream.Write(buffer, offset, count);
                    }
                    catch
                    {
                        S.Ready = false;
                        Tools.Thread(delegate () { StreamGone(this, S.Stream); });
                    }
                }
            }
            bytesWritten += count;
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var Tasks = Streams.Where(m => m.Ready).Select(m => WriteToStream(m, buffer, offset, count, cancellationToken));
            await Task.WhenAll(Tasks);
            bytesWritten += count;
        }

        protected override void Dispose(bool disposing)
        {
            if (ownsStreams)
            {
                foreach (var S in Streams)
                {
                    S.Dispose();
                }
            }
            Streams.Clear();
        }

        /// <summary>
        /// Writes to a stream and never fails.
        /// Raises <see cref="StreamGone"/> on error and sets Ready property of <paramref name="S"/> to false.
        /// </summary>
        /// <param name="S">Stream container</param>
        /// <param name="buffer">Data</param>
        /// <param name="offset">Data offset</param>
        /// <param name="count">Data length</param>
        /// <param name="cancellationToken">cancellation token</param>
        /// <returns>true, if not failed</returns>
        private async Task<bool> WriteToStream(StreamInfo S, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (S.Ready)
            {
                try
                {
                    await S.Stream.WriteAsync(buffer, offset, count, cancellationToken);
                }
                catch
                {
                    S.Ready = false;
                    Tools.Thread(delegate () { StreamGone(this, S.Stream); });
                }
            }
            return S.Ready;
        }
    }
}
