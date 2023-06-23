using System;
using System.IO;

namespace YtStream.Models.Mp3
{
    /// <summary>
    /// Output stream info
    /// </summary>
    public class Mp3CutTargetStreamInfoModel
    {
        /// <summary>
        /// Writable stream
        /// </summary>
        public Stream Stream { get; }

        /// <summary>
        /// Gets if this stream receives the uncut MP3 data
        /// </summary>
        public bool IsUncut { get; }

        /// <summary>
        /// Gets if this stream is allowed to fault
        /// </summary>
        /// <remarks>If false, conversion aborts on fault</remarks>
        public bool CanFault { get; }

        /// <summary>
        /// Gets if this stream will be skipped once a timeout occurs
        /// </summary>
        public bool SkipOnTimeout { get; }

        /// <summary>
        /// Gets if this stream is used for live streaming
        /// </summary>
        /// <remarks>
        /// If this is true, <see cref="Services.Mp3.Mp3CutService.CutMp3"/> will stream data in real time
        /// instead of trying to get it out as fast as possible.
        /// </remarks>
        public bool LiveStream { get; }

        /// <summary>
        /// Gets if the stream is in a faulted state
        /// </summary>
        public bool Faulted { get; private set; }

        /// <summary>
        /// Initializes a new instance
        /// </summary>
        /// <param name="stream">Output stream</param>
        /// <param name="isUncut">true to receive all MP3 data, false to receive cut data only</param>
        /// <param name="canFault">true to permit faulting, false otherwise</param>
        /// <param name="skipOnTimeout">true to no longer write to this stream on a slow stream timeout</param>
        /// <param name="liveStream">Whether to cut in real time or as fast as possible</param>
        public Mp3CutTargetStreamInfoModel(Stream stream, bool isUncut, bool canFault, bool skipOnTimeout, bool liveStream)
        {
            Stream = stream ?? throw new ArgumentNullException(nameof(stream));
            if (!stream.CanWrite)
            {
                throw new ArgumentException(nameof(stream) + " not writable");
            }
            LiveStream = liveStream;
            IsUncut = isUncut;
            CanFault = canFault;
            SkipOnTimeout = skipOnTimeout;
            Faulted = false;
        }

        /// <summary>
        /// Puts the stream into a faulted state
        /// </summary>
        /// <param name="Throw">true to throw an exception if <see cref="CanFault"/> is set to false</param>
        public void SetFaulted(bool Throw = false)
        {
            if (!Faulted)
            {
                Faulted = true;
                if (!CanFault && Throw)
                {
                    throw new IOException("Stream set to faulted state but CanFault set to false");
                }
            }
        }
    }
}
