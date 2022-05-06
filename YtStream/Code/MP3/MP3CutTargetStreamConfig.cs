using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace YtStream.MP3
{
    /// <summary>
    /// Output configuration for the MP3 cutting function
    /// </summary>
    public class MP3CutTargetStreamConfig
    {
        /// <summary>
        /// Stream configuration
        /// </summary>
        private readonly List<MP3CutTargetStreamInfo> streams;

        /// <summary>
        /// Gets the stream configuration
        /// </summary>
        public MP3CutTargetStreamInfo[] Streams { get => streams.ToArray(); }
        /// <summary>
        /// Gets if <see cref="SetTimeout"/> has been called
        /// </summary>
        public bool HasTimeout { get; private set; }

        /// <summary>
        /// Initializes a new instance
        /// </summary>
        /// <param name="streams">Pre-fill for stream list</param>
        public MP3CutTargetStreamConfig(IEnumerable<MP3CutTargetStreamInfo> streams = null)
        {
            this.streams = new List<MP3CutTargetStreamInfo>();
            if (streams != null)
            {
                this.streams.AddRange(streams);
            }
            HasTimeout = false;
        }

        /// <summary>
        /// Gets if at least one non-faulted stream is available
        /// </summary>
        /// <returns>true if non-faulted stream available</returns>
        public bool HasWorkingStreams()
        {
            return streams.Any(m => !m.Faulted);
        }

        /// <summary>
        /// Gets if at least one stream is faulted
        /// </summary>
        /// <returns></returns>
        public bool HasFaultedStreams()
        {
            return streams.Any(m => m.Faulted);
        }

        /// <summary>
        /// Set this instance into timeout state
        /// </summary>
        /// <param name="Throw">Throw exception if a stream is not set to be allowed to fault</param>
        /// <remarks>
        /// This calls <see cref="MP3CutTargetStreamInfo.SetFaulted"/> on all streams with
        /// <see cref="MP3CutTargetStreamInfo.SkipOnTimeout"/> set to true
        /// </remarks>
        public void SetTimeout(bool Throw = false)
        {
            List<Exception> exs = new List<Exception>();
            foreach (var S in streams.Where(m => !m.Faulted && m.SkipOnTimeout))
            {
                try
                {
                    S.SetFaulted(Throw);
                }
                catch (Exception ex)
                {
                    exs.Add(ex);
                }
            }
            if (!HasTimeout)
            {
                HasTimeout = true;
            }
            if (Throw && exs.Count > 0)
            {
                throw new AggregateException("At least one stream threw an exception when set to faulted state", exs.ToArray());
            }
        }

        /// <summary>
        /// Add a new stream to the list
        /// </summary>
        /// <param name="stream">Stream info</param>
        public void AddStream(MP3CutTargetStreamInfo stream)
        {
            streams.Add(stream);
        }
    }

    /// <summary>
    /// Output stream info
    /// </summary>
    public class MP3CutTargetStreamInfo
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
        /// If this is true, <see cref="MP3Cut"/> will stream data in real time
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
        public MP3CutTargetStreamInfo(Stream stream, bool isUncut, bool canFault, bool skipOnTimeout, bool liveStream)
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
