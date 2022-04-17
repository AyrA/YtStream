using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace YtStream.YT
{
    public class YtApi
    {
        private const string ApiBase = "https://youtube.googleapis.com/youtube/v3/";

        private readonly string apiKey;

        public YtApi(string ApiKey)
        {
            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                throw new ArgumentException($"'{nameof(ApiKey)}' cannot be null or whitespace.", nameof(ApiKey));
            }
            apiKey = ApiKey;
        }

        public async Task<YtSnippet[]> GetPlaylistInfoAsync(string PlaylistId, int MaxItems = int.MaxValue)
        {
            if (!Tools.IsYoutubePlaylist(PlaylistId))
            {
                throw new ArgumentException("Playlist id is invalid");
            }
            var Ret = new List<YtSnippet>();
            if (MaxItems <= 0)
            {
                return Ret.ToArray();
            }
            var URL = BuildUrl("playlistItems", new
            {
                part = "snippet",
                maxResults = 50,
                playlistId = PlaylistId
            });
            YtResult Result;
            do
            {
                try
                {
                    Result = (await GetJson(URL)).FromJson<YtResult>(true, true);
                }
                catch
                {
                    return null;
                }
                if (Result == null || Result.Items == null)
                {
                    return null;
                }
                Ret.AddRange(Result.Items.Select(m => m.Snippet));
                URL = BuildUrl("playlistItems", new
                {
                    part = "snippet",
                    maxResults = 50,
                    playlistId = PlaylistId,
                    pageToken = Result.NextPageToken
                });
            } while (Ret.Count < MaxItems && !string.IsNullOrEmpty(Result.NextPageToken));
            return Ret.Take(MaxItems).ToArray();
        }

        public async Task<YtSnippet> GetVideoInfo(string VideoId)
        {
            if (!Tools.IsYoutubeId(VideoId))
            {
                throw new ArgumentException("Video id is invalid");
            }
            var URL = BuildUrl("videos", new
            {
                part = "snippet",
                maxResults = 1,
                id = VideoId
            });
            try
            {
                return (await GetJson(URL)).FromJson<YtResult>(true, true).Items[0].Snippet;
            }
            catch
            {
                return null;
            }
        }

        private Uri BuildUrl(string Function, object UrlParams)
        {
            var Base = ApiBase + Function + "?key=" + Esc(apiKey);
            string[] Encoded = null;
            if (UrlParams != null)
            {
                Encoded = UrlParams
                    .GetType()
                    .GetProperties()
                    .Select(m => Esc(m.Name) + "=" + Esc(m.GetValue(UrlParams)))
                    .ToArray();
            }
            if (Encoded != null && Encoded.Length > 0)
            {
                return new Uri(Base + "&" + string.Join("&", Encoded));
            }
            return new Uri(Base);
        }

        private static async Task<string> GetJson(Uri Url)
        {
            var Req = WebRequest.CreateHttp(Url);
            Req.Accept = "application/json";
            Req.UserAgent = "YtStream +https://github.com/AyrA/YtStream";
            Req.AutomaticDecompression = DecompressionMethods.All;
            try
            {
                using (var Res = (HttpWebResponse)await Req.GetResponseAsync())
                {
                    using (var Body = Res.GetResponseStream())
                    {
                        var Result = await Tools.ReadStringAsync(Body);
                        return Result;
                    }
                }

            }
            catch (WebException ex)
            {
                using (var Res = ex.Response)
                {
                    using (var Body = Res.GetResponseStream())
                    {
                        var Result = await Tools.ReadStringAsync(Body);
                        ILogger Logger = Startup.GetLogger<YtApi>();
                        Logger.LogWarning(Result);
                        return Result;
                    }
                }
            }
        }

        private static string Esc(object o)
        {
            if (o == null)
            {
                return string.Empty;
            }
            return Uri.EscapeDataString(o.ToString());
        }
    }
}
