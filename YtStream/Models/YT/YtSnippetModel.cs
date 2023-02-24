using System;
using System.Collections.Generic;

namespace YtStream.Models.YT
{
    public class YtSnippetModel
    {
        public DateTime PublishedAt { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public Dictionary<string, YtThumbnailModel> Thumbnails { get; set; }

        public int Position { get; set; }

        public YtPlVideoModel ResourceId { get; set; }

        public string VideoOwnerChannelTitle { get; set; }

        public string VideoOwnerChannelId { get; set; }
    }
}
