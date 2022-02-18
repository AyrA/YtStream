using System;
using System.IO;

namespace YtStream
{
    /// <summary>
    /// Caching utility
    /// </summary>
    public static class Cache
    {
        /// <summary>
        /// Gets the base cache directory
        /// </summary>
        /// <remarks>Set using <see cref="SetBaseDirectory(string)"/></remarks>
        public static string BaseDirectory { get; private set; }

        /// <summary>
        /// Set new cache directory
        /// </summary>
        /// <param name="Dir">Directory</param>
        public static void SetBaseDirectory(string Dir)
        {
            if (string.IsNullOrEmpty(Dir))
            {
                throw new ArgumentNullException(nameof(Dir));
            }
            Directory.CreateDirectory(Dir);
            BaseDirectory = Dir;
        }

        /// <summary>
        /// Gets a cache handler
        /// </summary>
        /// <param name="Type">Cache type</param>
        /// <param name="DefaultCacheLifetimeSeconds">
        /// Maximum object lifetime before they become stale.
        /// Use zero for infinite caching
        /// </param>
        /// <returns>Cache handler</returns>
        public static CacheHandler GetHandler(CacheType Type, double DefaultCacheLifetimeSeconds)
        {
            return GetHandler(Type, TimeSpan.FromSeconds(DefaultCacheLifetimeSeconds));
        }

        /// <summary>
        /// Gets a cache handler
        /// </summary>
        /// <param name="Type">Cache type</param>
        /// <param name="DefaultCacheLifetime">
        /// Maximum object lifetime before they become stale.
        /// Use <see cref="TimeSpan.Zero"/> for infinite caching
        /// </param>
        /// <returns>Cache handler</returns>
        public static CacheHandler GetHandler(CacheType Type, TimeSpan DefaultCacheLifetime)
        {
            if (Enum.IsDefined(typeof(CacheType), Type))
            {
                return new CacheHandler(Path.Combine(BaseDirectory, Type.ToString()), DefaultCacheLifetime);
            }
            throw new ArgumentException($"Undefined enumeration: {Type}");
        }

        /// <summary>
        /// Cache types
        /// </summary>
        public enum CacheType
        {
            /// <summary>
            /// MP3 file cache from youtube
            /// </summary>
            MP3,
            /// <summary>
            /// SBlock range cache
            /// </summary>
            SponsorBlock,
            /// <summary>
            /// Audio segments that are inserted in between other audio segments (ads)
            /// </summary>
            AudioSegments
        }
    }
}
