namespace YtStream.Enums
{
    /// <summary>
    /// Audio channels for MP3 files
    /// </summary>
    /// <remarks>"Stereo" in this case means "Joint Stereo" in MP3 terms</remarks>
    public enum Mp3ChannelEnum : int
    {
        Mono = 1,
        Stereo = 2
    }
}
