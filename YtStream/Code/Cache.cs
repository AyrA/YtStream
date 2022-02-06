using System;
using System.IO;

namespace YtStream
{
    public static class Cache
    {
        public static string BaseDirectory { get; private set; }

        public static void SetBaseDirectory(string Dir)
        {
            if (string.IsNullOrEmpty(Dir))
            {
                throw new ArgumentNullException(nameof(Dir));
            }
            Directory.CreateDirectory(Dir);
            BaseDirectory = Dir;
        }

        public static CacheHandler GetHandler(CacheType Type, double DefaultCacheLifetimeSeconds)
        {
            return GetHandler(Type, TimeSpan.FromSeconds(DefaultCacheLifetimeSeconds));
        }

        public static CacheHandler GetHandler(CacheType Type, TimeSpan DefaultCacheLifetime)
        {
            if (Enum.IsDefined(typeof(CacheType), Type))
            {
                return new CacheHandler(Path.Combine(BaseDirectory, Type.ToString()), DefaultCacheLifetime);
            }
            throw new ArgumentException($"Undefined enumeration: {Type}");
        }

        public enum CacheType
        {
            MP3,
            SponsorBlock
        }
    }
}
