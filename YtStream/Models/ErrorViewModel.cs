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
        public static bool DefaultDetailOption { get; private set; }

        /// <summary>
        /// Gets if details should be shown (exception type and stack trace)
        /// </summary>
        public bool ShowDetails { get; set; }

        /// <summary>
        /// Exception that is displayed
        /// </summary>
        public Exception? Error { get; set; }

        /// <summary>
        /// Random id of the request
        /// </summary>
        public string? RequestId { get; set; }

        public int Status { get; set; }

        public bool IsClientError => Status >= 400 && Status < 500;

        /// <summary>
        /// Gest the description for the given status code
        /// </summary>
        public string StatusDescription
        {
            get
            {
                var code = (System.Net.HttpStatusCode)Status;
                if (Enum.IsDefined(code))
                {
                    return Tools.PascalCaseConverter(code.ToString());
                }
                return "Unknown error";
            }
        }

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
        public StackTraceInfoModel[] ParseStackTrace()
        {
            if (Error == null)
            {
                throw new InvalidOperationException($"The \"{nameof(Error)}\" property is null.");
            }
            if (Error.StackTrace == null)
            {
                return Array.Empty<StackTraceInfoModel>();
            }
            return Error.StackTrace
                .Trim()
                .Split('\n')
                .Select(m => new StackTraceInfoModel(m.Trim()))
                .ToArray();
        }
    }
}
