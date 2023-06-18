using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace YtStream.Code
{
    /// <summary>
    /// Stream that buffers data in memory
    /// </summary>
    /// <remarks>
    /// This is usable when data is produced faster than it's consumed,
    /// but the producing end cannot be slowed down.
    /// Be aware that this implementation lacks a memory limit
    /// </remarks>
    public class BufferedStream : Stream
    {
        private readonly long cutoffInterval;
        private bool disposed = false;
        private long readPtr = 0;
        private long writePtr = 0;
        private bool hasWriteEnded = false;
        private MemoryStream MS = new();

        private readonly SemaphoreSlim semaphore = new(1);

        private bool IsStarved => !hasWriteEnded && readPtr == writePtr;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => writePtr;

        public override long Position
        {
            get => readPtr;
            set => throw new NotSupportedException();
        }

        /// <summary>
        /// Creates a new buffered stream instance
        /// </summary>
        /// <param name="CutoffInterval">Number of bytes to be read after which the stream is truncated</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="CutoffInterval"/> is zero or less</exception>
        public BufferedStream(long CutoffInterval)
        {
            if (CutoffInterval < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(CutoffInterval), "Cutoff must be at least 1");
            }
            cutoffInterval = CutoffInterval;
        }

        /// <summary>
        /// Communicates to the backend that write operations have completed.
        /// Pending and future read operations at the end of the stream will read 0 bytes instead of stalling for more data
        /// </summary>
        /// <remarks>
        /// This must be called after the last data has been written to the stream
        /// </remarks>
        public void EndWrite()
        {
            CheckDispose();
            hasWriteEnded = true;
        }

        public override void Flush()
        {
            CheckDispose();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            CheckDispose();
            while (IsStarved)
            {
                Thread.Sleep(100);
                CheckDispose();
            }
            semaphore.Wait();
            try
            {
                MS.Position = readPtr;
                int read = MS.Read(buffer, offset, count);
                readPtr += read;
                //If we've read beyond an MB, cut it off from the stream
                //This is done by copying the remainder to a new stream and replacing the original
                if (readPtr > cutoffInterval)
                {
                    var stream = MS;
                    using (stream)
                    {
                        MS = new MemoryStream();
                        stream.CopyTo(MS);
                    }
                    readPtr = 0;
                }
                else
                {
                    //Return back to the end of the stream for further writing
                    MS.Position = MS.Length;
                }
                return read;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            CheckDispose();
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            CheckDispose();
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            CheckDispose();
            if (hasWriteEnded)
            {
                throw new InvalidOperationException("Write operations have previously been marked as complete by EndWrite()");
            }
            semaphore.Wait(1);
            try
            {
                MS.Write(buffer, offset, count);
                writePtr += count;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            CheckDispose();
            while (IsStarved)
            {
                await Task.Delay(100, cancellationToken);
            }
            return await ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            CheckDispose();
            while (IsStarved)
            {
                await Task.Delay(100, cancellationToken);
            }
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                MS.Position = readPtr;
                int read = await MS.ReadAsync(buffer, cancellationToken);
                readPtr += read;
                //If we've read beyond an MB, cut it off from the stream
                //This is done by copying the remainder to a new stream and replacing the original
                if (readPtr > cutoffInterval)
                {
                    var stream = MS;
                    using (stream)
                    {
                        MS = new MemoryStream();
                        await stream.CopyToAsync(MS, cancellationToken);
                    }
                    readPtr = 0;
                }
                else
                {
                    //Return back to the end of the stream for further writing
                    MS.Position = MS.Length;
                }
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new Exception("The operation was cancelled");
                }
                return read;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await WriteAsync(buffer.AsMemory(offset, count), cancellationToken);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            CheckDispose();
            await semaphore.WaitAsync(1, cancellationToken);
            try
            {
                await MS.WriteAsync(buffer, cancellationToken);
                writePtr += buffer.Length;
            }
            finally
            {
                semaphore.Release();
            }
        }

        protected override void Dispose(bool disposing)
        {
            disposed = true;
            semaphore.Dispose();
            MS?.Dispose();
            MS = null;
            base.Dispose(disposing);
        }

        private void CheckDispose()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(BufferedStream));
            }
        }
    }
}
