namespace YtStream.Enums
{
    /// <summary>
    /// Cache types
    /// </summary>
    public enum CacheTypeEnum
    {
        /// <summary>
        /// MP3 file cache from youtube
        /// </summary>
        MP3,
        /// <summary>
        /// SBlock range cache
        /// </summary>
        SponsorBlock,
        /// <summary>
        /// Audio segments that are inserted in between other audio segments (ads)
        /// </summary>
        AudioSegments
    }
}
