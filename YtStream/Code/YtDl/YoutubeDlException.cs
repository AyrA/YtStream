using System;

namespace YtStream.YtDl
{
    public class YoutubeDlException : Exception
    {
        public YoutubeDlException(string Msg) : base(Msg) { }
    }
}
