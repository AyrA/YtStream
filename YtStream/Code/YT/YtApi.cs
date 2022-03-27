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

        public async Task<YtPlItem[]> GetPlaylistInfoAsync(string PlaylistId)
        {
            if (!Tools.IsYoutubePlaylist(PlaylistId))
            {
                throw new ArgumentException("Playlist id is invalid");
            }
            var URL = BuildUrl("playlistItems", new
            {
                part = "snippet",
                maxResults = 20,
                playlistId = PlaylistId
            });
            var Ret = new List<YtPlItem>();
            YtPlResult Result;
            do
            {
                try
                {
                    Result = (await GetJson(URL)).FromJson<YtPlResult>(true, true);
                }
                catch (Exception ex)
                {
                    return null;
                }
                if (Result == null || Result.Items == null)
                {
                    return null;
                }
                Ret.AddRange(Result.Items);
                URL = BuildUrl("playlistItems", new
                {
                    part = "snippet",
                    maxResults = 20,
                    playlistId = PlaylistId,
                    pageToken = Result.NextPageToken
                });
            } while (!string.IsNullOrEmpty(Result.NextPageToken));
            return Ret.ToArray();
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
                        return await Tools.ReadStringAsync(Body);
                    }
                }

            }
            catch (WebException ex)
            {
                using (var Res = ex.Response)
                {
                    using (var Body = Res.GetResponseStream())
                    {
                        return await Tools.ReadStringAsync(Body);
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
