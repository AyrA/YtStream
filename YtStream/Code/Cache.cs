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
    }
}
