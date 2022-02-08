using System;

namespace YtStream.MP3
{
    public class MP3Header
    {
        private static readonly int[] AudioRates = new int[]
        {
            0, 32, 40, 48, 56, 64, 80, 96,
            112, 128, 160, 192, 224, 256, 320, 0
        };

        private static readonly int[] AudioFrequencies = new int[]
        {
            44100, 48000, 32000, 0
        };

        private const int SamplesPerFrame = 1152;

        private readonly int numBytes;
        private readonly double length;

        public bool HasPadding { get; }
        public bool IsProtected { get; }
        public Frequency AudioFrequency { get; }
        public Bitrate AudioRate { get; }

        public int NumberOfBytes => numBytes;

        public double AudioLengthMS => length;

        public MP3Header(byte[] Data)
        {
            if (!IsHeader(Data))
            {
                throw new ArgumentException("Data failed header check");
            }

            IsProtected = (Data[1] & 1) == 0;
            AudioRate = (Bitrate)AudioRates[(Data[2] & 0b11110000) >> 4];
            AudioFrequency = (Frequency)AudioFrequencies[(Data[2] & 0b1100) >> 2];
            HasPadding = (Data[2] & 0b10) == 0b10;
            if (!Enum.IsDefined(typeof(Bitrate), AudioRate))
            {
                throw new ArgumentException("Header has invalid bitrate");
            }
            if (!Enum.IsDefined(typeof(Frequency), AudioFrequency))
            {
                throw new ArgumentException("Header has invalid frequency");
            }

            //Create doubles to avoid casting a lot
            var srate = 1000.0 * (int)AudioRate;
            var sfrq = (double)(int)AudioFrequency;

            //This always rounds down. The rounding error is corrected over time by using the padding
            numBytes = (int)(SamplesPerFrame * srate / sfrq / 8.0 + (HasPadding ? 1.0 : 0.0) - 4.0 + (IsProtected ? 2.0 : 0.0));
            //Audio length does not depend on sample rate
            length = 1000.0 * SamplesPerFrame / sfrq;
        }

        /// <summary>
        /// Checks if the given header is potentially valid mpeg 1 layer 3
        /// </summary>
        /// <param name="B"></param>
        /// <returns></returns>
        public static bool IsHeader(byte[] B)
        {
            return B != null && B.Length == 4 && B[0] == 0xFF && (B[1] == 0xFA || B[1] == 0xFB);
        }

    }

    public enum Frequency : int
    {
        Hz32000 = 32000,
        Hz44100 = 44100,
        Hz48000 = 48000
    }

    public enum Bitrate : int
    {
        kbps32 = 32,
        kbps40 = 40,
        kbps48 = 48,
        kbps56 = 56,
        kbps64 = 64,
        kbps80 = 80,
        kbps96 = 96,
        kbps112 = 112,
        kbps128 = 128,
        kbps160 = 160,
        kbps192 = 192,
        kbps224 = 224,
        kbps256 = 256,
        kbps320 = 320
    }
}
