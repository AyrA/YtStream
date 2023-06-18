namespace YtStream.Models.YT
{
    public class YtBaseItemModel
    {
        public string? Kind { get; set; }
        public string? ETag { get; set; }
        public string? Id { get; set; }
        public YtSnippetModel? Snippet { get; set; }
    }
}
