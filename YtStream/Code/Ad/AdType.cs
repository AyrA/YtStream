﻿using System;

namespace YtStream.Ad
{
    /// <summary>
    /// Represents Ad types
    /// </summary>
    [Flags]
    public enum AdType
    {
        /// <summary>
        /// Default value, cannot be used by the user as function arguments
        /// </summary>
        /// <remarks>This is in use by <see cref="AdFileInfo"/> only</remarks>
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
