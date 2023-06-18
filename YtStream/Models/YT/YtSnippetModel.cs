using System;
using System.Collections.Generic;
using System.Linq;

namespace YtStream.Models.YT
{
    public class YtSnippetModel
    {
        public DateTime PublishedAt { get; set; }

        public string? Id { get; set; }

        public string? Title { get; set; }

        public string? Description { get; set; }

        public Dictionary<string, YtThumbnailModel>? Thumbnails { get; set; }

        public int Position { get; set; }

        public YtPlVideoModel? ResourceId { get; set; }

        public string? VideoOwnerChannelTitle { get; set; }

        public string? VideoOwnerChannelId { get; set; }

        /// <summary>
        /// Get the smallest thumbnail that has at least the specified dimensions if possible,
        /// otherwise returns the biggest thumbnail
        /// </summary>
        /// <param name="MinWidth">Minimum requested width</param>
        /// <param name="MinHeight">Minimum requested height</param>
        /// <returns>Thumbnail with the given dimensions. If none found, biggest thumbnail</returns>
        /// <remarks>Returns null if <see cref="Thumbnails"/> is empty</remarks>
        public YtThumbnailModel? GetThumbnailBySize(int MinWidth, int MinHeight)
        {
            //Early abort if no thumbnail exists
            if (Thumbnails == null || Thumbnails.Count == 0)
            {
                return null;
            }
            //Get thumbnails by size restriction, and sort by minimum image area
            var thumbs = Thumbnails
                .Select(m => m.Value)
                .Where(m => m.Width >= MinWidth && m.Height >= MinHeight)
                .OrderBy(m => m.Width * m.Height)
                .ToArray();
            //No thumb matches the limitations, return the largest thumbnail
            if (thumbs.Length == 0)
            {
                return Thumbnails.Select(m => m.Value).MaxBy(m => m.Width * m.Height);
            }
            return thumbs[0];
        }

        /// <summary>
        /// Converts this instance into an API model
        /// </summary>
        /// <returns>API model</returns>
        public Api.InfoModel ToApiModel()
        {
            return new Api.InfoModel()
            {
                Title = Title,
                Id = Id ?? ResourceId?.VideoId,
                Thumbnail = (Thumbnails?.ContainsKey("medium") ?? false) ? Thumbnails["medium"]?.Url : null
            };
        }
    }
}
