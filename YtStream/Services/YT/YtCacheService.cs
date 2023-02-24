using AyrA.AutoDI;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace YtStream.Services.YT
{
    [AutoDIRegister(AutoDIType.Singleton)]
    /// <summary>
    /// Caches responses from the youtube API
    /// </summary>
    public class YtCacheService : IDisposable
    {
        /// <summary>
        /// Set to true if <see cref="Dispose"/> is called
        /// </summary>
        private bool disposed = false;
        /// <summary>
        /// Holds cache entries
        /// </summary>
        private readonly Dictionary<string, YtCacheEntry> entries;
        /// <summary>
        /// Provides logging facility
        /// </summary>
        private readonly ILogger _logger;
        /// <summary>
        /// Runs regular cleanup routine
        /// </summary>
        private readonly Thread tCleanup;
        /// <summary>
        /// Gets or sets the interval at which cleanup is performed.
        /// Default is 60 seconds
        /// </summary>
        /// <remarks>
        /// Regardless of this value, cleanup will never happen more often than every second.
        /// </remarks>
        private readonly TimeSpan cleanupInverval;

        /// <summary>
        /// Initializer that starts the cleanup thread
        /// </summary>
        public YtCacheService(ILogger<YtCacheService> logger)
        {
            _logger = logger;
            entries = new Dictionary<string, YtCacheEntry>();
            cleanupInverval = TimeSpan.FromSeconds(60.0);
            tCleanup = new Thread(Cleanup)
            {
                IsBackground = true,
                Name = $"{nameof(YtCacheService)}.{nameof(Cleanup)}()"
            };
            tCleanup.Start();
        }

        /// <summary>
        /// Removes expired entries
        /// </summary>
        private void Cleanup()
        {
            _logger.LogInformation("Cache cleanup routine started");
            while (true)
            {
                var SW = Stopwatch.StartNew();
                lock (entries)
                {
                    int removed = 0;
                    var keys = entries.Keys.OfType<string>().ToArray();
                    foreach (var K in keys)
                    {
                        if (entries[K].Expired)
                        {
                            entries.Remove(K);
                            ++removed;
                        }
                    }
                    if (removed > 0)
                    {
                        _logger.LogDebug("Removed {count} entries from cache", removed);
                    }
                }
                while (!disposed && SW.ElapsedTicks < cleanupInverval.Ticks)
                {
                    Thread.Sleep(500);
                }
            }
        }

        /// <summary>
        /// Gets the given entry
        /// </summary>
        /// <param name="key">Key</param>
        /// <returns>Returns null if not found or expired</returns>
        public object Get(string key)
        {
            YtCacheEntry e;
            lock (entries)
            {
                if (!entries.TryGetValue(key, out e))
                {
                    return null;
                }
            }
            return e.Expired ? null : e.Value;
        }

        /// <summary>
        /// Gets the given entry
        /// </summary>
        /// <param name="key">Key of entry</param>
        /// <param name="exists">
        /// boolean that receives whether the key existed or not, and if it's not expired.
        /// This allows the user to know whether a "null" return is because the object is actually null or not.
        /// </param>
        /// <returns>Object, or null if not found or expired</returns>
        public object Get(string key, out bool exists)
        {
            YtCacheEntry e;
            lock (entries)
            {
                exists = entries.TryGetValue(key, out e);
                if (!exists)
                {
                    return null;
                }
            }
            exists = !e.Expired;
            return e.Expired ? null : e.Value;
        }

        /// <summary>
        /// Gets all keys from the cache
        /// </summary>
        /// <returns>keys</returns>
        public string[] GetKeys()
        {
            lock (entries)
            {
                return entries.Keys.OfType<string>().ToArray();
            }
        }

        /// <summary>
        /// Sets a value in the cache. Overwrites existing entries.
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="data">Data</param>
        /// <param name="lifetime">Entry lifetime. Must be more than zero</param>
        public void Set(string key, object data, TimeSpan lifetime)
        {
            if (lifetime > TimeSpan.Zero)
            {
                lock (entries)
                {
                    entries[key] = new YtCacheEntry(data, lifetime);
                }
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(lifetime));
            }
        }

        /// <summary>
        /// Updates the expiration timestamp of an entry
        /// </summary>
        /// <param name="key">Entry key</param>
        /// <param name="lifetime">New liefetime</param>
        /// <returns>true, if entry found and updated</returns>
        /// <remarks>
        /// This will update the timestamp of expired entries if they've not yet been removed.
        /// You can set the timestamp to zero to have the entry removed on the next cleanup.
        /// </remarks>
        public bool Poke(string key, TimeSpan lifetime)
        {
            if (lifetime >= TimeSpan.Zero)
            {
                lock (entries)
                {
                    if (entries.TryGetValue(key, out YtCacheEntry e))
                    {
                        e.Poke(lifetime);
                        return true;
                    }
                }
                return false;
            }
            throw new ArgumentOutOfRangeException(nameof(lifetime));
        }

        /// <summary>
        /// Deletes an entry from the cache
        /// </summary>
        /// <param name="key">Cache</param>
        /// <returns>true, if entry was removed. False if not found</returns>
        public bool Remove(string key)
        {
            lock (entries)
            {
                return entries.Remove(key);
            }
        }

        /// <summary>
        /// Clears all entries
        /// </summary>
        /// <returns>Number of entries removed</returns>
        public int Clear()
        {
            lock (entries)
            {
                var i = entries.Count;
                entries.Clear();
                _logger.LogInformation("Cache cleared. Removed {count} entries", i);
                return i;
            }
        }

        public void Dispose()
        {
            disposed = true;
        }

        /// <summary>
        /// Represents an entry in the cache
        /// </summary>
        private class YtCacheEntry
        {
            public DateTime Expires { get; private set; }
            public object Value { get; set; }
            public bool Expired => DateTime.UtcNow > Expires;

            public YtCacheEntry(object data, TimeSpan lifetime)
            {
                Value = data;
                Poke(lifetime);
            }

            public DateTime Poke(TimeSpan duration)
            {
                if (duration >= TimeSpan.Zero)
                {
                    return Expires = DateTime.UtcNow.Add(duration);
                }
                return Expires;
            }
        }
    }
}
