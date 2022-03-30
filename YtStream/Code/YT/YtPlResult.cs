using System;
using System.Collections.Generic;

namespace YtStream.YT
{
    public class YtResult
    {
        public string NextPageToken { get; set; }
        public YtBaseItem[] Items { get; set; }

        public YtPageInfo PageInfo { get; set; }
    }

    public class YtPageInfo
    {
        public int TotalResults { get; set; }

        public int ResultsPerPage { get; set; }
    }

    public class YtBaseItem
    {
        public string Kind { get; set; }
        public string ETag { get; set; }
        public string Id { get; set; }
        public YtSnippet Snippet { get; set; }
    }

    public class YtSnippet
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
