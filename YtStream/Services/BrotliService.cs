using AyrA.AutoDI;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace YtStream.Services
{
    /// <summary>
    /// Provides Brotli compression and decompression routines
    /// </summary>
    [AutoDIRegister(AutoDIType.Transient)]
    public class BrotliService
    {
        /// <summary>
        /// Compress bytes
        /// </summary>
        /// <param name="Source">Data</param>
        /// <returns>Compressed data</returns>
        public async Task<byte[]> Compress(byte[] Source)
        {
            using var MS = new MemoryStream(Source, false);
            return await Compress(MS);
        }

        /// <summary>
        /// Compress a stream
        /// </summary>
        /// <param name="Source">Stream</param>
        /// <returns>Compressed data</returns>
        public async Task<byte[]> Compress(Stream Source)
        {
            using var MS = new MemoryStream();
            using var BR = new BrotliStream(MS, CompressionLevel.Optimal);
            await Source.CopyToAsync(BR);
            await BR.FlushAsync();
            return MS.ToArray();
        }

        /// <summary>
        /// Decompresses bytes
        /// </summary>
        /// <param name="Source">Compressed data</param>
        /// <returns>Decompressed data</returns>
        public async Task<byte[]> Decompress(byte[] Source)
        {
            using var MS = new MemoryStream(Source, false);
            return await Decompress(MS);
        }

        /// <summary>
        /// Decompresses a stream
        /// </summary>
        /// <param name="Source">Compressed stream</param>
        /// <returns>Decompressed data</returns>
        public async Task<byte[]> Decompress(Stream Source)
        {
            using var MS = new MemoryStream();
            using var BR = new BrotliStream(Source, CompressionMode.Decompress);
            await BR.CopyToAsync(MS);
            return MS.ToArray();
        }
    }
}
