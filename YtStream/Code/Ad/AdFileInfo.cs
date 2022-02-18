
using System;

namespace YtStream.Ad
{

    public class AdFileInfo
    {
        public string Filename { get; private set; }
        public AdType Type { get; private set; }

        public AdFileInfo(string filename, AdType type)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                throw new ArgumentException($"'{nameof(filename)}' cannot be null or whitespace.", nameof(filename));
            }

            if (!Tools.CheckEnumFlags(type))
            {
                throw new ArgumentException($"Unsupported {nameof(AdType)} value: {type}");
            }
            Filename = filename;
            Type = type;
        }
    }
}
