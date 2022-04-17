using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace YtStream.YT
{
    /// <summary>
    /// Caches responses from the youtube API
    /// </summary>
    public static class YtCache
    {
        /// <summary>
        /// Holds cache entries
        /// </summary>
        private static readonly Dictionary<string, YtCacheEntry> Entries;
        /// <summary>
        /// Provides logging facility
        /// </summary>
        private static readonly ILogger Logger;
        /// <summary>
        /// Runs regular cleanup routine
        /// </summary>
        private static readonly Thread T;

        /// <summary>
        /// Gets or sets the interval at which cleanup is performed.
        /// Default is 60 seconds
        /// </summary>
        /// <remarks>
        /// Regardless of this value, cleanup will never happen more often than every second.
        /// </remarks>
        public static TimeSpan CleanupInverval { get; set; }
        
        /// <summary>
        /// Gets the number of entries in the cache
        /// </summary>
        public static int CacheSize { get => Entries.Count; }

        /// <summary>
        /// Static initializer that starts the cleanup thread
        /// </summary>
        static YtCache()
        {
            Logger = Startup.GetLogger<YtApi>();
            Entries = new Dictionary<string, YtCacheEntry>();
            CleanupInverval = TimeSpan.FromSeconds(60.0);
            T = new Thread(Cleanup)
            {
                IsBackground = true,
                Name = $"{nameof(YtCache)}.{nameof(Cleanup)}()"
            };
            T.Start();
        }

        /// <summary>
        /// Removes expired entries
        /// </summary>
        private static void Cleanup()
        {
            var SW = Stopwatch.StartNew();
            Logger.LogInformation("Cache cleanup routine started");
            while (true)
            {
                lock (Entries)
                {
                    int removed = 0;
                    var Keys = Entries.Keys.OfType<string>().ToArray();
                    foreach (var K in Keys)
                    {
                        if (Entries[K].Expired)
                        {
                            Entries.Remove(K);
                            ++removed;
                        }
                    }
                    if (removed > 0)
                    {
                        Logger.LogDebug("Removed {0} entries from cache", removed);
                    }
                }
                while (SW.ElapsedTicks < CleanupInverval.Ticks || SW.ElapsedMilliseconds < 1000)
                {
                    Thread.Sleep(1000);
                }
                SW.Restart();
            }
        }

        /// <summary>
        /// Gets the given entry
        /// </summary>
        /// <param name="Key">Key</param>
        /// <returns>Returns null if not found or expired</returns>
        public static object Get(string Key)
        {
            YtCacheEntry e;
            lock (Entries)
            {
                if (!Entries.TryGetValue(Key, out e))
                {
                    return null;
                }
            }
            return e.Expired ? null : e.Value;
        }

        public static object Get(string Key, out bool Exists)
        {
            YtCacheEntry e;
            lock (Entries)
            {
                Exists = Entries.TryGetValue(Key, out e);
                if (!Exists)
                {
                    return null;
                }
            }
            Exists = !e.Expired;
            return e.Expired ? null : e.Value;
        }

        /// <summary>
        /// Gets all keys from the cache
        /// </summary>
        /// <returns>keys</returns>
        public static string[] Keys()
        {
            lock (Entries)
            {
                return Entries.Keys.OfType<string>().ToArray();
            }
        }

        /// <summary>
        /// Sets a value in the cache. Overwrites existing entries.
        /// </summary>
        /// <param name="Key">Key</param>
        /// <param name="Data">Data</param>
        /// <param name="Lifetime">Entry lifetime. Must be more than zero</param>
        public static void Set(string Key, object Data, TimeSpan Lifetime)
        {
            if (Lifetime > TimeSpan.Zero)
            {
                lock (Entries)
                {
                    Entries[Key] = new YtCacheEntry(Data, Lifetime);
                }
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(Lifetime));
            }
        }

        /// <summary>
        /// Updates the expiration timestamp of an entry
        /// </summary>
        /// <param name="Key">Entry key</param>
        /// <param name="Lifetime">New liefetime</param>
        /// <returns>true, if entry found and updated</returns>
        /// <remarks>
        /// This will update the timestamp of expired entries if they've not yet been removed.
        /// You can set the timestamp to zero to have the entry removed on the next cleanup.
        /// </remarks>
        public static bool Poke(string Key, TimeSpan Lifetime)
        {
            if (Lifetime >= TimeSpan.Zero)
            {
                lock (Entries)
                {
                    if (Entries.TryGetValue(Key, out YtCacheEntry e))
                    {
                        e.Poke(Lifetime);
                        return true;
                    }
                }
                return false;
            }
            throw new ArgumentOutOfRangeException(nameof(Lifetime));
        }

        /// <summary>
        /// Deletes an entry from the cache
        /// </summary>
        /// <param name="Key">Cache</param>
        /// <returns>true, if entry was removed. False if not found</returns>
        public static bool Remove(string Key)
        {
            lock (Entries)
            {
                return Entries.Remove(Key);
            }
        }

        /// <summary>
        /// Clears all entries
        /// </summary>
        /// <returns>Number of entries removed</returns>
        public static int Clear()
        {
            lock (Entries)
            {
                var i = Entries.Count;
                Entries.Clear();
                Logger.LogInformation("Cache cleared");
                return i;
            }
        }

        /// <summary>
        /// Represents an entry in the cache
        /// </summary>
        private class YtCacheEntry
        {
            public DateTime Expires { get; private set; }
            public object Value { get; set; }
            public bool Expired
            {
                get
                {
                    return DateTime.UtcNow > Expires;
                }
            }

            public YtCacheEntry(object data, TimeSpan lifetime)
            {
                Value = data;
                Poke(lifetime);
            }

            public DateTime Poke(TimeSpan Duration)
            {
                if (Duration >= TimeSpan.Zero)
                {
                    return Expires = DateTime.UtcNow.Add(Duration);
                }
                return Expires;
            }
        }
    }
}
