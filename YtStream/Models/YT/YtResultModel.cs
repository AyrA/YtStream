namespace YtStream.Models.YT
{
    public class YtResultModel
    {
        public string NextPageToken { get; set; }
        public YtBaseItemModel[] Items { get; set; }

        public YtPageInfoModel PageInfo { get; set; }
    }
}
