using System.Text.RegularExpressions;

namespace YtStream.Models
{
    /// <summary>
    /// Deserializes a stack trace message
    /// </summary>
    public class StackTraceInfoModel
    {
        /// <summary>
        /// Stack trace line format
        /// </summary>
        /// <remarks>This only matches a full trace with file name and line number</remarks>
        private const string Pattern = @"at\s+([^\(]+)\(([^\)]*)\)\s+in\s+(.+):line\s+(\d+)";

        /// <summary>
        /// Raw message
        /// </summary>
        public string? Raw { get; private set; }

        /// <summary>
        /// Name of the function
        /// </summary>
        public string? Function { get; private set; }

        /// <summary>
        /// Argument list
        /// </summary>
        public string? Arguments { get; private set; }

        /// <summary>
        /// Source file name
        /// </summary>
        public string? Filename { get; private set; }

        /// <summary>
        /// Source code line
        /// </summary>
        public int Line { get; private set; }

        /// <summary>
        /// Parses a stack trace line
        /// </summary>
        /// <param name="TraceLine">Stack trace line</param>
        public StackTraceInfoModel(string TraceLine)
        {
            var Result = Regex.Match(TraceLine, Pattern);
            if (Result.Success)
            {
                Function = Result.Groups[1].Value;
                Arguments = Result.Groups[2].Value;
                Filename = Result.Groups[3].Value;
                Line = int.TryParse(Result.Groups[4].Value, out int i) ? i : 0;
            }
            Raw = TraceLine;
        }
    }
}
