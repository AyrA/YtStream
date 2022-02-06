using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace YtStream
{
    public class CacheHandler
    {
        public string CachePath { get; }
        public TimeSpan DefaultCacheLifetime { get; }

        public CacheHandler(string CachePath, TimeSpan DefaultCacheLifetime)
        {
            if (string.IsNullOrWhiteSpace(CachePath))
            {
                throw new ArgumentException($"'{nameof(CachePath)}' cannot be null or whitespace.", nameof(CachePath));
            }
            if (DefaultCacheLifetime.Ticks < 0)
            {
                throw new ArgumentOutOfRangeException("Cache time cannot be negative");
            }
            Directory.CreateDirectory(CachePath);
            this.CachePath = Directory.CreateDirectory(CachePath).FullName;
            this.DefaultCacheLifetime = DefaultCacheLifetime;
        }

        private string GetPath(string FileName)
        {
            var P = Path.GetFullPath(Path.Combine(CachePath, FileName));
            if (P.StartsWith(CachePath + Path.DirectorySeparatorChar))
            {
                return P;
            }
            throw new IOException($"{FileName} outside of {CachePath}");
        }

        public string GetCacheFileName(string FileName)
        {
            return GetPath(FileName);
        }

        public bool HasFileInCache(string FileName)
        {
            return File.Exists(GetPath(FileName));
        }

        public DateTime GetFileAge(string FileName)
        {
            return File.GetLastWriteTimeUtc(GetPath(FileName));
        }

        public FileStream ReadFile(string FileName)
        {
            return File.OpenRead(GetPath(FileName));
        }

        public FileStream WriteFile(string FileName, bool Append = false)
        {
            if (Append)
            {
                return File.Open(GetPath(FileName), FileMode.Append);
            }
            return File.Create(GetPath(FileName));
        }

        public FileStream OpenIfNotStale(string FileName)
        {
            return OpenIfNotStale(FileName, DefaultCacheLifetime);
        }

        public FileStream OpenIfNotStale(string FileName, TimeSpan MaxAge)
        {
            try
            {
                var stale = false;
                var FS = ReadFile(FileName);
                try
                {
                    stale = IsStale(FileName, MaxAge);
                }
                catch
                {
                    stale = true;
                }
                if (stale)
                {
                    FS.Close();
                    FS.Dispose();
                    return null;
                }
                return FS;
            }
            catch (FileNotFoundException)
            {
                return null;
            }
        }

        public bool DeleteFile(string FileName)
        {
            try
            {
                File.Delete(GetPath(FileName));
            }
            catch
            {
                return false;
            }
            return true;
        }

        public TimeSpan TimeToStale(string FileName)
        {
            return TimeToStale(FileName, DefaultCacheLifetime);
        }

        public TimeSpan TimeToStale(string FileName, TimeSpan MaxAge)
        {
            try
            {
                var Age = DateTime.UtcNow.Subtract(GetFileAge(FileName));
                if (Age <= MaxAge)
                {
                    return MaxAge - Age;
                }
            }
            catch
            {
                //NOOP
            }
            return TimeSpan.Zero;
        }

        public bool IsStale(string FileName)
        {
            return IsStale(FileName, DefaultCacheLifetime);
        }

        public bool IsStale(string FileName, TimeSpan MaxAge)
        {
            if (MaxAge.Ticks < 0)
            {
                return true;
            }
            var F = GetPath(FileName);
            if (!File.Exists(F))
            {
                return true;
            }
            return DateTime.UtcNow.Subtract(File.GetLastWriteTimeUtc(F)) >= MaxAge;
        }

        public bool IsStale(string FileName, int Seconds)
        {
            return IsStale(FileName, TimeSpan.FromSeconds(Seconds));
        }

        public bool IsStale(string FileName, DateTime MaxAge)
        {
            return IsStale(FileName, DateTime.UtcNow.Subtract(MaxAge.ToUniversalTime()));
        }
    }
}
