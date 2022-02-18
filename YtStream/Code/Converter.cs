using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using YtStream.MP3;

namespace YtStream
{
    /// <summary>
    /// Converts to MP3
    /// </summary>
    public class Converter : IDisposable
    {
        /// <summary>
        /// Default bitrate for the MP3 target.
        /// Defaults to 192 kbps because that's approximately what youtube uses.
        /// Setting this higher has thus no benefits because the source material is already heavily compressed.
        /// You can set it lower though.
        /// 128 kbps is common for internet radio stations.
        /// If you use this instance for spoken material and not music, you can go down to 64 kbps.
        /// In that case you may also want to switch to mono. (ffmpeg argument: -ac 1)
        /// </summary>
        public const Bitrate DefaultRate = Bitrate.kbps192;
        /// <summary>
        /// Default frequency for the MP3 target.
        /// Defaults to 44.1 kHz ("CD quality")
        /// because the difference to 48 kHz is inaudible at 1x playback speed
        /// and 32 kHz is too low.
        /// </summary>
        public const Frequency DefaultFrequency = Frequency.Hz44100;

        /// <summary>
        /// Result for async conversion operation
        /// </summary>
        public class AsyncStreamResult
        {
            /// <summary>
            /// Task that completes when all data has been piped INTO the process
            /// </summary>
            public Task CopyStreamResult { get; }
            /// <summary>
            /// MP3 output stream
            /// </summary>
            public Stream StandardOutputStream { get; }

            /// <summary>
            /// Creates a new instance
            /// </summary>
            /// <param name="CopyStreamResult">Task</param>
            /// <param name="StandardOutputStream">Stream</param>
            public AsyncStreamResult(Task CopyStreamResult, Stream StandardOutputStream)
            {
                this.CopyStreamResult = CopyStreamResult;
                this.StandardOutputStream = StandardOutputStream;
            }
        }

        /// <summary>
        /// FFmpeg Executable path
        /// </summary>
        private readonly string executable;
        /// <summary>
        /// User agent string to use for URL requests
        /// </summary>
        private readonly string userAgent;
        /// <summary>
        /// Running instance
        /// </summary>
        private Process P;

        /// <summary>
        /// Get or set audio bitrate
        /// </summary>
        public Bitrate AudioRate { get; set; } = DefaultRate;

        /// <summary>
        /// Get or set audio frequency
        /// </summary>
        public Frequency AudioFrequency { get; set; } = DefaultFrequency;

        /// <summary>
        /// Gets if the converter is currently running
        /// </summary>
        public bool IsConverting { get => P != null && !P.HasExited; }

        /// <summary>
        /// Creates a converter instance
        /// </summary>
        /// <param name="Executable">FFmpeg executable path</param>
        /// <param name="UserAgent">
        /// User agent for URL based requests. See <see cref="YoutubeDl.GetUserAgent"/>.
        /// If null, the default by ffmpeg is used
        /// </param>
        public Converter(string Executable, string UserAgent = null)
        {
            if (string.IsNullOrWhiteSpace(Executable))
            {
                throw new ArgumentException($"'{nameof(Executable)}' cannot be null or whitespace.", nameof(Executable));
            }
            if (!File.Exists(Executable))
            {
                throw new IOException("File not found");
            }
            if (!string.IsNullOrEmpty(UserAgent))
            {
                UserAgent = UserAgent.Replace("\"", "");
            }
            executable = Executable;
            userAgent = UserAgent;
        }

        /// <summary>
        /// Aborts conversion
        /// </summary>
        /// <returns>true if aborted, false if failed to abort</returns>
        public bool Abort()
        {
            try
            {
                using (P)
                {
                    P.Kill();
                }
                P = null;
            }
            catch
            {
                //NOOP
                return false;
            }
            return true;
        }

        /// <summary>
        /// Blocks until the converter exits
        /// </summary>
        /// <param name="MaxWaitMs">Maximum wait time</param>
        /// <returns>true if exited. False if timeout hit</returns>
        /// <remarks>If the process has already exited returns true immediately</remarks>
        public bool WaitForExit(int MaxWaitMs = int.MaxValue)
        {
            if (IsConverting)
            {
                return P.WaitForExit(MaxWaitMs);
            }
            return true;
        }

        /// <summary>
        /// Returns a task that completes when the converter exits
        /// </summary>
        /// <param name="MaxWaitMs">Maximum wait time</param>
        /// <returns>true if exited. False if timeout hit</returns>
        /// <remarks>If the process has already exited returns true immediately</remarks>
        public Task<bool> WaitForExitAsync(int MaxWaitMs = int.MaxValue)
        {
            if (IsConverting)
            {
                return Task.Run(delegate { return P.WaitForExit(MaxWaitMs); });
            }
            return Task.FromResult(true);
        }

        /// <summary>
        /// Convert a file or URL to MP3
        /// </summary>
        /// <param name="SourceFileOrUrl">File path or URL</param>
        /// <returns>MP3 stream</returns>
        public Stream ConvertToMp3(string SourceFileOrUrl)
        {
            if (P != null)
            {
                throw new InvalidOperationException("Conversion already running");
            }
            var Args = GetArgs(SourceFileOrUrl);
            var PSI = new ProcessStartInfo(executable, Args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            P = Process.Start(PSI);
            P.Exited += ExitHandler;
            P.EnableRaisingEvents = true;
            return P.StandardOutput.BaseStream;
        }

        /// <summary>
        /// Convert a stream to MP3
        /// </summary>
        /// <param name="SourceData">Stream with media data</param>
        /// <returns>Data for async streaming</returns>
        public AsyncStreamResult ConvertToMp3(Stream SourceData)
        {
            if (P != null)
            {
                throw new InvalidOperationException("Conversion already running");
            }
            var Args = GetArgs(SourceData);
            var PSI = new ProcessStartInfo(executable, Args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardInput = true
            };
            P = Process.Start(PSI);
            P.Exited += ExitHandler;
            P.EnableRaisingEvents = true;
            return new AsyncStreamResult(
                SourceData.CopyToAsync(P.StandardInput.BaseStream),
                P.StandardOutput.BaseStream);
        }

        /// <summary>
        /// Disposes this instance and kills the converter if still running
        /// </summary>
        public void Dispose()
        {
            var Temp = P;
            if (Temp != null)
            {
                try
                {
                    Temp.Kill();
                }
                catch
                {
                    //NOOP
                }
                try
                {
                    Temp.Dispose();
                }
                catch
                {
                    //NOOP
                }
                P = null;
            }
        }

        /// <summary>
        /// Handles process exit (normal and abort)
        /// </summary>
        /// <param name="sender">Process</param>
        /// <param name="e">Generic event arguments</param>
        private void ExitHandler(object sender, EventArgs e)
        {
            var Temp = (Process)sender;
            try
            {
                Temp.Dispose();
            }
            catch
            {
                //NOOP
            }
            P = null;
        }

        /// <summary>
        /// Gets conversion arguments for a stream
        /// </summary>
        /// <param name="Arg">Stream</param>
        /// <returns>Conversion arguments for a stream</returns>
        /// <remarks><paramref name="Arg"/> is not actually touched in any way</remarks>
        private string GetArgs(Stream Arg)
        {
            return $"-i pipe:0 " + GetBaseArg();
        }

        /// <summary>
        /// Gets conversion arguments for a file or URL
        /// </summary>
        /// <param name="Arg">File path or URL</param>
        /// <returns>Conversion arguments for the given file or URL</returns>
        private string GetArgs(string Arg)
        {
            if (!string.IsNullOrWhiteSpace(userAgent))
            {
                return $"-user_agent \"{userAgent}\" -i \"{Arg}\" " + GetBaseArg();
            }
            return $"-i \"{Arg}\" " + GetBaseArg();
        }

        /// <summary>
        /// Gets conversion arguments that are identical for all types of source media
        /// </summary>
        /// <returns>Static conversion arguments</returns>
        /// <remarks>
        /// This incorporates <see cref="AudioRate"/> and <see cref="AudioFrequency"/>
        /// </remarks>
        private string GetBaseArg()
        {
            return $"-ab {(int)AudioRate}k -vn -ar {(int)AudioFrequency} -acodec mp3 -f mp3 -y pipe:1";
        }
    }
}
