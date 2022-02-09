using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace YtStream
{
    /// <summary>
    /// Provides a youtube-dl interface
    /// </summary>
    public class YoutubeDl
    {
        /// <summary>
        /// Youtube-dl executable
        /// </summary>
        private readonly string executable;

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="Executable">ytdl executable path</param>
        public YoutubeDl(string Executable)
        {
            if (string.IsNullOrWhiteSpace(Executable))
            {
                throw new ArgumentException($"'{nameof(Executable)}' cannot be null or whitespace.", nameof(Executable));
            }

            if (!File.Exists(Executable))
            {
                throw new IOException("File not found");
            }
            executable = Executable;
        }

        /// <summary>
        /// Gets information about the supplied id
        /// </summary>
        /// <param name="Id">Video id</param>
        /// <returns>Video information</returns>
        /// <remarks>
        /// The information is biased towards the best audio URL
        /// </remarks>
        public async Task<YoutubeDlResult> GetAudioDetails(string Id)
        {
            if (!Tools.IsYoutubeId(Id))
            {
                throw new FormatException("Argument must be a youtube video id");
            }
            var PSI = new ProcessStartInfo(executable, $"--skip-download --dump-json --format bestaudio {Tools.IdToUrl(Id)}");
            PSI.UseShellExecute = false;
            PSI.RedirectStandardOutput = true;
            using (var P = Process.Start(PSI))
            {
                return (await P.StandardOutput.ReadToEndAsync()).FromJson<YoutubeDlResult>();
            }
        }

        /// <summary>
        /// Gets the audio URL for a video
        /// </summary>
        /// <param name="Id">Video id</param>
        /// <returns>Audio URL</returns>
        /// <remarks>Internally calls <see cref="GetAudioDetails(string)"/>.Url</remarks>
        public async Task<string> GetAudioUrl(string Id)
        {
            return (await GetAudioDetails(Id)).Url;
        }
    }
}
