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
        /// <param name="cachePath">Cache path</param>
        /// <param name="defaultCacheLifetime">Default object lifetime</param>
        public CacheHandler(string cachePath, TimeSpan defaultCacheLifetime)
        {
            if (string.IsNullOrWhiteSpace(cachePath))
            {
                throw new ArgumentException($"'{nameof(cachePath)}' cannot be null or whitespace.", nameof(cachePath));
            }
            if (defaultCacheLifetime.Ticks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(defaultCacheLifetime), "Cache time cannot be negative");
            }
            Directory.CreateDirectory(cachePath);
            CachePath = Directory.CreateDirectory(cachePath).FullName;
            DefaultCacheLifetime = defaultCacheLifetime;
        }

        /// <summary>
        /// Gets the full path for the given file name
        /// </summary>
        /// <param name="fileName">File name</param>
        /// <returns>Full file path</returns>
        private string GetPath(string fileName)
        {
            var P = Path.GetFullPath(Path.Combine(CachePath, fileName));
            if (P.StartsWith(CachePath + Path.DirectorySeparatorChar))
            {
                return P;
            }
            throw new IOException($"{fileName} outside of {CachePath}");
        }

        /// <summary>
        /// Gets the full path for the given file name
        /// </summary>
        /// <param name="fileName">File name</param>
        /// <returns>Full file path</returns>
        public string GetCacheFileName(string fileName)
        {
            return GetPath(fileName);
        }

        /// <summary>
        /// Checks if the given file exists in the cache
        /// </summary>
        /// <param name="fileName">File name</param>
        /// <returns>true if file exists.</returns>
        /// <remarks>Does not check if the file is stale</remarks>
        public bool HasFileInCache(string fileName)
        {
            return File.Exists(GetPath(fileName));
        }

        /// <summary>
        /// Gets the age of the file
        /// </summary>
        /// <param name="fileName">File name</param>
        /// <returns>File age</returns>
        public DateTime GetFileAge(string fileName)
        {
            return File.GetLastWriteTimeUtc(GetPath(fileName));
        }

        /// <summary>
        /// Opens a cache file for reading
        /// </summary>
        /// <param name="fileName">File name</param>
        /// <returns>File</returns>
        public FileStream ReadFile(string fileName)
        {
            return File.OpenRead(GetPath(fileName));
        }

        /// <summary>
        /// Creates or appends to a file
        /// </summary>
        /// <param name="fileName">File name</param>
        /// <param name="append">true to append instead of overwrite</param>
        /// <returns>File</returns>
        public FileStream WriteFile(string fileName, bool append = false)
        {
            if (append)
            {
                return File.Open(GetPath(fileName), FileMode.Append);
            }
            return File.Create(GetPath(fileName));
        }

        /// <summary>
        /// Opens a file for reading if it's not stale
        /// </summary>
        /// <param name="fileName">File name</param>
        /// <returns>File, or null if stale or not found</returns>
        public FileStream? OpenIfNotStale(string fileName)
        {
            return OpenIfNotStale(fileName, DefaultCacheLifetime);
        }

        /// <summary>
        /// Opens a file for reading if it's not stale
        /// </summary>
        /// <param name="fileName">File name</param>
        /// <param name="maxAge">Maximum allowed file age</param>
        /// <returns>File, or null if stale or not found</returns>
        public FileStream? OpenIfNotStale(string fileName, TimeSpan maxAge)
        {
            try
            {
                var stale = false;
                var FS = ReadFile(fileName);
                //Never stale if zero
                if (FS != null && maxAge.Ticks == 0)
                {
                    return FS;
                }
                try
                {
                    stale = IsStale(fileName, maxAge);
                }
                catch
                {
                    stale = true;
                }
                if (stale)
                {
                    FS?.Close();
                    FS?.Dispose();
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
        /// Updates the last write timestamp to the current date
        /// </summary>
        /// <param name="fileName">File name</param>
        /// <returns>true, if time was updated</returns>
        public bool Poke(string fileName)
        {
            try
            {
                File.SetLastWriteTimeUtc(GetPath(fileName), DateTime.UtcNow);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Deletes the given file
        /// </summary>
        /// <param name="fileName">File name</param>
        /// <returns>true if deleted. False if failed or not found</returns>
        public bool DeleteFile(string fileName)
        {
            try
            {
                File.Delete(GetPath(fileName));
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
        /// <param name="filename">File name</param>
        /// <param name="move">
        /// If true, the file is moved instead of copied
        /// </param>
        /// <param name="avoidConflict">
        /// If true, the file name is changed if needed to avoid conflicts.
        /// If false, the destination is overwritten
        /// </param>
        /// <returns>
        /// Final file name under which the file is stored.
        /// </returns>
        /// <remarks>
        /// The result is usually the file name component from <paramref name="filename"/>,
        /// but it may differ if <paramref name="avoidConflict"/> is used and a conflict was present.
        /// </remarks>
        public string ImportFile(string filename, bool move = false, bool avoidConflict = true)
        {
            var BaseName = Path.GetFileName(filename);
            if (avoidConflict)
            {
                BaseName = GetNoConfictName(filename, true);
            }
            var Dest = Path.Combine(CachePath, BaseName);
            if (move)
            {
                File.Move(filename, Dest, true);
            }
            else
            {
                File.Copy(filename, Dest, true);
            }
            return BaseName;
        }

        /// <summary>
        /// Gets a file name that doesn't conflicts with existing names in the cache
        /// </summary>
        /// <param name="filename">File name. Path component is discarded</param>
        /// <param name="createFile">If set, the file with the final name is created empty</param>
        /// <returns>Conflict free file name</returns>
        /// <remarks>
        /// Conflict prevention is done by adding an incremental counter if the initial name caused a conflict.
        /// </remarks>
        public string GetNoConfictName(string filename, bool createFile = false)
        {
            var BaseName = Path.GetFileName(filename);
            var Index = 0;
            do
            {
                if (createFile)
                {
                    try
                    {
                        using (var FS = File.Open(Path.Combine(CachePath, BaseName), FileMode.CreateNew))
                        {
                            FS.Close();
                        }
                        return BaseName;
                    }
                    catch
                    {
                        ++Index;
                        BaseName = Path.GetFileNameWithoutExtension(filename) + $"_{Index}" + Path.GetExtension(filename);
                    }
                }
                else
                {
                    if (File.Exists(Path.Combine(CachePath, BaseName)))
                    {
                        ++Index;
                        BaseName = Path.GetFileNameWithoutExtension(filename) + $"_{Index}" + Path.GetExtension(filename);
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
        /// <param name="fileName">File name</param>
        /// <returns>Time to stale</returns>
        public TimeSpan TimeToStale(string fileName)
        {
            return TimeToStale(fileName, DefaultCacheLifetime);
        }

        /// <summary>
        /// Gets how long until the given file becomes stale
        /// </summary>
        /// <param name="fileName">File name</param>
        /// <param name="maxAge">Stale cutoff</param>
        /// <returns>Time to stale. Zero if not found or already stale</returns>
        public TimeSpan TimeToStale(string fileName, TimeSpan maxAge)
        {
            if (maxAge.Ticks == 0)
            {
                return TimeSpan.MaxValue;
            }
            try
            {
                var Age = DateTime.UtcNow.Subtract(GetFileAge(fileName));
                if (Age <= maxAge)
                {
                    return maxAge - Age;
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
        /// <param name="fileName">File name</param>
        /// <returns>true if stale</returns>
        public bool IsStale(string fileName)
        {
            return IsStale(fileName, DefaultCacheLifetime);
        }

        /// <summary>
        /// Checks if the file is stale
        /// </summary>
        /// <param name="fileName">File name</param>
        /// <param name="maxAge">Time to stale</param>
        /// <returns>true if stale</returns>
        public bool IsStale(string fileName, TimeSpan maxAge)
        {
            if (maxAge.Ticks < 0)
            {
                return true;
            }
            var F = GetPath(fileName);
            if (!File.Exists(F))
            {
                return true;
            }
            //A timeout of zero is never stale
            return maxAge.Ticks > 0 && DateTime.UtcNow.Subtract(File.GetLastWriteTimeUtc(F)) >= maxAge;
        }

        /// <summary>
        /// Checks if the file is stale
        /// </summary>
        /// <param name="fileName">File name</param>
        /// <param name="seconds">Time to stale</param>
        /// <returns>true if stale</returns>
        public bool IsStale(string fileName, int seconds)
        {
            return IsStale(fileName, TimeSpan.FromSeconds(seconds));
        }

        /// <summary>
        /// Checks if the file is stale
        /// </summary>
        /// <param name="fileName">File name</param>
        /// <param name="maxAge">Date before which it is stale</param>
        /// <returns>true if stale</returns>
        public bool IsStale(string fileName, DateTime maxAge)
        {
            return IsStale(fileName, DateTime.UtcNow.Subtract(maxAge.ToUniversalTime()));
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
        /// <param name="maxAge">Maximum allowed file age</param>
        /// <returns>Number of deleted files</returns>
        public int ClearStale(TimeSpan maxAge)
        {
            if (maxAge.Ticks == 0)
            {
                return 0;
            }
            int Removed = 0;
            var DI = new DirectoryInfo(CachePath);
            var Cutoff = DateTime.UtcNow.Subtract(maxAge);
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