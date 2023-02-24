using System;
using YtStream.Enums;

namespace YtStream.Models.Ad
{
    /// <summary>
    /// Information about an ad
    /// </summary>
    public class AdFileInfoModel
    {
        /// <summary>
        /// File name
        /// </summary>
        public string Filename { get; private set; }
        /// <summary>
        /// Types this ad is registered for
        /// </summary>
        public AdTypeEnum Type { get; private set; }

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="filename">File name (as given from cache)</param>
        /// <param name="type">Registered types</param>
        public AdFileInfoModel(string filename, AdTypeEnum type)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                throw new ArgumentException($"'{nameof(filename)}' cannot be null or whitespace.", nameof(filename));
            }

            if (!Tools.CheckEnumFlags(type))
            {
                throw new ArgumentException($"Unsupported {nameof(AdTypeEnum)} value: {type}");
            }
            Filename = filename;
            Type = type;
        }
    }
}
