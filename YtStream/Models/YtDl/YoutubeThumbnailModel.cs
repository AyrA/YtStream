using System.Text.Json.Serialization;

namespace YtStream.Models.YtDl
{

    /// <summary>
    /// Represents a thumbnail
    /// </summary>
    public class YoutubeThumbnailModel
    {
        /// <summary>
        /// Width in pixels
        /// </summary>
        [JsonPropertyName("width")]
        public int Width { get; set; }

        /// <summary>
        /// Height in pixels
        /// </summary>
        [JsonPropertyName("height")]
        public int Height { get; set; }

        /// <summary>
        /// Area (<see cref="Width"/>*<see cref="Height"/>)
        /// </summary>
        [JsonIgnore]
        public int Area { get => Width * Height; }

        /// <summary>
        /// Image URL
        /// </summary>
        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }
}
