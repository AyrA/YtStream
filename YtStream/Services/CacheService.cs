using AyrA.AutoDI;
using System;
using System.IO;
using YtStream.Enums;

namespace YtStream.Services
{
    [AutoDIRegister(AutoDIType.Transient)]
    /// <summary>
    /// Caching utility
    /// </summary>
    public partial class CacheService
    {
        /// <summary>
        /// Gets the base cache directory
        /// </summary>
        public string BaseDirectory { get; }

        public CacheService(ConfigService config)
        {
            BaseDirectory = config.GetConfiguration().CachePath;
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
        public CacheHandler GetHandler(CacheTypeEnum Type, double DefaultCacheLifetimeSeconds)
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
        public CacheHandler GetHandler(CacheTypeEnum Type, TimeSpan DefaultCacheLifetime)
        {
            if (Enum.IsDefined(Type))
            {
                return new CacheHandler(Path.Combine(BaseDirectory, Type.ToString()), DefaultCacheLifetime);
            }
            throw new ArgumentException($"Undefined enumeration: {Type}");
        }
    }
}
