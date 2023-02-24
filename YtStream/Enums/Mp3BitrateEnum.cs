namespace YtStream.Enums
{
    /// <summary>
    /// Known bitrates
    /// </summary>
    /// <remarks>
    /// Variable bitrate (VBR) doesn't exists.
    /// VBR simply is an MP3 file where every header potentially has a different bitrate.
    /// </remarks>
    public enum Mp3BitrateEnum : int
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
