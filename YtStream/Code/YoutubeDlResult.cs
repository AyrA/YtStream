using System.Text.Json.Serialization;

namespace YtStream
{
    public class YoutubeDlResult
    {
        [JsonPropertyName("channel")]
        public string Channel { get; set; }

        [JsonPropertyName("artist")]
        public string Artist { get; set; }

        [JsonPropertyName("track")]
        public string Track { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("uploader_url")]
        public string ChannelUrl { get; set; }

        [JsonPropertyName("duration")]
        public double Duration { get; set; }

        [JsonPropertyName("asr")]
        public double SampleRate { get; set; }

        [JsonPropertyName("abr")]
        public double Bitrate { get; set; }

        [JsonPropertyName("filesize")]
        public int FileSize { get; set; }

        [JsonPropertyName("thumbnails")]
        public YoutubeThumbnail[] Thumbnails { get; set; }

        [JsonPropertyName("thumbnail")]
        public string Thumbnail { get; set; }

        [JsonPropertyName("tags")]
        public string[] Tags { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }
    }

    public class YoutubeThumbnail
    {
        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonIgnore]
        public int Area { get => Width * Height; }

        [JsonPropertyName("url")]
        public string Url { get; set; }
    }
}
