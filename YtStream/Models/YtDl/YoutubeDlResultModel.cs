﻿using System.Text.Json.Serialization;

namespace YtStream.Models.YtDl
{
    /// <summary>
    /// Represents the JSON result of youtube dl
    /// </summary>
    /// <remarks>This is not necessarily complete</remarks>
    public class YoutubeDlResultModel
    {
        /// <summary>
        /// Channel display name
        /// </summary>
        [JsonPropertyName("channel")]
        public string? Channel { get; set; }

        /// <summary>
        /// Artist name
        /// </summary>
        [JsonPropertyName("artist")]
        public string? Artist { get; set; }

        /// <summary>
        /// Song name
        /// </summary>
        [JsonPropertyName("track")]
        public string? Track { get; set; }

        /// <summary>
        /// Video title
        /// </summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        /// <summary>
        /// Video desctiption
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// Channel URL
        /// </summary>
        [JsonPropertyName("uploader_url")]
        public string? ChannelUrl { get; set; }

        /// <summary>
        /// Audio duration in seconds
        /// </summary>
        [JsonPropertyName("duration")]
        public double Duration { get; set; }

        /// <summary>
        /// Audio sample rate
        /// </summary>
        [JsonPropertyName("asr")]
        public double SampleRate { get; set; }

        /// <summary>
        /// Audio bitrate
        /// </summary>
        [JsonPropertyName("abr")]
        public double Bitrate { get; set; }

        /// <summary>
        /// Audio file size
        /// </summary>
        [JsonPropertyName("filesize")]
        public int FileSize { get; set; }

        /// <summary>
        /// Video thumbnails
        /// </summary>
        [JsonPropertyName("thumbnails")]
        public YoutubeThumbnailModel[]? Thumbnails { get; set; }

        /// <summary>
        /// Automatically selected thumbnail URL
        /// </summary>
        /// <remarks>This is likely the .webp image</remarks>
        [JsonPropertyName("thumbnail")]
        public string? Thumbnail { get; set; }

        /// <summary>
        /// Video tags
        /// </summary>
        [JsonPropertyName("tags")]
        public string[]? Tags { get; set; }

        /// <summary>
        /// Audio stream Url
        /// </summary>
        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }
}
