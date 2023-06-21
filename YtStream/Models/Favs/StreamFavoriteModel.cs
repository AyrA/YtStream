using System.Linq;

namespace YtStream.Models.Favs
{
    public class StreamFavoriteModel : FavoriteBaseModel
    {
        public string[]? Ids { get; set; }

        public override string[] GetValidationMessages()
        {
            var msgs = base.GetValidationMessages().ToList();

            if (Ids == null || Ids.Length == 0 || !Ids.All(m => Tools.IsYoutubeId(m) || Tools.IsYoutubePlaylist(m)))
            {
                msgs.Add("Invalid id list");
            }

            return msgs.ToArray();
        }

        public override bool IsValid()
        {
            return base.IsValid() &&
                Ids != null && Ids.Length > 0 &&
                Ids.All(m => Tools.IsYoutubeId(m) || Tools.IsYoutubePlaylist(m));
        }
    }
}
