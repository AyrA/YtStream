using System;
using System.Linq;
using System.Net;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace YtStream
{
    /// <summary>
    /// Provides limited access to the SponsorBlock API
    /// </summary>
    public static class SponsorBlock
    {
        /// <summary>
        /// Host with official API
        /// </summary>
        public const string DefaultHost = "sponsor.ajay.app";
        /// <summary>
        /// Type of range we want to retrieve
        /// </summary>
        private const string Category = "music_offtopic";

        /// <summary>
        /// SBlock host
        /// </summary>
        public static string ApiHost = DefaultHost;

        /// <summary>
        /// Gets blockable ranges for the given youtube video id
        /// </summary>
        /// <param name="Id">Video id</param>
        /// <returns>
        /// List of ranges.
        /// null on severe errors.
        /// </returns>
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

        /// <summary>
        /// Represents API response of SponsorBlock
        /// </summary>
        private class SponsorBlockResult
        {
            /// <summary>
            /// Category of the range
            /// </summary>
            [JsonPropertyName("category")]
            public string Category { get; set; }
            
            /// <summary>
            /// Type of action
            /// </summary>
            [JsonPropertyName("actionType")]
            public string ActionType { get; set; }
            
            /// <summary>
            /// Start and End timestamp
            /// </summary>
            [JsonPropertyName("segment")]
            public double[] Segment { get; set; }
            /// <summary>
            /// Randomly generated id of this segment
            /// </summary>
            public string UUID { get; set; }
            
            /// <summary>
            /// Votes for this range
            /// </summary>
            [JsonPropertyName("votes")]
            public int Votes { get; set; }
            
            /// <summary>
            /// Id of user that created this range
            /// </summary>
            [JsonPropertyName("userID")]
            public string UserID { get; set; }
            
            /// <summary>
            /// Range description
            /// </summary>
            /// <remarks>This seems to always be empty as of now</remarks>
            [JsonPropertyName("description")]
            public string Description { get; set; }
        }
    }
}
