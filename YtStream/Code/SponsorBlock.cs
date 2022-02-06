using System;
using System.Linq;
using System.Net;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace YtStream
{
    public static class SponsorBlock
    {
        public const string DefaultHost = "sponsor.ajay.app";
        private const string Category = "music_offtopic";

        public static string ApiHost = DefaultHost;

        public async static Task<TimeRange[]> GetRangesAsync(string Id)
        {
            if (!Tools.IsYoutubeId(Id))
            {
                throw new ArgumentException("Invalid youtube id");
            }
            Uri Addr = new Uri($"https://{ApiHost}/api/skipSegments?videoID={Id}&category={Category}");
            var Req = WebRequest.CreateHttp(Addr);
            using(var Res = await Tools.GetResponseAsync(Req))
            {
                if (Res.StatusCode == HttpStatusCode.OK)
                {
                    var Json = await Tools.ReadStringAsync(Res.GetResponseStream());
                    var Result = Json.FromJson<SponsorBlockResult[]>(true);
                    //Invalid response
                    if (Result == null)
                    {
                        return null;
                    }
                    return Result
                        .Where(m => m.Category == Category && m.Segment != null && m.Segment.Length == 2)
                        .Select(m => new TimeRange(m.Segment[0], m.Segment[1]))
                        .ToArray();
                }
                //Not found
                if (Res.StatusCode == HttpStatusCode.NotFound)
                {
                    return new TimeRange[0];
                }
            }
            //Invalid response
            return null;
        }

        private class SponsorBlockResult
        {
            [JsonPropertyName("category")]
            public string Category { get; set; }
            
            [JsonPropertyName("actionType")]
            public string ActionType { get; set; }
            
            [JsonPropertyName("segment")]
            public double[] Segment { get; set; }
            public string UUID { get; set; }
            
            [JsonPropertyName("votes")]
            public int Votes { get; set; }
            
            [JsonPropertyName("userID")]
            public string UserID { get; set; }
            
            [JsonPropertyName("description")]
            public string Description { get; set; }
        }
    }
}
