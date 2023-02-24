using System;
using YtStream.Enums;

namespace YtStream.Models.Mp3
{
    /// <summary>
    /// Partially decodes and represents an MP3 header
    /// </summary>
    /// <remarks>
    /// This only decodes as much as is needed for this application (just header, no audio).
    /// Supports only mpeg 1 layer 3
    /// </remarks>
    public class Mp3HeaderModel
    {
        /// <summary>
        /// Possible mpeg 1 layer 3 bitrates
        /// </summary>
        /// <remarks>Directly maps to bits in the header. Zero is invalid or unknown</remarks>
        private static readonly int[] AudioRates = new int[]
        {
            0, 32, 40, 48, 56, 64, 80, 96,
            112, 128, 160, 192, 224, 256, 320, 0
        };

        /// <summary>
        /// Possible mpeg 1 layer 3 frequencies
        /// </summary>
        /// <remarks>Directly maps to bits in the header. Zero is invalid</remarks>
        private static readonly int[] AudioFrequencies = new int[]
        {
            44100, 48000, 32000, 0
        };

        /// <summary>
        /// Number of samples per frame is constant for mpeg 1 layer 3
        /// </summary>
        private const int SamplesPerFrame = 1152;

        /// <summary>
        /// Backing field for <see cref="NumberOfBytes"/>
        /// </summary>
        private readonly int numBytes;
        /// <summary>
        /// Backing field for <see cref="AudioLengthMS"/>
        /// </summary>
        private readonly double length;

        /// <summary>
        /// Gets whether a padding sample is present or not
        /// </summary>
        /// <remarks>For mpeg 1 layer 3 this is exactly one byte</remarks>
        public bool HasPadding { get; }

        /// <summary>
        /// Gets whether a CRC16 is present directly after the header or not
        /// </summary>
        public bool IsProtected { get; }

        /// <summary>
        /// Gets the status of the private bit
        /// </summary>
        public bool Private { get; }

        /// <summary>
        /// Gets the frequency of the audio
        /// </summary>
        public Mp3FrequencyEnum AudioFrequency { get; }

        /// <summary>
        /// Gets the bitrate of the audio
        /// </summary>
        public Mp3BitrateEnum AudioRate { get; }

        /// <summary>
        /// Gets the number of data bytes that follow the header
        /// </summary>
        /// <remarks>This handles <see cref="HasPadding"/> and <see cref="IsProtected"/></remarks>
        public int NumberOfBytes => numBytes;

        /// <summary>
        /// Gets the computed length of audio data in milliseconds
        /// </summary>
        public double AudioLengthMS => length;

        /// <summary>
        /// Creates an instance from the given bytes
        /// </summary>
        /// <param name="Data">4 Header bytes</param>
        public Mp3HeaderModel(byte[] Data)
        {
            if (!IsHeader(Data))
            {
                throw new ArgumentException("Data failed header check");
            }

            IsProtected = (Data[1] & 1) == 0;
            Private = (Data[2] & 1) == 1;
            AudioRate = (Mp3BitrateEnum)AudioRates[(Data[2] & 0b11110000) >> 4];
            AudioFrequency = (Mp3FrequencyEnum)AudioFrequencies[(Data[2] & 0b1100) >> 2];
            HasPadding = (Data[2] & 0b10) == 0b10;
            if (!Enum.IsDefined(AudioRate))
            {
                throw new ArgumentException("Header has invalid bitrate");
            }
            if (!Enum.IsDefined(AudioFrequency))
            {
                throw new ArgumentException("Header has invalid frequency");
            }

            //Create doubles to avoid casting a lot
            var srate = 1000.0 * (int)AudioRate;
            var sfrq = (double)(int)AudioFrequency;

            //This always rounds down. The rounding error is corrected over time by using the padding
            numBytes = (int)((SamplesPerFrame * srate / sfrq / 8.0) + (HasPadding ? 1.0 : 0.0) - 4.0 + (IsProtected ? 2.0 : 0.0));
            //Audio length does not depend on sample rate
            length = 1000.0 * SamplesPerFrame / sfrq;
        }

        /// <summary>
        /// Checks if the given header is potentially valid mpeg 1 layer 3
        /// </summary>
        /// <param name="B">4 data bytes</param>
        /// <returns>true if header</returns>
        /// <remarks>
        /// This is not a full test.
        /// It will not catch headers with invalid bitrate or frequency
        /// </remarks>
        public static bool IsHeader(byte[] B)
        {
            return B != null && B.Length == 4 && B[0] == 0xFF && (B[1] == 0xFA || B[1] == 0xFB);
        }
    }
}
