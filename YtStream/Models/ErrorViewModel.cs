using System;
using System.Linq;

namespace YtStream.Models
{
    /// <summary>
    /// Model for the generic error page
    /// </summary>
    public class ErrorViewModel
    {
        /// <summary>
        /// If set to true, details are shown by default
        /// </summary>
        /// <remarks>In <see cref="Startup"/> this is set to true for the development environment</remarks>
        public static bool DefaultDetailOption = false;

        /// <summary>
        /// Gets if details should be shown (exception type and stack trace)
        /// </summary>
        public bool ShowDetails { get; set; }

        /// <summary>
        /// Exception that is displayed
        /// </summary>
        public Exception Error { get; set; }

        /// <summary>
        /// Random id of the request
        /// </summary>
        public string RequestId { get; set; }

        /// <summary>
        /// True if RequestId passes <see cref="string.IsNullOrEmpty"/>
        /// </summary>
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

        /// <summary>
        /// Creates a default instance
        /// </summary>
        public ErrorViewModel()
        {
            ShowDetails = DefaultDetailOption;
            Error = new Exception("Unspecified error");
        }

        /// <summary>
        /// Creates an instance for the given exception
        /// </summary>
        /// <param name="ex">Exception</param>
        public ErrorViewModel(Exception ex)
        {
            ShowDetails = DefaultDetailOption;
            Error = ex;
        }

        /// <summary>
        /// Deserializes stack trace lines
        /// </summary>
        /// <returns>Stack trace</returns>
        public StackTraceInfo[] ParseStackTrace()
        {
            if (Error == null)
            {
                throw new InvalidOperationException($"The \"{nameof(Error)}\" property is null.");
            }
            if (Error.StackTrace == null)
            {
                return new StackTraceInfo[0];
            }
            return Error.StackTrace
                .Trim()
                .Split('\n')
                .Select(m => new StackTraceInfo(m.Trim()))
                .ToArray();
        }
    }
}
