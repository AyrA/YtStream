using AyrA.AutoDI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YtStream.Enums;
using YtStream.Extensions;
using YtStream.Models.Ad;

namespace YtStream.Services
{
    [AutoDIRegister(AutoDIType.Transient)]
    /// <summary>
    /// Provides ad segments before, between and after streams
    /// </summary>
    public class AdsService
    {
        /// <summary>
        /// Ad config file name
        /// </summary>
        private const string ConfigFile = "ads.json";

        /// <summary>
        /// Uses the cache handler for file management but cache time is always infinite
        /// </summary>
        private readonly CacheHandler Handler;
        /// <summary>
        /// Ad config
        /// </summary>
        private readonly AdConfigModel Config;

        private readonly string _configFile;

        /// <summary>
        /// Creates a new instance
        /// </summary>
        public AdsService(CacheService cache, BasePathService basePath)
        {
            _configFile = Path.Combine(basePath.BasePath, ConfigFile);
            Handler = cache.GetHandler(CacheTypeEnum.AudioSegments, 0.0);
            Config = ReloadConfig();
        }

        /// <summary>
        /// Gets a list of all ads for the given type
        /// </summary>
        /// <param name="Type">Ad type</param>
        /// <returns>File name list</returns>
        public string[] GetList(AdTypeEnum Type)
        {
            if (Type == AdTypeEnum.None)
            {
                throw new ArgumentException($"{Type} cannot be used in AddFile()");
            }
            if (!Tools.CheckEnumFlags(Type))
            {
                throw new ArgumentException($"{Type} is not a valid {nameof(AdTypeEnum)} set");
            }
            var Collection = new List<string>();
            if (Type.HasFlag(AdTypeEnum.Intro))
            {
                Collection.AddRange(Config.Intro);
            }
            if (Type.HasFlag(AdTypeEnum.Inter))
            {
                Collection.AddRange(Config.Inter);
            }
            if (Type.HasFlag(AdTypeEnum.Outro))
            {
                Collection.AddRange(Config.Outro);
            }
            return Collection.ToArray();
        }

        /// <summary>
        /// Gets all files from the ad storage
        /// </summary>
        /// <returns>Ads</returns>
        public IEnumerable<AdFileInfoModel> GetFiles()
        {
            foreach (var F in Directory.GetFiles(Handler.CachePath))
            {
                var Name = Path.GetFileName(F);
                var Type = AdTypeEnum.None;
                if (Config.Intro.Contains(Name))
                {
                    Type |= AdTypeEnum.Intro;
                }
                if (Config.Inter.Contains(Name))
                {
                    Type |= AdTypeEnum.Inter;
                }
                if (Config.Outro.Contains(Name))
                {
                    Type |= AdTypeEnum.Outro;
                }
                yield return new AdFileInfoModel(Name, Type);
            }
        }

        /// <summary>
        /// Gets a random ad from the given categories
        /// </summary>
        /// <param name="Type">One or more categories</param>
        /// <returns>Ad, null if category mask yields no ads</returns>
        public string GetRandomAdName(AdTypeEnum Type)
        {
            if (Type == AdTypeEnum.None)
            {
                throw new ArgumentException($"{Type} cannot be used in AddFile()");
            }
            if (!Tools.CheckEnumFlags(Type))
            {
                throw new ArgumentException($"{Type} is not a valid {nameof(AdTypeEnum)} set");
            }
            var Collection = new List<string>();
            if (Type.HasFlag(AdTypeEnum.Intro))
            {
                Collection.AddRange(Config.Intro);
            }
            if (Type.HasFlag(AdTypeEnum.Inter))
            {
                Collection.AddRange(Config.Inter);
            }
            if (Type.HasFlag(AdTypeEnum.Outro))
            {
                Collection.AddRange(Config.Outro);
            }
            if (Collection.Count > 0)
            {
                var Shuffled = Tools.Shuffle(Collection.Distinct().ToArray());
                return Shuffled[Tools.GetRandom(0, Shuffled.Length)];
            }
            return null;
        }

        /// <summary>
        /// Gets a file stream from the backend
        /// </summary>
        /// <param name="Filename">File name</param>
        /// <returns>File stream</returns>
        public Stream GetAd(string Filename)
        {
            return Handler.ReadFile(Filename);
        }

        /// <summary>
        /// Imports a file to the cache and ad list
        /// </summary>
        /// <param name="Filename">File name</param>
        /// <param name="Type">One or more types</param>
        /// <returns>final file name of added file</returns>
        /// <remarks>
        /// This will not convert the file at all. Be sure to do this yourself
        /// </remarks>
        public string ImportFile(string Filename, AdTypeEnum Type)
        {
            if (Type == AdTypeEnum.None)
            {
                throw new ArgumentException($"{Type} cannot be used in AddFile()");
            }
            if (!Tools.CheckEnumFlags(Type))
            {
                throw new ArgumentException($"{Type} is not a valid {nameof(AdTypeEnum)} set");
            }
            var Result = Handler.ImportFile(Filename);
            Config.Add(Result, Type);
            SaveConfig(Config);
            return Result;
        }

        /// <summary>
        /// Registers an existing file for the given ad types
        /// </summary>
        /// <param name="Filename">File name</param>
        /// <param name="Type">One or more types</param>
        /// <remarks>Does not remove existing registrations from other types</remarks>
        public void AddFile(string Filename, AdTypeEnum Type)
        {
            if (Type == AdTypeEnum.None)
            {
                throw new ArgumentException($"{Type} cannot be used in AddFile()");
            }
            if (!Tools.CheckEnumFlags(Type))
            {
                throw new ArgumentException($"{Type} is not a valid {nameof(AdTypeEnum)} set");
            }
            if (!Handler.HasFileInCache(Filename))
            {
                throw new FileNotFoundException($"{Filename} not in ad library");
            }
            if (Config.Add(Filename, Type))
            {
                SaveConfig(Config);
            }
        }

        /// <summary>
        /// Removes a file from the given ad list
        /// </summary>
        /// <param name="Filename">File name</param>
        /// <param name="Type">Types to remove</param>
        /// <returns>true if removed</returns>
        /// <remarks>This will not delete the file. Use <see cref="DeleteFile(string)"/> instead</remarks>
        public bool RemoveFile(string Filename, AdTypeEnum Type)
        {
            if (Config.Remove(Filename, Type))
            {
                SaveConfig(Config);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Deletes a file from the store
        /// </summary>
        /// <param name="Filename">File name</param>
        /// <returns>true, if removed</returns>
        /// <remarks>This also removes the file from all ad lists</remarks>
        public bool DeleteFile(string Filename)
        {
            RemoveFile(Filename, AdTypeEnum.All);
            return Handler.DeleteFile(Filename);
        }

        /// <summary>
        /// Removes dead entries from the list that no longer physically exist on disk
        /// </summary>
        /// <remarks>Number of dead entries processed</remarks>
        public int SyncConfigWithCache()
        {
            Config.Fix();
            var Invalids = Config.Inter
                .Concat(Config.Intro)
                .Concat(Config.Outro)
                .Distinct()
                .Where(m => !Handler.HasFileInCache(m))
                .ToArray();
            foreach (var Inv in Invalids)
            {
                Config.Remove(Inv, AdTypeEnum.All);
            }
            return Invalids.Length;
        }

        /// <summary>
        /// Saves config to disk
        /// </summary>
        private void SaveConfig(AdConfigModel C)
        {
            C.Fix();
            File.WriteAllText(_configFile, C.ToJson(true));
        }

        /// <summary>
        /// Reloads config from disk
        /// </summary>
        /// <returns></returns>
        private AdConfigModel ReloadConfig()
        {
            try
            {
                var Config = File.ReadAllText(_configFile).FromJson<AdConfigModel>(true);
                Config.Fix();
                return Config;
            }
            catch (FileNotFoundException)
            {
                //Create initial config file
                var C = new AdConfigModel();
                SaveConfig(C);
                return C;
            }
        }
    }
}
