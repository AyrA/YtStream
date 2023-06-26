using System;
using YtStream.Models.Api;
using YtStream.Models.YT;

namespace YtStream.Models
{
    public class PlayerViewModel
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public string Owner { get; set; }

        public Guid? StreamKey { get; set; }

        public InfoModel[] Videos { get; set; }

        public PlayerViewModel(YtSnippetModel playlistInfo, InfoModel[] videos)
        {
            if (playlistInfo == null)
            {
                throw new System.ArgumentNullException(nameof(playlistInfo));
            }
            Videos = videos ?? throw new System.ArgumentNullException(nameof(videos));
            Name = playlistInfo.Title ?? "<unnamed playlist>";
            Description = playlistInfo.Description ?? "<no description>";
            Owner = playlistInfo.ChannelTitle ?? "<no owner information>";
        }
    }
}
