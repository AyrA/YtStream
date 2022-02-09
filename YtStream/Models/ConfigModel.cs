using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

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
        /// Buffer for the HTTP MP3 output
        /// </summary>
        public int OutputBufferKB { get; set; }

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
        /// Encrypted password for the settings
        /// </summary>
        /// <remarks>
        /// Not actually encrypted, but the result of a KDF
        /// Format: Salt:Iterations:Bytes
        /// </remarks>
        public string EncryptedPassword { get; set; }

        /// <summary>
        /// Gets if a password has been set
        /// </summary>
        [JsonIgnore]
        public bool HasPassword { get => !string.IsNullOrEmpty(EncryptedPassword); }

        /// <summary>
        /// Gets if the password should be set
        /// </summary>
        [JsonIgnore]
        public bool ShouldChangePassword
        {
            get
            {
                return !string.IsNullOrEmpty(AdminPassword) &&
                    AdminPassword.Length > 7 &&
                    AdminPassword == AdminPasswordVerify;
            }
        }

        /// <summary>
        /// Gets the unencrypted admin password from the web UI
        /// </summary>
        [JsonIgnore]
        public string AdminPassword { get; set; }

        /// <summary>
        /// Verification of <see cref="AdminPassword"/>
        /// </summary>
        [JsonIgnore]
        public string AdminPasswordVerify { get; set; }

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
            //5M is enough for 3 min at 192 kbps
            OutputBufferKB = 5000;
        }

        /// <summary>
        /// Convert <see cref="AdminPassword"/> into <see cref="EncryptedPassword"/>
        /// </summary>
        public void EncryptPassword()
        {
            if (!ShouldChangePassword)
            {
                throw new InvalidOperationException(nameof(AdminPassword) + " not secure");
            }
            using (var Enc = new Rfc2898DeriveBytes(AdminPassword, 16, 100000, HashAlgorithmName.SHA256))
            {
                EncryptedPassword = Convert.ToBase64String(Enc.Salt) + ":100000:" + Convert.ToBase64String(Enc.GetBytes(16));
            }
        }

        /// <summary>
        /// Checks the supplied password against <see cref="EncryptedPassword"/>
        /// </summary>
        /// <param name="Password">Password</param>
        /// <returns>true if password valid</returns>
        /// <remarks>Will accept empty password if <see cref="HasPassword"/> is false</remarks>
        public bool CheckPassword(string Password)
        {
            if (string.IsNullOrEmpty(Password))
            {
                return !HasPassword;
            }
            if (!string.IsNullOrEmpty(EncryptedPassword))
            {
                var Parts = EncryptedPassword.Split(':');
                if (Parts.Length != 3)
                {
                    return false;
                }
                using (var Enc = new Rfc2898DeriveBytes(Password, Convert.FromBase64String(Parts[0]), int.Parse(Parts[1]), HashAlgorithmName.SHA256))
                {
                    return Parts[2] == Convert.ToBase64String(Enc.GetBytes(16));
                }
            }
            return false;
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
            if (!string.IsNullOrEmpty(AdminPassword) || !string.IsNullOrEmpty(AdminPasswordVerify))
            {
                if (AdminPassword == null || AdminPassword.Length < 8)
                {
                    Messages.Add("Password must be at least 8 characters");
                }
                else if (AdminPassword != AdminPasswordVerify)
                {
                    Messages.Add("Passwords do not match");
                }
            }
            if (CacheMp3Lifetime < 0 || CacheSBlockLifetime < 0)
            {
                Messages.Add("Cache lifetime cannot be negative");
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
            if (OutputBufferKB < 1)
            {
                Messages.Add("Output buffer must be at least 1");
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
