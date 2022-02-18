using System;
using System.IO;

namespace YtStream
{
    /// <summary>
    /// Provides file based caching
    /// </summary>
    public class CacheHandler
    {
        /// <summary>
        /// Gets the cache directory
        /// </summary>
        public string CachePath { get; }

        /// <summary>
        /// Gets the default object lifetime
        /// </summary>
        public TimeSpan DefaultCacheLifetime { get; }

        /// <summary>
        /// Creates a new cache instance
        /// </summary>
        /// <param name="CachePath">Cache path</param>
        /// <param name="DefaultCacheLifetime">Default object lifetime</param>
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

        /// <summary>
        /// Gets the full path for the given file name
        /// </summary>
        /// <param name="FileName">File name</param>
        /// <returns>Full file path</returns>
        private string GetPath(string FileName)
        {
            var P = Path.GetFullPath(Path.Combine(CachePath, FileName));
            if (P.StartsWith(CachePath + Path.DirectorySeparatorChar))
            {
                return P;
            }
            throw new IOException($"{FileName} outside of {CachePath}");
        }

        /// <summary>
        /// Gets the full path for the given file name
        /// </summary>
        /// <param name="FileName">File name</param>
        /// <returns>Full file path</returns>
        public string GetCacheFileName(string FileName)
        {
            return GetPath(FileName);
        }

        /// <summary>
        /// Checks if the given file exists in the cache
        /// </summary>
        /// <param name="FileName">File name</param>
        /// <returns>true if file exists.</returns>
        /// <remarks>Does not check if the file is stale</remarks>
        public bool HasFileInCache(string FileName)
        {
            return File.Exists(GetPath(FileName));
        }

        /// <summary>
        /// Gets the age of the file
        /// </summary>
        /// <param name="FileName">File name</param>
        /// <returns>File age</returns>
        public DateTime GetFileAge(string FileName)
        {
            return File.GetLastWriteTimeUtc(GetPath(FileName));
        }

        /// <summary>
        /// Opens a cache file for reading
        /// </summary>
        /// <param name="FileName">File name</param>
        /// <returns>File</returns>
        public FileStream ReadFile(string FileName)
        {
            return File.OpenRead(GetPath(FileName));
        }

        /// <summary>
        /// Creates or appends to a file
        /// </summary>
        /// <param name="FileName">File name</param>
        /// <param name="Append">true to append instead of overwrite</param>
        /// <returns>File</returns>
        public FileStream WriteFile(string FileName, bool Append = false)
        {
            if (Append)
            {
                return File.Open(GetPath(FileName), FileMode.Append);
            }
            return File.Create(GetPath(FileName));
        }

        /// <summary>
        /// Opens a file for reading if it's not stale
        /// </summary>
        /// <param name="FileName">File name</param>
        /// <returns>File, or null if stale or not found</returns>
        public FileStream OpenIfNotStale(string FileName)
        {
            return OpenIfNotStale(FileName, DefaultCacheLifetime);
        }

        /// <summary>
        /// Opens a file for reading if it's not stale
        /// </summary>
        /// <param name="FileName">File name</param>
        /// <param name="MaxAge">Maximum allowed file age</param>
        /// <returns>File, or null if stale or not found</returns>
        public FileStream OpenIfNotStale(string FileName, TimeSpan MaxAge)
        {
            try
            {
                var stale = false;
                var FS = ReadFile(FileName);
                //Never stale if zero
                if (FS != null && MaxAge.Ticks == 0)
                {
                    return FS;
                }
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

        /// <summary>
        /// Deletes the given file
        /// </summary>
        /// <param name="FileName">File name</param>
        /// <returns>true if deleted. False if failed or not found</returns>
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

        /// <summary>
        /// Imports an existing file into the cache
        /// </summary>
        /// <param name="Filename">File name</param>
        /// <param name="Move">
        /// If true, the file is moved instead of copied
        /// </param>
        /// <param name="AvoidConflict">
        /// If true, the file name is changed if needed to avoid conflicts.
        /// If false, the destination is overwritten
        /// </param>
        /// <returns>
        /// Final file name under which the file is stored.
        /// </returns>
        /// <remarks>
        /// The result is usually the file name component from <paramref name="Filename"/>,
        /// but it may differ if <paramref name="AvoidConflict"/> is used and a conflict was present.
        /// </remarks>
        public string ImportFile(string Filename, bool Move = false, bool AvoidConflict = true)
        {
            var BaseName = Path.GetFileName(Filename);
            if (AvoidConflict)
            {
                BaseName = GetNoConfictName(Filename, true);
            }
            var Dest = Path.Combine(CachePath, BaseName);
            if (Move)
            {
                File.Move(Filename, Dest, true);
            }
            else
            {
                File.Copy(Filename, Dest, true);
            }
            return BaseName;
        }

        /// <summary>
        /// Gets a file name that doesn't conflicts with existing names in the cache
        /// </summary>
        /// <param name="Filename">File name. Path component is discarded</param>
        /// <param name="CreateFile">If set, the file with the final name is created empty</param>
        /// <returns>Conflict free file name</returns>
        /// <remarks>
        /// Conflict prevention is done by adding an incremental counter if the initial name caused a conflict.
        /// </remarks>
        public string GetNoConfictName(string Filename, bool CreateFile = false)
        {
            var BaseName = Path.GetFileName(Filename);
            var Index = 0;
            do
            {
                if (CreateFile)
                {
                    try
                    {
                        using (var FS = File.Open(Path.Combine(Cache.BaseDirectory, BaseName), FileMode.CreateNew))
                        {
                            FS.Close();
                        }
                        return BaseName;
                    }
                    catch
                    {
                        ++Index;
                        BaseName = Path.GetFileNameWithoutExtension(Filename) + $"_{Index}" + Path.GetExtension(Filename);
                    }
                }
                else
                {
                    if (File.Exists(Path.Combine(CachePath, BaseName)))
                    {
                        ++Index;
                        BaseName = Path.GetFileNameWithoutExtension(Filename) + $"_{Index}" + Path.GetExtension(Filename);
                    }
                    else
                    {
                        return BaseName;
                    }
                }
            } while (true);
        }

        /// <summary>
        /// Gets how long until the given file becomes stale
        /// </summary>
        /// <param name="FileName">File name</param>
        /// <returns>Time to stale</returns>
        public TimeSpan TimeToStale(string FileName)
        {
            return TimeToStale(FileName, DefaultCacheLifetime);
        }

        /// <summary>
        /// Gets how long until the given file becomes stale
        /// </summary>
        /// <param name="FileName">File name</param>
        /// <param name="MaxAge">Stale cutoff</param>
        /// <returns>Time to stale. Zero if not found or already stale</returns>
        public TimeSpan TimeToStale(string FileName, TimeSpan MaxAge)
        {
            if (MaxAge.Ticks == 0)
            {
                return TimeSpan.MaxValue;
            }
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

        /// <summary>
        /// Checks if the file is stale
        /// </summary>
        /// <param name="FileName">File name</param>
        /// <returns>true if stale</returns>
        public bool IsStale(string FileName)
        {
            return IsStale(FileName, DefaultCacheLifetime);
        }

        /// <summary>
        /// Checks if the file is stale
        /// </summary>
        /// <param name="FileName">File name</param>
        /// <param name="MaxAge">Time to stale</param>
        /// <returns>true if stale</returns>
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
            //A timeout of zero is never stale
            return MaxAge.Ticks > 0 && DateTime.UtcNow.Subtract(File.GetLastWriteTimeUtc(F)) >= MaxAge;
        }

        /// <summary>
        /// Checks if the file is stale
        /// </summary>
        /// <param name="FileName">File name</param>
        /// <param name="Seconds">Time to stale</param>
        /// <returns>true if stale</returns>
        public bool IsStale(string FileName, int Seconds)
        {
            return IsStale(FileName, TimeSpan.FromSeconds(Seconds));
        }

        /// <summary>
        /// Checks if the file is stale
        /// </summary>
        /// <param name="FileName">File name</param>
        /// <param name="MaxAge">Date before which it is stale</param>
        /// <returns>true if stale</returns>
        public bool IsStale(string FileName, DateTime MaxAge)
        {
            return IsStale(FileName, DateTime.UtcNow.Subtract(MaxAge.ToUniversalTime()));
        }

        /// <summary>
        /// Deletes all stale files
        /// </summary>
        /// <returns>Number of deleted files</returns>
        public int ClearStale()
        {
            return ClearStale(DefaultCacheLifetime);
        }

        /// <summary>
        /// Deletes all stale files
        /// </summary>
        /// <param name="MaxAge">Maximum allowed file age</param>
        /// <returns>Number of deleted files</returns>
        public int ClearStale(TimeSpan MaxAge)
        {
            if (MaxAge.Ticks == 0)
            {
                return 0;
            }
            int Removed = 0;
            var DI = new DirectoryInfo(CachePath);
            var Cutoff = DateTime.UtcNow.Subtract(MaxAge);
            foreach (var FI in DI.EnumerateFiles(CachePath))
            {
                if (FI.LastWriteTimeUtc < Cutoff)
                {
                    try
                    {
                        FI.Delete();
                        ++Removed;
                    }
                    catch
                    {
                        //NOOP
                    }
                }
            }
            return Removed;
        }

        /// <summary>
        /// Purges the cache
        /// </summary>
        /// <returns>Number of files deleted</returns>
        /// <remarks>
        /// Files unable to be deleted will silently remain in the cache
        /// </remarks>
        public int Purge()
        {
            var Removed = 0;
            foreach (var F in Directory.GetFiles(CachePath))
            {
                try
                {
                    File.Delete(F);
                    ++Removed;
                }
                catch
                {
                    //NOOP
                }
            }
            return Removed;
        }
    }
}