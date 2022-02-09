namespace YtStream.Models
{
    /// <summary>
    /// Model for the generic error page
    /// </summary>
    public class ErrorViewModel
    {
        /// <summary>
        /// Random id of the request
        /// </summary>
        public string RequestId { get; set; }

        /// <summary>
        /// True if RequestId passes <see cref="string.IsNullOrEmpty"/>
        /// </summary>
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
