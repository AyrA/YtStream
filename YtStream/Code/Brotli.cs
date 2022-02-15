using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace YtStream
{
    public static class Brotli
    {
        public static async Task<byte[]> Compress(byte[] Source)
        {
            using (var MS = new MemoryStream(Source, false))
            {
                return await Compress(MS);
            }
        }
        public static async Task<byte[]> Compress(Stream Source)
        {
            using (var MS = new MemoryStream())
            {
                using (var BR = new BrotliStream(MS, CompressionLevel.Optimal))
                {
                    await Source.CopyToAsync(BR);
                    await BR.FlushAsync();
                    return MS.ToArray();
                }
            }
        }
        public static async Task<byte[]> Decompress(byte[] Source)
        {
            using (var MS = new MemoryStream(Source, false))
            {
                return await Decompress(MS);
            }
        }
        public static async Task<byte[]> Decompress(Stream Source)
        {
            using (var MS = new MemoryStream())
            {
                using (var BR = new BrotliStream(Source, CompressionMode.Decompress))
                {
                    await BR.CopyToAsync(MS);
                }
                return MS.ToArray();
            }
        }
    }
}
