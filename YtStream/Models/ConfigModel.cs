using System;
using System.Collections.Generic;
using System.IO;
using YtStream.MP3;

namespace YtStream.Models
{
    /// <summary>
    /// User editable configuration
    /// </summary>
    public class ConfigModel : IValidateable
    {
        /// <summary>
        /// File name where config is stored
        /// </summary>
        public const string ConfigFileName = "config.json";

        /// <summary>
        /// Path to cache main directory
        /// </summary>
        public string CachePath { get; set; }

        /// <summary>
        /// Enable or disable cache
        /// </summary>
        public bool UseCache { get; set; }

        /// <summary>
        /// Enable or disable SBlock
        /// </summary>
        public bool UseSponsorBlock { get; set; }

        /// <summary>
        /// Require an account for streaming functionality
        /// </summary>
        public bool RequireAccount { get; set; }

        /// <summary>
        /// Stream data as fast as it's consumed instead of as fast as possible
        /// </summary>
        public bool SimulateRealStream { get; set; }

        /// <summary>
        /// Maximum age for MP3 files in seconds
        /// </summary>
        /// <remarks>
        /// Youtube files theoretically never expire since you can't change them,
        /// only delete them.
        /// Setting this to infinite (zero) is recommended
        /// </remarks>
        public int CacheMp3Lifetime { get; set; }

        /// <summary>
        /// SBlock response cache lifetime
        /// </summary>
        /// <remarks>
        /// Ranges do not change too often
        /// </remarks>
        public int CacheSBlockLifetime { get; set; }

        /// <summary>
        /// Maximum allowed number of keys per user
        /// </summary>
        public int MaxKeysPerUser { get; set; }

        /// <summary>
        /// Maximum allowed ids for a single stream
        /// </summary>
        /// <remarks>
        /// Playlists are counted in their expanded state
        /// </remarks>
        public int MaxStreamIds { get; set; }

        /// <summary>
        /// API key for the YT API
        /// </summary>
        /// <remarks>
        /// Some features will be unavailable if this is not set
        /// </remarks>
        public string YtApiKey { get; set; }

        /// <summary>
        /// Path to youtube-dl executable
        /// </summary>
        public string YoutubedlPath { get; set; }

        /// <summary>
        /// Path to FFmpeg executable
        /// </summary>
        public string FfmpegPath { get; set; }

        /// <summary>
        /// Hostname of sponsorblock server
        /// </summary>
        /// <remarks>
        /// Please respect the server license
        /// </remarks>
        public string SponsorBlockServer { get; set; }

        /// <summary>
        /// Enable public registration of new accounts
        /// </summary>
        /// <remarks>
        /// Only the admin can create new accounts if this is disabled.
        /// Registration is always permitted if no user account exists.
        /// </remarks>
        public bool PublicRegistration { get; set; }

        /// <summary>
        /// Enable or disable streaming ads for administrators
        /// </summary>
        public bool AdminAds { get; set; }
        
        /// <summary>
        /// Sets whether ads should be marked
        /// </summary>
        /// <remarks>
        /// This utilizes the "private" bit of the MP3 header.
        /// It will be set for ads and unset for other audio
        /// </remarks>
        public bool MarkAds { get; set; }

        /// <summary>
        /// Audio bitrate for MP3 conversion
        /// </summary>
        public Bitrate AudioBitrate { get; set; }

        /// <summary>
        /// Audio frequency for MP3 conversion
        /// </summary>
        public Frequency AudioFrequency { get; set; }

        /// <summary>
        /// Initializes a configuration with defaults
        /// </summary>
        public ConfigModel()
        {
            UseCache = true;
            UseSponsorBlock = true;
            SponsorBlockServer = SponsorBlock.DefaultHost;
            CachePath = Path.Combine(Startup.BasePath, "Cache");
            FfmpegPath = Path.Combine(Startup.BasePath, "Tools", "ffmpeg.exe");
            YoutubedlPath = Path.Combine(Startup.BasePath, "Tools", "youtube-dl.exe");
            //Cache MP3 forever
            CacheMp3Lifetime = 0;
            //7 days
            CacheSBlockLifetime = Tools.SponsorBlockCacheTime;
            MaxKeysPerUser = 10;
            //Default MP3 settings
            AudioBitrate = Converter.DefaultRate;
            AudioFrequency = Converter.DefaultFrequency;
        }

        /// <summary>
        /// Save this instance to the default configuration file
        /// </summary>
        public void Save()
        {
            var F = Path.Combine(Startup.BasePath, ConfigFileName);
            File.WriteAllText(F, this.ToJson(true));
        }

        public string[] GetValidationMessages()
        {
            var Messages = new List<string>();
            if (!Enum.IsDefined(typeof(Bitrate), AudioBitrate))
            {
                Messages.Add("Audio bitrate has an invalid value");
            }
            if (!Enum.IsDefined(typeof(Frequency), AudioFrequency))
            {
                Messages.Add("Audio frequency has an invalid value");
            }
            if (CacheMp3Lifetime < 0 || CacheSBlockLifetime < 0)
            {
                Messages.Add("Cache lifetime cannot be negative");
            }
            if (MaxKeysPerUser < 0)
            {
                Messages.Add(nameof(MaxKeysPerUser) + " cannot be negative. Use zero to impose no limit instead.");
            }
            if (MaxStreamIds < 0)
            {
                Messages.Add(nameof(MaxStreamIds) + " cannot be negative. Use zero to impose no limit instead.");
            }
            if (UseCache)
            {
                if (string.IsNullOrWhiteSpace(CachePath))
                {
                    Messages.Add("Cache path is not set");
                }
                else
                {
                    if (!Path.IsPathFullyQualified(CachePath))
                    {
                        Messages.Add("Cache path must be absolute");
                    }
                    else if (!Directory.Exists(CachePath) && !Directory.Exists(Path.GetDirectoryName(CachePath)))
                    {
                        Messages.Add("Cache path (or parent) does not exist");
                    }
                }
            }
            if (UseSponsorBlock)
            {
                if (string.IsNullOrWhiteSpace(SponsorBlockServer))
                {
                    Messages.Add($"Sponsorblock server address not set. Default would be {SponsorBlock.DefaultHost}");
                }
                else if (Uri.CheckHostName(SponsorBlockServer) != UriHostNameType.Dns)
                {
                    Messages.Add("Invalid sponsor block host name");
                }
            }
            if (string.IsNullOrWhiteSpace(YoutubedlPath))
            {
                Messages.Add("Youtube-dl path is not set");
            }
            else if (!File.Exists(YoutubedlPath))
            {
                Messages.Add("Youtube-dl not found");
            }
            if (string.IsNullOrWhiteSpace(FfmpegPath))
            {
                Messages.Add("FFmpeg path is not set");
            }
            else if (!File.Exists(FfmpegPath))
            {
                Messages.Add("FFmpeg not found");
            }
            return Messages.ToArray();
        }

        public bool IsValid()
        {
            return GetValidationMessages().Length == 0;
        }

        /// <summary>
        /// Loads settings from the default confgiuration file
        /// </summary>
        /// <returns>Settings. Defaults if no file was found</returns>
        public static ConfigModel Load()
        {
            var F = Path.Combine(Startup.BasePath, ConfigFileName);
            try
            {
                return File.ReadAllText(F).FromJson<ConfigModel>(true);
            }
            catch (FileNotFoundException)
            {
                return new ConfigModel();
            }
        }
    }
}
