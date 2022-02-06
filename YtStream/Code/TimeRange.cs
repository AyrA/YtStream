using System;

namespace YtStream
{
    public class TimeRange
    {
        public double Start { get; }

        public double Duration { get => End - Start; }

        public double End { get; }

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
            this.Start = Start;
            this.End = End;
        }

        private static bool IsValidVideoTime(double d)
        {
            return !double.IsNaN(d) && !double.IsInfinity(d) && d >= 0;
        }

        public override string ToString()
        {
            return $"{Start} --> {End}";
        }
    }
}
