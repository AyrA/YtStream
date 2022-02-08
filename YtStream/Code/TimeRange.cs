using System;

namespace YtStream
{
    public class TimeRange
    {
        private double start;
        private double end;

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

        [System.Text.Json.Serialization.JsonIgnore]
        public double Duration { get => end - start; }

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

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsValid
        {
            get => start < end && start >= 0;
        }

        public TimeRange()
        {
            start = 0.0;
            end = double.Epsilon;
        }

        public TimeRange(double Start, double End)
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

        private static bool IsValidVideoTime(double d)
        {
            return !double.IsNaN(d) && !double.IsInfinity(d) && d >= 0;
        }

        public bool IsInRange(double d)
        {
            return d >= start && d <= end;
        }

        public override string ToString()
        {
            return $"{Start} --> {End}";
        }
    }
}
