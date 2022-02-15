using System;
using System.Linq;

namespace YtStream.Models
{
    /// <summary>
    /// Model for the generic error page
    /// </summary>
    public class ErrorViewModel
    {
        public static bool DefaultDetailOption = false;

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

        public ErrorViewModel() : this(null) { }

        public ErrorViewModel(Exception ex)
        {
            ShowDetails = DefaultDetailOption;
            Error = ex;
        }

        public StackTraceInfo[] ParseStackTrace()
        {
            if (Error == null)
            {
                throw new InvalidOperationException($"The \"{nameof(Error)}\" property is null.");
            }
            return Error.StackTrace
                .Trim()
                .Split('\n')
                .Select(m => new StackTraceInfo(m.Trim()))
                .ToArray();
        }
    }
}
