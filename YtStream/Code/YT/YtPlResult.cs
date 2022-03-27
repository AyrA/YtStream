using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace YtStream.YT
{
    public class YtPlResult
    {
        public string NextPageToken { get; set; }
        public YtPlItem[] Items { get; set; }

        public YtPageInfo PageInfo { get; set; }
    }

    public class YtPageInfo
    {
        public int TotalResults { get; set; }

        public int ResultsPerPage { get; set; }
    }

    public class YtPlItem
    {
        public YtPlSnippet Snippet { get; set; }
    }

    public class YtPlSnippet
    {
        public DateTime PublishedAt { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public Dictionary<string, YtThumbnail> Thumbnails { get; set; }

        public int Position { get; set; }

        public YtPlVideo ResourceId { get; set; }

        public string VideoOwnerChannelTitle { get; set; }

        public string VideoOwnerChannelId { get; set; }
    }

    public class YtPlVideo
    {
        public string VideoId { get; set; }
    }

    public class YtThumbnail
    {
        public string Url { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
}
