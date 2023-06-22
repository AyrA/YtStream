using System.Linq;

namespace YtStream.Models.Favs
{
    public class PlayerFavoriteModel : FavoriteBaseModel
    {
        public string? PlaylistId { get; set; }
        public bool Random { get; set; }

        public override string[] GetValidationMessages()
        {
            var msgs = base.GetValidationMessages().ToList();
            if (!Tools.IsYoutubePlaylist(PlaylistId))
            {
                msgs.Add("Playlist id is invalid");
            }
            return msgs.ToArray();
        }

        public override bool IsValid()
        {
            return base.IsValid() && Tools.IsYoutubePlaylist(PlaylistId);
        }
    }
}
