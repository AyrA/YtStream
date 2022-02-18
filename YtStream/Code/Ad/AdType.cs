using System;

namespace YtStream.Ad
{
    [Flags]
    public enum AdType
    {
        /// <summary>
        /// Default value, cannot be used by the user as function arguments
        /// </summary>
        None = 0,
        /// <summary>
        /// All supported types
        /// </summary>
        All = Inter | Intro | Outro,
        /// <summary>
        /// Ad between two media streams
        /// </summary>
        Inter = 1,
        /// <summary>
        /// Ad before the first media stream
        /// </summary>
        Intro = Inter << 1,
        /// <summary>
        /// Ad after the last media stream
        /// </summary>
        Outro = Intro << 1
    }
}
