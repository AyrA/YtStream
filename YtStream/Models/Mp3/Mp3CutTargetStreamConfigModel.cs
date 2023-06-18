using System;
using System.Collections.Generic;
using System.Linq;

namespace YtStream.Models.Mp3
{
    /// <summary>
    /// Output configuration for the MP3 cutting function
    /// </summary>
    public class Mp3CutTargetStreamConfigModel
    {
        /// <summary>
        /// Stream configuration
        /// </summary>
        private readonly List<Mp3CutTargetStreamInfoModel> streams;

        /// <summary>
        /// Gets the stream configuration
        /// </summary>
        public Mp3CutTargetStreamInfoModel[] Streams { get => streams.ToArray(); }
        /// <summary>
        /// Gets if <see cref="SetTimeout"/> has been called
        /// </summary>
        public bool HasTimeout { get; private set; }

        /// <summary>
        /// Initializes a new instance
        /// </summary>
        /// <param name="streams">Pre-fill for stream list</param>
        public Mp3CutTargetStreamConfigModel(IEnumerable<Mp3CutTargetStreamInfoModel>? streams = null)
        {
            this.streams = new List<Mp3CutTargetStreamInfoModel>();
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
        /// This calls <see cref="Mp3CutTargetStreamInfoModel.SetFaulted"/> on all streams with
        /// <see cref="Mp3CutTargetStreamInfoModel.SkipOnTimeout"/> set to true
        /// </remarks>
        public void SetTimeout(bool Throw = false)
        {
            var exs = new List<Exception>();
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
        public void AddStream(Mp3CutTargetStreamInfoModel stream)
        {
            streams.Add(stream);
        }
    }
}
