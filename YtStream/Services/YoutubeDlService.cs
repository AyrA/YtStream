using AyrA.AutoDI;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YtStream.Extensions;
using YtStream.Models.YtDl;
using YtStream.YtDl;

namespace YtStream.Services
{
    /// <summary>
    /// Provides a youtube-dl interface
    /// </summary>
    [AutoDIRegister(AutoDIType.Transient)]
    public class YoutubeDlService
    {
        /// <summary>
        /// Upper limit for playlist item count
        /// </summary>
        public const int MaxPlaylistEntries = 1000;
        /// <summary>
        /// How many minutes playlist items remain in the cache
        /// </summary>
        private const double MaxCacheAgeMinutes = 60.0 * 24.0;
        /// <summary>
        /// Holds results from playlist queries for a limited time
        /// </summary>
        private static readonly Dictionary<string, PlaylistInfo> plCache = new();
        /// <summary>
        /// Holds results from video queries for a limited time
        /// </summary>
        private static readonly Dictionary<string, AudioInfo> idCache = new();
        /// <summary>
        /// Counts time since last cache cleanup
        /// </summary>
        private static readonly Stopwatch lastClean = Stopwatch.StartNew();

        /// <summary>
        /// Youtube-dl executable
        /// </summary>
        private readonly string executable;
        /// <summary>
        /// Cached user agent
        /// </summary>
        private string? userAgent = null;
        /// <summary>
        /// Cached version
        /// </summary>
        private string? version = null;
        /// <summary>
        /// Logging interface
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="config">System configuration instance</param>
        /// <param name="logger">Logger instance</param>
        public YoutubeDlService(ConfigService config, ILogger<YoutubeDlService> logger)
        {
            var c = config.GetConfiguration();
            if (string.IsNullOrWhiteSpace(c.YoutubedlPath))
            {
                logger.LogCritical("Youtube-Dl has not been configured yet");
                throw new ArgumentException($"'{nameof(c.YoutubedlPath)}' property in the configuration cannot be null or whitespace.", nameof(config));
            }

            if (!File.Exists(c.YoutubedlPath))
            {
                logger.LogCritical("Youtube-Dl cannot be found at {path}", c.YoutubedlPath);
                throw new IOException("File not found");
            }
            executable = c.YoutubedlPath;
            _logger = logger;
        }

        /// <summary>
        /// Gets the user agent that ytdl uses for requests
        /// </summary>
        /// <returns>User agent string</returns>
        public async Task<string> GetUserAgent()
        {
            if (!string.IsNullOrEmpty(userAgent))
            {
                return userAgent;
            }
            var PSI = new ProcessStartInfo(executable, "--dump-user-agent")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            using var P = Process.Start(PSI) ?? throw new Exception("Failed to start process");
            return userAgent = (await P.StandardOutput.ReadToEndAsync()).Trim();
        }

        /// <summary>
        /// Gets the version of the application
        /// </summary>
        /// <returns>Version</returns>
        /// <remarks>Version number is currently formatted as YYYY.MM.DD</remarks>
        public async Task<string> GetVersion()
        {
            if (!string.IsNullOrEmpty(version))
            {
                return version;
            }
            return version = await GetVersion(executable);
        }

        /// <summary>
        /// Downloads a youtube playlist and returns all video ids within
        /// </summary>
        /// <param name="playlist">Playlist id</param>
        /// <param name="maxItems">Maximum number of items to extract</param>
        /// <returns>Video Ids</returns>
        public async Task<string[]> GetPlaylist(string playlist, int maxItems)
        {
            if (!Tools.IsYoutubePlaylist(playlist))
            {
                throw new FormatException("Argument must be a youtube playlist id");
            }
            if (maxItems < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxItems), "Must be 0 or bigger");
            }
            CleanCache();
            lock (plCache)
            {
                if (plCache.TryGetValue(playlist, out PlaylistInfo? info))
                {
                    if (info.Age.TotalMinutes < MaxCacheAgeMinutes)
                    {
                        return maxItems == 0 ? info.Items : info.Items.Take(maxItems).ToArray();
                    }
                }
            }
            var itemCount = maxItems > 0 ? $"--playlist-items 1-{Math.Min(MaxPlaylistEntries, maxItems)}" : "";
            var args = $"--get-id {itemCount} --flat-playlist https://www.youtube.com/playlist?list={playlist}";
            var PSI = new ProcessStartInfo(executable, args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var P = Process.Start(PSI) ?? throw new Exception("Failed to start process");

            //Read STDOUT and STDERR
            var lineTask = P.StandardOutput.ReadToEndAsync();
            var errorTask = P.StandardError.ReadToEndAsync();
            await Task.WhenAll(lineTask, errorTask);
            var lines = lineTask.Result.Trim();
            var error = errorTask.Result.Trim();

            //Only consider errors if there was no regular output.
            //Otherwise they're likely just warnings.
            if (error.Length > 0 && lines.Length == 0)
            {
                throw new YoutubeDlException(error);
            }
            var items = lines.Split('\n').Where(m => Tools.IsYoutubeId(m.Trim())).ToArray();
            plCache[playlist] = new PlaylistInfo(items);
            return items;
        }

        /// <summary>
        /// Gets information about the supplied id
        /// </summary>
        /// <param name="id">Video id</param>
        /// <returns>Video information</returns>
        /// <remarks>
        /// The information is biased towards the best audio URL
        /// </remarks>
        public async Task<YoutubeDlResultModel> GetAudioDetails(string id)
        {
            if (!Tools.IsYoutubeId(id))
            {
                throw new FormatException("Argument must be a youtube video id");
            }
            CleanCache();
            lock (idCache)
            {
                if (idCache.TryGetValue(id, out AudioInfo? info) && !info.Expired)
                {
                    return info.Info.JsonClone();
                }
                //Id not found or expired
            }
            var PSI = new ProcessStartInfo(executable, $"--skip-download --dump-json --format bestaudio {Tools.IdToUrl(id)}")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var P = Process.Start(PSI) ?? throw new Exception("Failed to start process");

            //Read STDOUT and STDERR
            var lineTask = P.StandardOutput.ReadToEndAsync();
            var errorTask = P.StandardError.ReadToEndAsync();
            await Task.WhenAll(lineTask, errorTask);
            var lines = lineTask.Result.Trim();
            var error = errorTask.Result.Trim();

            var result = lines.FromJson<YoutubeDlResultModel>();
            if (result != null)
            {
                idCache[id] = new AudioInfo(result);
                return result;
            }
            throw new YoutubeDlException(error);
        }

        /// <summary>
        /// Gets the audio URL for a video
        /// </summary>
        /// <param name="id">Video id</param>
        /// <returns>Audio URL</returns>
        /// <remarks>Internally calls <see cref="GetAudioDetails(string)"/>.Url</remarks>
        public async Task<string?> GetAudioUrl(string id)
        {
            try
            {
                return (await GetAudioDetails(id)).Url;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to obtain YT stream url. {msg}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Gets the version of the application
        /// </summary>
        /// <param name="executable">YtDl executable</param>
        /// <returns>Version</returns>
        /// <remarks>Version number is currently formatted as YYYY.MM.DD</remarks>
        public static async Task<string> GetVersion(string executable)
        {
            var PSI = new ProcessStartInfo(executable, "--version")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            using var P = Process.Start(PSI) ?? throw new Exception("Failed to start process");
            return (await P.StandardOutput.ReadToEndAsync()).Trim();
        }

        /// <summary>
        /// Gets the user agent used by YtDl
        /// </summary>
        /// <param name="executable">YtDl executable</param>
        /// <returns>User agent string</returns>
        /// <remarks>Use instance member <see cref="GetUserAgent()"/> instead if possible</remarks>
        public static async Task<string> GetUserAgent(string executable)
        {
            var PSI = new ProcessStartInfo(executable, "--dump-user-agent")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            using var P = Process.Start(PSI) ?? throw new Exception("Failed to start process");
            return (await P.StandardOutput.ReadToEndAsync()).Trim();
        }

        /// <summary>
        /// Removes expired items from the cache
        /// </summary>
        /// <remarks>
        /// Should be called fairly often. It will clean at most once a minute
        /// </remarks>
        private static void CleanCache()
        {
            if (lastClean.Elapsed.TotalMinutes >= 1.0)
            {
                lastClean.Reset();
                lock (plCache)
                {
                    var items = plCache
                        .Where(m => m.Value.Age.TotalMinutes > MaxCacheAgeMinutes)
                        .Select(m => m.Key)
                        .ToList();
                    foreach (var item in items)
                    {
                        plCache.Remove(item);
                    }
                }
                lock (idCache)
                {
                    var items = idCache
                        .Where(m => m.Value.Expired)
                        .Select(m => m.Key)
                        .ToList();
                    foreach (var item in items)
                    {
                        idCache.Remove(item);
                    }
                }
            }
        }

        /// <summary>
        /// Represents an item in the playlist cache
        /// </summary>
        private class PlaylistInfo
        {
            /// <summary>
            /// Counts age of this instance
            /// </summary>
            private readonly Stopwatch Timer;

            /// <summary>
            /// Items in the playlist
            /// </summary>
            public string[] Items { get; private set; }

            /// <summary>
            /// Age of this instance
            /// </summary>
            public TimeSpan Age => Timer.Elapsed;

            /// <summary>
            /// Creates a new cached playlist result
            /// </summary>
            /// <param name="items">Playlist items</param>
            public PlaylistInfo(string[] items)
            {
                Timer = Stopwatch.StartNew();
                Items = (string[])items.Clone();
            }

            /// <summary>
            /// Resets expiration back to zero
            /// </summary>
            public void ResetExpiration()
            {
                Timer.Restart();
            }
        }

        /// <summary>
        /// Represents an element in the audio info cache
        /// </summary>
        private class AudioInfo
        {
            /// <summary>
            /// Date of expiration
            /// </summary>
            private readonly DateTime Expiration;

            /// <summary>
            /// Audio info from YT
            /// </summary>
            public YoutubeDlResultModel Info { get; private set; }
            /// <summary>
            /// Gets how long this item can remain in the cache
            /// </summary>
            public TimeSpan Remaining { get => Expiration.Subtract(DateTime.UtcNow); }
            /// <summary>
            /// Gets if this item is expired
            /// </summary>
            public bool Expired { get => Expiration < DateTime.UtcNow; }

            /// <summary>
            /// Creates a new cached audio result 
            /// </summary>
            /// <param name="Info">Audio result</param>
            public AudioInfo(YoutubeDlResultModel Info)
            {
                this.Info = Info ?? throw new ArgumentNullException(nameof(Info));
                Expiration = DateTime.Parse(Tools.UnixZeroParse).ToUniversalTime();
                if (Uri.TryCreate(Info.Url, UriKind.Absolute, out Uri? Result) && !string.IsNullOrEmpty(Result.Query))
                {
                    //Get the numerical part from "expire=1234"
                    var ExpArg = Result.Query.Substring(1).Split('&').First(m => m.StartsWith("expire="));
                    ExpArg = ExpArg.Substring(ExpArg.IndexOf('=') + 1);
                    if (ulong.TryParse(ExpArg, out ulong Timestamp))
                    {
                        //Store expiration time minus a minute
                        Expiration = DateTime
                            .Parse(Tools.UnixZeroParse)
                            .AddSeconds(Timestamp - 60)
                            .ToUniversalTime();
                    }
                }
            }
        }
    }
}
