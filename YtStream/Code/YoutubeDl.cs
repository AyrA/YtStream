using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace YtStream
{
    /// <summary>
    /// Provides a youtube-dl interface
    /// </summary>
    public class YoutubeDl
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
        private static readonly Dictionary<string, PlaylistInfo> PlCache = new Dictionary<string, PlaylistInfo>();
        private static readonly Dictionary<string, AudioInfo> IdCache = new Dictionary<string, AudioInfo>();
        /// <summary>
        /// Counts time since last cache cleanup
        /// </summary>
        private static readonly Stopwatch LastClean = Stopwatch.StartNew();

        /// <summary>
        /// Youtube-dl executable
        /// </summary>
        private readonly string executable;
        /// <summary>
        /// Cached user agent
        /// </summary>
        private string userAgent = null;
        /// <summary>
        /// Cached version
        /// </summary>
        private string version = null;
        private ILogger Logger;

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
            Logger = Startup.GetLogger<YoutubeDl>();
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
            using (var P = Process.Start(PSI))
            {
                return userAgent = (await P.StandardOutput.ReadToEndAsync()).Trim();
            }
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
            var PSI = new ProcessStartInfo(executable, "--version")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            using (var P = Process.Start(PSI))
            {
                return version = (await P.StandardOutput.ReadToEndAsync()).Trim();
            }
        }

        /// <summary>
        /// Downloads a youtube playlist and returns all video ids within
        /// </summary>
        /// <param name="Playlist">Playlist id</param>
        /// <returns>Video Ids</returns>
        public async Task<string[]> GetPlaylist(string Playlist, int MaxItems)
        {
            if (!Tools.IsYoutubePlaylist(Playlist))
            {
                throw new FormatException("Argument must be a youtube playlist id");
            }
            if (MaxItems < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxItems), "Must be 0 or bigger");
            }
            CleanCache();
            lock (PlCache)
            {
                if (PlCache.TryGetValue(Playlist, out PlaylistInfo info))
                {
                    if (info.Age.TotalMinutes < MaxCacheAgeMinutes)
                    {
                        return MaxItems == 0 ? info.Items : info.Items.Take(MaxItems).ToArray();
                    }
                }
            }
            var ItemCount = MaxItems > 0 ? $"--playlist-items 1-{Math.Min(MaxPlaylistEntries, MaxItems)}" : "";
            var Args = $"--get-id {ItemCount} --flat-playlist https://www.youtube.com/playlist?list={Playlist}";
            var PSI = new ProcessStartInfo(executable, Args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            using (var P = Process.Start(PSI))
            {
                var Lines = await P.StandardOutput.ReadToEndAsync();
                var Items = Lines.Trim().Split('\n').Where(m => Tools.IsYoutubeId(m.Trim())).ToArray();
                PlCache[Playlist] = new PlaylistInfo(Items);
                return Items;
            }
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
            CleanCache();
            lock (IdCache)
            {
                if (IdCache.TryGetValue(Id, out AudioInfo info) && !info.Expired)
                {
                    return info.Info.JsonClone();
                }
                //Id not found or expired
            }
            var PSI = new ProcessStartInfo(executable, $"--skip-download --dump-json --format bestaudio {Tools.IdToUrl(Id)}")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            using (var P = Process.Start(PSI))
            {
                var Result = (await P.StandardOutput.ReadToEndAsync()).FromJson<YoutubeDlResult>();
                IdCache[Id] = new AudioInfo(Result);
                return Result;
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
            try
            {
                return (await GetAudioDetails(Id)).Url;
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Failed to obtain YT stream url", ex.Message);
            }
            return null;
        }

        /// <summary>
        /// Removes expired items from the cache
        /// </summary>
        /// <remarks>
        /// Should be called fairly often. It will clean at most once a minute
        /// </remarks>
        private static void CleanCache()
        {
            if (LastClean.Elapsed.TotalMinutes >= 1.0)
            {
                LastClean.Reset();
                lock (PlCache)
                {
                    var Items = PlCache
                        .Where(m => m.Value.Age.TotalMinutes > MaxCacheAgeMinutes)
                        .Select(m => m.Key)
                        .ToArray();
                    foreach (var Item in Items)
                    {
                        PlCache.Remove(Item);
                    }
                }
                lock (IdCache)
                {
                    var Items = IdCache.Where(m => m.Value.Expired).Select(m => m.Key).ToArray();
                    foreach (var Item in Items)
                    {
                        IdCache.Remove(Item);
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
            public TimeSpan Age
            {
                get
                {
                    return Timer.Elapsed;
                }
            }

            /// <summary>
            /// Creates a new cached playlist result
            /// </summary>
            /// <param name="Items">Playlist items</param>
            public PlaylistInfo(string[] Items)
            {
                Timer = Stopwatch.StartNew();
                this.Items = (string[])Items.Clone();
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
            public YoutubeDlResult Info { get; private set; }
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
            public AudioInfo(YoutubeDlResult Info)
            {
                this.Info = Info ?? throw new ArgumentNullException(nameof(Info));
                Expiration = DateTime.Parse(Tools.UnixZeroParse).ToUniversalTime();
                if (Uri.TryCreate(Info.Url, UriKind.Absolute, out Uri Result) && !string.IsNullOrEmpty(Result.Query))
                {
                    //Get the numerical part from "expire=1234"
                    var ExpArg = Result.Query.Substring(1).Split('&').FirstOrDefault(m => m.StartsWith("expire="));
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
