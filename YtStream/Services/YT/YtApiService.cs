﻿using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using YtStream.Extensions;
using YtStream.Models.YT;

namespace YtStream.Services.YT
{
    public class YtApiService
    {
        /// <summary>
        /// Youtube API base URL
        /// </summary>
        private const string ApiBase = "https://youtube.googleapis.com/youtube/v3/";

        private readonly string apiKey;
        private readonly ILogger<YtApiService> _logger;

        public YtApiService(ConfigService config, ILogger<YtApiService> logger)
        {
            _logger = logger;
            var c = config.GetConfiguration();
            if (string.IsNullOrWhiteSpace(c.YtApiKey))
            {
                throw new ArgumentException($"'{nameof(c.YtApiKey)}' property of the configuration cannot be null or whitespace.", nameof(config));
            }
            apiKey = c.YtApiKey;
        }

        public async Task<YtSnippetModel[]> GetPlaylistInfoAsync(string playlistId, int maxItems = int.MaxValue)
        {
            if (!Tools.IsYoutubePlaylist(playlistId))
            {
                throw new ArgumentException("Playlist id is invalid");
            }
            var ret = new List<YtSnippetModel>();
            if (maxItems <= 0)
            {
                return ret.ToArray();
            }
            var url = BuildUrl("playlistItems", new
            {
                part = "snippet",
                maxResults = 50,
                playlistId
            });
            YtResultModel result;
            _logger.LogInformation("Getting YT playlist info for {id}", playlistId);
            do
            {
                try
                {
                    result = (await GetJson(url)).FromJson<YtResultModel>(true, true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to get JSON from YT API {url}", url);
                    return null;
                }
                if (result == null || result.Items == null)
                {
                    return null;
                }
                ret.AddRange(result.Items.Select(m => m.Snippet));
                url = BuildUrl("playlistItems", new
                {
                    part = "snippet",
                    maxResults = 50,
                    playlistId,
                    pageToken = result.NextPageToken
                });
            } while (ret.Count < maxItems && !string.IsNullOrEmpty(result.NextPageToken));
            return ret.Take(maxItems).ToArray();
        }

        public async Task<YtSnippetModel> GetVideoInfo(string videoId)
        {
            if (!Tools.IsYoutubeId(videoId))
            {
                throw new ArgumentException("Video id is invalid");
            }
            var url = BuildUrl("videos", new
            {
                part = "snippet",
                maxResults = 1,
                id = videoId
            });
            _logger.LogInformation("Getting YT video info for {id}", videoId);
            try
            {
                return (await GetJson(url)).FromJson<YtResultModel>(true, true).Items[0].Snippet;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get JSON from YT API {url}", url);
                return null;
            }
        }

        private Uri BuildUrl(string ytFunction, object ytParams)
        {
            var baseUrl = $"{ApiBase}{ytFunction}?key={Esc(apiKey)}";
            List<string> encoded = null;
            if (ytParams != null)
            {
                encoded = ytParams
                    .GetType()
                    .GetProperties()
                    .Select(m => $"{Esc(m.Name)}={Esc(m.GetValue(ytParams))}")
                    .ToList();
            }
            if (encoded != null && encoded.Count > 0)
            {
                baseUrl += "&" + string.Join("&", encoded);
            }
            _logger.LogDebug("YT url builder: {url}", baseUrl);
            return new Uri(baseUrl);
        }

        private async Task<string> GetJson(Uri url)
        {
            var req = new HttpClient();
            var msg = new HttpRequestMessage
            {
                RequestUri = url,
                Method = HttpMethod.Get
            };
            msg.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            msg.Headers.UserAgent.Add(new ProductInfoHeaderValue("YtStream +https://github.com/AyrA/YtStream"));

            try
            {
                using var res = await req.SendAsync(msg);
                res.EnsureSuccessStatusCode();
                var body = await res.Content.ReadAsStringAsync();
                _logger.LogDebug("JSON from YT api: {body}", body);
                return body;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Youtube API at {url} failed", url);
            }
            return null;
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
