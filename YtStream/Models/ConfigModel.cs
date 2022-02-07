using System;
using System.Collections.Generic;
using System.IO;

namespace YtStream.Models
{
    public class ConfigModel : IValidateable
    {
        public const string ConfigFileName = "config.json";

        public string CachePath { get; set; }

        public bool UseCache { get; set; }

        public bool UseSponsorBlock { get; set; }

        public int CacheMp3Lifetime { get; set; }

        public int CacheSBlockLifetime { get; set; }

        public string YoutubedlPath { get; set; }

        public string FfmpegPath { get; set; }

        public string SponsorBlockServer { get; set; }

        public ConfigModel()
        {
            UseCache = true;
            UseSponsorBlock = true;
            SponsorBlockServer = SponsorBlock.DefaultHost;
            CachePath = Path.Combine(Startup.BasePath, "Cache");
            FfmpegPath = Path.Combine(Startup.BasePath, "Tools", "ffmpeg.exe");
            YoutubedlPath = Path.Combine(Startup.BasePath, "Tools", "youtube-dl.exe");
            CacheMp3Lifetime = 0;
            CacheSBlockLifetime = 86400 * 7;
        }

        public void Save()
        {
            var F = Path.Combine(Startup.BasePath, ConfigFileName);
            File.WriteAllText(F, this.ToJson());
        }

        public string[] GetValidationMessages()
        {
            var Messages = new List<string>();
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
