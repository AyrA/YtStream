﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YtStream.Models;

namespace YtStream.Controllers
{
    public class StreamController : Controller
    {
        private readonly ConfigModel Settings;
        private readonly ILogger _logger;

        public StreamController(ILogger<StreamController> Logger)
        {
            _logger = Logger;
            Settings = ConfigModel.Load();
        }

        private static string[] SplitIds(string id)
        {
            return string.IsNullOrEmpty(id) ? new string[0] : id.Split(',');
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var Controller = (ControllerBase)context.Controller;
            if (Startup.Locked)
            {
                //context.Result = StatusCode(503, "Streaming services are currently locked. Please try again later");
                context.HttpContext.Response.StatusCode = 503;
                context.Result = View("Locked");
                await base.OnActionExecutionAsync(context, next);
            }
            else
            {
                await base.OnActionExecutionAsync(context, next);
            }
        }

        public IActionResult Locked()
        {
            return View();
        }

        public async Task<IActionResult> Order(string id)
        {
            return await PerformStream(SplitIds(id));
        }

        public async Task<IActionResult> Ranges(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound("No id specified");
            }
            if (Tools.IsYoutubeId(id))
            {
                return Json(await GetRanges(id));
            }
            return BadRequest("Invalid youtube id");
        }

        public async Task<IActionResult> Random(string id)
        {
            return await PerformStream(Tools.Shuffle(SplitIds(id)));
        }

        private async Task<IActionResult> PerformStream(string[] ids)
        {
            if (ids == null || ids.Length == 0)
            {
                return NotFound("No id specified");
            }
            var inv = ids.FirstOrDefault(m => !Tools.IsYoutubeId(m));
            if (inv != null)
            {
                return BadRequest($"Invalid id: {inv}");
            }
            var MP3 = Cache.GetHandler(Cache.CacheType.MP3, Settings.CacheMp3Lifetime);
            var skipped = 0;
            _logger.LogInformation("Preparing response for {0} ids", ids.Length);

            //Buffering to prevent stalling the server
            //TODO: Consider reading the audio length from the YTDL metadata
            //and simply abort the request if download takes longer than playback.

            using (var BS = new BufferedStream(Response.Body, Settings.OutputBufferKB * 1000))
            {
                foreach (var ytid in ids)
                {
                    //Stop streaming if the client is gone or the application has been locked
                    if(Startup.Locked || HttpContext.RequestAborted.IsCancellationRequested)
                    {
                        break;
                    }
                    var setCache = true;
                    var filename = Tools.GetIdName(ytid) + ".mp3";
                    var ranges = await GetRanges(ytid);
                    _logger.LogInformation("{0} has {1} ranges", filename, ranges.Length);
                    FileStream CacheStream = null;
                    try
                    {
                        CacheStream = MP3.OpenIfNotStale(filename);
                    }
                    catch
                    {
                        _logger.LogWarning("Could not open {0} from cache. Will perform a direct YT stream", filename);
                        //If this doesn't works, the file is currently being written to.
                        //In that case we directly go to youtube but do not create a cached file
                        setCache = false;
                    }
                    if (CacheStream != null)
                    {
                        _logger.LogInformation("Using cache for {0}", filename);
                        using (CacheStream)
                        {
                            Tools.SetAudioHeaders(Response);
                            await Mp3Cut.CutMp3Async(ranges, CacheStream, BS);
                            await BS.FlushAsync();
                            continue;
                        }
                    }
                    //At this point we need to go live to youtube to get the file
                    var ytdl = new YoutubeDl(Settings.YoutubedlPath);
                    var converter = new Converter(Settings.FfmpegPath);
                    var url = await ytdl.GetAudioUrl(ytid);
                    if (string.IsNullOrEmpty(url))
                    {
                        _logger.LogWarning("No YT url for {0} (invalid or restricted video id)", ytid);
                        ++skipped;
                        continue;
                    }
                    if (setCache)
                    {
                        CacheStream = MP3.WriteFile(filename);
                    }
                    using (var Mp3Data = converter.ConvertToMp3(url))
                    {
                        Tools.SetAudioHeaders(Response);
                        if (CacheStream != null)
                        {
                            _logger.LogInformation("Downloading {0} from YT and populate cache", ytid);
                            using (CacheStream)
                            {
                                await Mp3Cut.CutMp3Async(ranges, Mp3Data, BS, CacheStream);
                            }
                        }
                        else
                        {
                            _logger.LogInformation("Downloading {0} from YT without populating cache", ytid);
                            await Mp3Cut.CutMp3Async(ranges, Mp3Data, BS);
                        }
                        //Flush all data before attempting the next file
                        await BS.FlushAsync();
                    }
                }
            }
            if (skipped == ids.Length)
            {
                _logger.LogWarning("None of the ids yielded usable results");
                return NotFound();
            }
            _logger.LogInformation("Stream complete");
            return new EmptyResult();
        }

        /// <summary>
        /// Gets time ranges for the given youtube id and utilizes cache where possible.
        /// </summary>
        /// <param name="ytid">Youtube id</param>
        /// <returns>time ranges</returns>
        private async Task<TimeRange[]> GetRanges(string ytid)
        {
            var C = Cache.GetHandler(Cache.CacheType.SponsorBlock, Settings.CacheSBlockLifetime);
            var FN = Tools.GetIdName(ytid) + ".json";
            var FS = C.OpenIfNotStale(FN);
            if (FS != null)
            {
                var Json = await Tools.ReadStringAsync(FS);
                var Segments = Json.FromJson<TimeRange[]>();
                if (Segments != null)
                {
                    return Segments;
                }
                //Cache problem. Get live data
            }
            var Ranges = await SponsorBlock.GetRangesAsync(ytid);
            if (Ranges != null)
            {
                using (var Cache = C.WriteFile(FN))
                {
                    await Tools.WriteStringAsync(Cache, Ranges.ToJson());
                }
                return Ranges;
            }
            return new TimeRange[0];
        }
    }
}
