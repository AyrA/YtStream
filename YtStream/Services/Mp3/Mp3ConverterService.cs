using AyrA.AutoDI;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YtStream.Enums;
using YtStream.Models;

namespace YtStream.Services.Mp3
{
    [AutoDIRegister(AutoDIType.Transient)]
    /// <summary>
    /// Converts to MP3
    /// </summary>
    public class Mp3ConverterService : IDisposable
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
        public const Mp3BitrateEnum DefaultRate = Mp3BitrateEnum.kbps192;
        /// <summary>
        /// Default frequency for the MP3 target.
        /// Defaults to 44.1 kHz ("CD quality")
        /// because the difference to 48 kHz is inaudible at 1x playback speed
        /// and 32 kHz is too low.
        /// </summary>
        public const Mp3FrequencyEnum DefaultFrequency = Mp3FrequencyEnum.Hz44100;

        /// <summary>
        /// FFmpeg Executable path
        /// </summary>
        private readonly string executable;
        /// <summary>
        /// User agent string to use for URL requests
        /// </summary>
        public string? UserAgent { get; private set; }
        /// <summary>
        /// Running instance
        /// </summary>
        private Process? P;

        /// <summary>
        /// Get or set audio bitrate
        /// </summary>
        public Mp3BitrateEnum AudioRate { get; }

        /// <summary>
        /// Get or set audio frequency
        /// </summary>
        public Mp3FrequencyEnum AudioFrequency { get; }

        /// <summary>
        /// Gets if the converter is currently running
        /// </summary>
        public bool IsConverting { get => P != null && !P.HasExited; }

        public int LastExitCode { get; private set; } = -1;

        public Mp3ConverterService(ConfigService config)
        {
            var c = config.GetConfiguration();
            if (string.IsNullOrWhiteSpace(c.FfmpegPath))
            {
                throw new ArgumentException($"'{nameof(c.FfmpegPath)}' property in the configuration cannot be null or whitespace.", nameof(config));
            }
            if (!File.Exists(c.FfmpegPath))
            {
                throw new IOException("File not found");
            }
            executable = c.FfmpegPath;
            AudioRate = c.AudioBitrate;
            AudioFrequency = c.AudioFrequency;
        }

        public void SetUserAgent(string ffmpegUserAgent)
        {
            if (string.IsNullOrWhiteSpace(ffmpegUserAgent))
            {
                throw new ArgumentException($"'{nameof(ffmpegUserAgent)}' cannot be null or whitespace.", nameof(ffmpegUserAgent));
            }
            UserAgent = ffmpegUserAgent.Replace("\"", "");
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
                    P?.Kill();
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
                return P?.WaitForExit(MaxWaitMs) ?? true;
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
            var proc = P;
            if (IsConverting && proc != null)
            {
                return Task.Run(delegate
                {
                    try
                    {
                        return proc.WaitForExit(MaxWaitMs);
                    }
                    catch
                    {
                        return true;
                    }
                });
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
            P = Process.Start(PSI) ?? throw new Exception("Failed to start process");
            P.Exited += ExitHandler;
            P.EnableRaisingEvents = true;
            return P.StandardOutput.BaseStream;
        }

        /// <summary>
        /// Convert a stream to MP3
        /// </summary>
        /// <param name="SourceData">Stream with media data</param>
        /// <returns>Data for async streaming</returns>
        public AsyncStreamResultModel ConvertToMp3(Stream SourceData)
        {
            if (P != null)
            {
                throw new InvalidOperationException("Conversion already running");
            }
            var Args = GetArgs();
            var PSI = new ProcessStartInfo(executable, Args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardInput = true
            };
            P = Process.Start(PSI) ?? throw new Exception("Failed to start process");
            P.Exited += ExitHandler;
            P.EnableRaisingEvents = true;
            return new AsyncStreamResultModel(
                SourceData.CopyToAsync(P.StandardInput.BaseStream),
                P.StandardInput.BaseStream,
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
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Handles process exit (normal and abort)
        /// </summary>
        /// <param name="sender">Process</param>
        /// <param name="e">Generic event arguments</param>
        private void ExitHandler(object? sender, EventArgs e)
        {
            if (sender == null)
            {
                return;
            }
            var Temp = (Process)sender;
            LastExitCode = Temp.ExitCode;
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
        private string GetArgs()
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
            if (!string.IsNullOrWhiteSpace(UserAgent))
            {
                return $"-user_agent \"{UserAgent}\" -i \"{Arg}\" " + GetBaseArg();
            }
            return $"-reconnect 1 -reconnect_on_network_error 1 -reconnect_streamed 1 -reconnect_delay_max 5 -i \"{Arg}\" " + GetBaseArg();
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

        /// <summary>
        /// Gets the version of ffmpeg
        /// </summary>
        /// <returns>version string</returns>
        /// <remarks>This will abort after 5 seconds if the application won't terminate</remarks>
        public async Task<string> GetVersion()
        {
            return await GetVersion(executable);
        }

        /// <summary>
        /// Gets the version of ffmpeg
        /// </summary>
        /// <param name="executable">ffmpeg executable</param>
        /// <returns>version string</returns>
        /// <remarks>This will abort after 5 seconds if the application won't terminate</remarks>
        public static async Task<string> GetVersion(string executable)
        {
            var PSI = new ProcessStartInfo(executable, "-version")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            using (var P = Process.Start(PSI) ?? throw new Exception("Failed to start process"))
            {
                var Reader = P.StandardOutput.ReadToEndAsync();
                var Sleeper = Task.Delay(5000);
                if (await Task.WhenAny(Reader, Sleeper) is Task<string> Result)
                {
                    var Output = Result.Result.Trim();
                    var M = Regex.Match(Output, @"ffmpeg version (\S+)", RegexOptions.IgnoreCase);
                    if (M.Success)
                    {
                        return M.Groups[1].Value;
                    }
                    throw new Exception($"Did not get version output on {P.StartInfo.FileName} {P.StartInfo.Arguments}");
                }
                else
                {
                    try
                    {
                        P.Kill();
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Application did not exit but killing also did not work", ex);
                    }
                }
            }
            throw new Exception("Application did not exit in time and was forcibly terminated");
        }
    }
}
