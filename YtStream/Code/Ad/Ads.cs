using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace YtStream.Ad
{
    /// <summary>
    /// Provides ad segments before, between and after streams
    /// </summary>
    public class Ads
    {
        /// <summary>
        /// Ad config file name
        /// </summary>
        private const string ConfigFile = "config.json";

        /// <summary>
        /// Uses the cache handler for file management but cache time is always infinite
        /// </summary>
        private readonly CacheHandler Handler;
        /// <summary>
        /// Ad config
        /// </summary>
        private readonly AdConfig Config;

        /// <summary>
        /// Creates a new instance
        /// </summary>
        public Ads()
        {
            Handler = Cache.GetHandler(Cache.CacheType.AudioSegments, 0.0);
            Config = ReloadConfig();
        }

        /// <summary>
        /// Gets a list of all ads for the given type
        /// </summary>
        /// <param name="Type">Ad type</param>
        /// <returns>File name list</returns>
        public string[] GetList(AdType Type)
        {
            if (Type == AdType.None)
            {
                throw new ArgumentException($"{Type} cannot be used in AddFile()");
            }
            if (!Tools.CheckEnumFlags(Type))
            {
                throw new ArgumentException($"{Type} is not a valid {nameof(AdType)} set");
            }
            var Collection = new List<string>();
            if (Type.HasFlag(AdType.Intro))
            {
                Collection.AddRange(Config.Intro);
            }
            if (Type.HasFlag(AdType.Inter))
            {
                Collection.AddRange(Config.Inter);
            }
            if (Type.HasFlag(AdType.Outro))
            {
                Collection.AddRange(Config.Outro);
            }
            return Collection.ToArray();
        }

        public IEnumerable<AdFileInfo> GetFiles()
        {
            foreach(var F in Directory.GetFiles(Handler.CachePath))
            {
                var Name = Path.GetFileName(F);
                if (Name == ConfigFile)
                {
                    continue;
                }
                var Type = AdType.None;
                if (Config.Intro.Contains(Name))
                {
                    Type |= AdType.Intro;
                }
                if (Config.Inter.Contains(Name))
                {
                    Type |= AdType.Inter;
                }
                if (Config.Outro.Contains(Name))
                {
                    Type |= AdType.Outro;
                }
                yield return new AdFileInfo(Name, Type);
            }
        }

        /// <summary>
        /// Gets a random ad from the given categories
        /// </summary>
        /// <param name="Type">One or more categories</param>
        /// <returns>Ad, null if category mask yields no ads</returns>
        public string GetRandomAdName(AdType Type)
        {
            if (Type == AdType.None)
            {
                throw new ArgumentException($"{Type} cannot be used in AddFile()");
            }
            if (!Tools.CheckEnumFlags(Type))
            {
                throw new ArgumentException($"{Type} is not a valid {nameof(AdType)} set");
            }
            var Collection = new List<string>();
            if (Type.HasFlag(AdType.Intro))
            {
                Collection.AddRange(Config.Intro);
            }
            if (Type.HasFlag(AdType.Inter))
            {
                Collection.AddRange(Config.Inter);
            }
            if (Type.HasFlag(AdType.Outro))
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
        public string ImportFile(string Filename, AdType Type)
        {
            if (Type == AdType.None)
            {
                throw new ArgumentException($"{Type} cannot be used in AddFile()");
            }
            if (!Tools.CheckEnumFlags(Type))
            {
                throw new ArgumentException($"{Type} is not a valid {nameof(AdType)} set");
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
        public void AddFile(string Filename, AdType Type)
        {
            if (Type == AdType.None)
            {
                throw new ArgumentException($"{Type} cannot be used in AddFile()");
            }
            if (!Tools.CheckEnumFlags(Type))
            {
                throw new ArgumentException($"{Type} is not a valid {nameof(AdType)} set");
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
        public bool RemoveFile(string Filename, AdType Type)
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
            RemoveFile(Filename, AdType.All);
            return Handler.DeleteFile(Filename);
        }

        /// <summary>
        /// Removes dead entries from the list that no longer physically exist on disk
        /// </summary>
        /// <remarks>Number of dead entries processed</remarks>
        public int SyncConfig()
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
                Config.Remove(Inv, AdType.All);
            }
            return Invalids.Length;
        }

        /// <summary>
        /// Saves config to disk
        /// </summary>
        private void SaveConfig(AdConfig C)
        {
            C.Fix();
            using (var FS = Handler.WriteFile(ConfigFile))
            {
                Tools.WriteString(FS, C.ToJson(true));
            }
        }

        /// <summary>
        /// Reloads config from disk
        /// </summary>
        /// <returns></returns>
        private AdConfig ReloadConfig()
        {
            try
            {
                using (var FS = Handler.ReadFile(ConfigFile))
                {
                    var Config = Tools.ReadString(FS).FromJson<AdConfig>(true);
                    Config.Fix();
                    return Config;
                }
            }
            catch (FileNotFoundException)
            {
                var C = new AdConfig();
                SaveConfig(C);
                return C;
                //NOOP
            }
        }

        private class AdConfig
        {
            public List<string> Intro { get; set; }
            public List<string> Outro { get; set;}
            public List<string> Inter { get; set; }

            public AdConfig()
            {
                Intro = new List<string>();
                Outro = new List<string>();
                Inter = new List<string>();
            }

            public bool Add(string Name, AdType Type)
            {
                var ok = false;
                if (Type.HasFlag(AdType.Inter) && !Inter.Contains(Name))
                {
                    Inter.Add(Name);
                    ok = true;
                }
                if (Type.HasFlag(AdType.Intro) && !Intro.Contains(Name))
                {
                    Intro.Add(Name);
                    ok = true;
                }
                if (Type.HasFlag(AdType.Outro) && !Outro.Contains(Name))
                {
                    Outro.Add(Name);
                    ok = true;
                }
                return ok;
            }

            public bool Remove(string Name, AdType Type)
            {
                var ok = false;
                if (Type.HasFlag(AdType.Inter))
                {
                    ok |= Inter.Remove(Name);
                }
                if (Type.HasFlag(AdType.Intro))
                {
                    ok |= Intro.Remove(Name);
                }
                if (Type.HasFlag(AdType.Outro))
                {
                    ok |= Outro.Remove(Name);
                }
                return ok;
            }

            public void Fix()
            {
                if (Inter == null)
                {
                    Inter = new List<string>();
                }
                else
                {
                    Inter = Inter.Distinct().ToList();
                }
                if (Intro == null)
                {
                    Intro = new List<string>();
                }
                else
                {
                    Intro = Intro.Distinct().ToList();
                }
                if (Outro == null)
                {
                    Outro = new List<string>();
                }
                else
                {
                    Outro = Outro.Distinct().ToList();
                }
            }
        }
    }
}
