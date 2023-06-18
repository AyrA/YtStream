using AyrA.AutoDI;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using YtStream.Extensions;
using YtStream.Models;

namespace YtStream.Services
{
    [AutoDIRegister(AutoDIType.Transient)]
    /// <summary>
    /// Provides limited access to the SponsorBlock API
    /// </summary>
    public class SponsorBlockService
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
        public string? ApiHost { get; set; } = DefaultHost;

        public SponsorBlockService(ConfigService config)
        {
            ApiHost = config.GetConfiguration().SponsorBlockServer;
        }

        /// <summary>
        /// Gets blockable ranges for the given youtube video id
        /// </summary>
        /// <param name="Id">Video id</param>
        /// <returns>
        /// List of ranges.
        /// null on severe errors.
        /// </returns>
        public async Task<TimeRangeModel[]?> GetRangesAsync(string Id)
        {
            if (!Tools.IsYoutubeId(Id))
            {
                throw new ArgumentException("Invalid youtube id");
            }
            var Addr = new Uri($"https://{ApiHost}/api/skipSegments?videoID={Id}&category={Category}");
            var Req = new HttpClient();
            using (var Res = await Req.GetAsync(Addr))
            {
                if (Res.StatusCode == HttpStatusCode.OK)
                {
                    var Json = await Res.Content.ReadAsStringAsync();
                    var Result = Json.FromJson<SponsorBlockResult[]>(true);
                    //Invalid response
                    if (Result == null)
                    {
                        return null;
                    }

#nullable disable //Nullable is not smart enough for LINQ
                    return Result
                        .Where(m => m.Category == Category && m.Segment != null && m.Segment.Length == 2)
                        .Select(m => new TimeRangeModel(m.Segment[0], m.Segment[1]))
                        .ToArray();
#nullable restore
                }
                //Not found
                if (Res.StatusCode == HttpStatusCode.NotFound)
                {
                    return Array.Empty<TimeRangeModel>();
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
            public string? Category { get; set; }

            /// <summary>
            /// Type of action
            /// </summary>
            [JsonPropertyName("actionType")]
            public string? ActionType { get; set; }

            /// <summary>
            /// Start and End timestamp
            /// </summary>
            [JsonPropertyName("segment")]
            public double[]? Segment { get; set; }
            /// <summary>
            /// Randomly generated id of this segment
            /// </summary>
            public string? UUID { get; set; }

            /// <summary>
            /// Votes for this range
            /// </summary>
            [JsonPropertyName("votes")]
            public int Votes { get; set; }

            /// <summary>
            /// Id of user that created this range
            /// </summary>
            [JsonPropertyName("userID")]
            public string? UserID { get; set; }

            /// <summary>
            /// Range description
            /// </summary>
            /// <remarks>This seems to always be empty as of now</remarks>
            [JsonPropertyName("description")]
            public string? Description { get; set; }
        }
    }
}
