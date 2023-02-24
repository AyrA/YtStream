using System;

namespace YtStream.Models
{
    /// <summary>
    /// Represents an SBlock time range
    /// </summary>
    public class TimeRangeModel
    {
        /// <summary>
        /// Backing field for <see cref="Start"/>
        /// </summary>
        private double start;
        /// <summary>
        /// Backing field for <see cref="End"/>
        /// </summary>
        private double end;

        /// <summary>
        /// Gets or sets the range start time in seconds
        /// </summary>
        public double Start
        {
            get
            {
                return start;
            }
            set
            {
                if (!IsValidVideoTime(value))
                {
                    throw new ArgumentOutOfRangeException("Invalid start time");
                }
                start = value;
            }
        }

        /// <summary>
        /// Gets the duration
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public double Duration { get => Math.Max(0.0, end - start); }

        /// <summary>
        /// Gets or sets the range end time in seconds
        /// </summary>
        public double End
        {
            get
            {
                return end;
            }
            set
            {
                if (!IsValidVideoTime(value))
                {
                    throw new ArgumentOutOfRangeException("Invalid end time");
                }
                end = value;
            }
        }

        /// <summary>
        /// Gets if the <see cref="Start"/> and <see cref="End"/> combination is valid
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsValid
        {
            get => start < end && IsValidVideoTime(start) && IsValidVideoTime(end);
        }

        /// <summary>
        /// Creates a blank instance
        /// </summary>
        /// <remarks>This is for deserialization</remarks>
        public TimeRangeModel()
        {
            start = 0.0;
            end = double.Epsilon;
        }

        /// <summary>
        /// Creates an instance for the given times
        /// </summary>
        /// <param name="Start">Range start in seconds</param>
        /// <param name="End">Range end in seconds</param>
        public TimeRangeModel(double Start, double End)
        {
            if (!IsValidVideoTime(Start))
            {
                throw new ArgumentOutOfRangeException(nameof(Start), "Invalid start time");
            }
            if (!IsValidVideoTime(End) || End < Start)
            {
                throw new ArgumentOutOfRangeException(nameof(End), "Invalid end time");
            }
            start = Start;
            end = End;
        }

        /// <summary>
        /// Gets if the given time is theoretically valid timestamp
        /// </summary>
        /// <param name="d">Time</param>
        /// <returns>True if positive and finite</returns>
        private static bool IsValidVideoTime(double d)
        {
            return !double.IsNaN(d) && !double.IsInfinity(d) && d >= 0;
        }

        /// <summary>
        /// Gets if the given time is inside of this range
        /// </summary>
        /// <param name="d">Time in seconds</param>
        /// <returns>True if inside</returns>
        /// <remarks>This is inclusive start and end time</remarks>
        public bool IsInRange(double d)
        {
            return d >= start && d <= end;
        }

        /// <summary>
        /// Gets a user friendly printable version
        /// </summary>
        /// <returns><see cref="Start"/> --> <see cref="End"/></returns>
        public override string ToString()
        {
            return $"{Start} --> {End}";
        }
    }
}
