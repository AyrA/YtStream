using System.IO;
using System.Threading.Tasks;

namespace YtStream.Models
{
    /// <summary>
    /// Result for async conversion operation
    /// </summary>
    public class AsyncStreamResultModel
    {
        /// <summary>
        /// Task that completes when all data has been piped INTO the process
        /// </summary>
        public Task CopyStreamResult { get; }
        public Stream StandardInputStream { get; }

        /// <summary>
        /// MP3 output stream
        /// </summary>
        public Stream StandardOutputStream { get; }

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="copyStreamResult">Task</param>
        /// <param name="standardInputStream">STDIN Stream</param>
        /// <param name="standardOutputStream">STDOUT Stream</param>
        public AsyncStreamResultModel(Task copyStreamResult, Stream standardInputStream, Stream standardOutputStream)
        {
            CopyStreamResult = copyStreamResult;
            StandardInputStream = standardInputStream;
            StandardOutputStream = standardOutputStream;
        }
    }
}
