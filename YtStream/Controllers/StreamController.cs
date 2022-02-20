using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YtStream.Ad;
using YtStream.MP3;

namespace YtStream.Controllers
{
    public class StreamController : BaseController
    {
        private readonly ILogger _logger;

        public StreamController(ILogger<StreamController> Logger)
        {
            _logger = Logger;
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
                context.HttpContext.Response.StatusCode = 503;
                context.Result = View("Locked");
                await base.OnActionExecutionAsync(context, next);
                return;
            }
            if (Settings == null || !Settings.IsValid())
            {
                context.Result = StatusCode(500, "Misconfiguration. Please check the application settings");
                return;
            }
            //Do not allow the use of stream keys if we're logged in
            if (Settings.RequireAccount && !User.Identity.IsAuthenticated)
            {
                //Check if streaming key was supplied
                var key = context.HttpContext.Request.Query["key"];
                if (key.Count == 1)
                {
                    if (Guid.TryParse(key.ToString(), out Guid StreamKey))
                    {
                        SetApiUser(StreamKey);
                        if (CurrentUser != null && CurrentUser.Enabled)
                        {
                            _logger.LogInformation("User {0}: Key based authentication success", CurrentUser.Username);
                            await base.OnActionExecutionAsync(context, next);
                            return;
                        }
                    }
                }
                context.Result = RedirectToAction("Login", "Account", new { returnUrl = HttpContext.Request.Path });
                return;
            }
            await base.OnActionExecutionAsync(context, next);
        }

        public IActionResult Locked()
        {
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> List(string id)
        {
            if (IsHead())
            {
                return Json(null);
            }
            if (!Tools.IsYoutubePlaylist(id))
            {
                return BadRequest("Supplied argument is not a valid playlist identifier");
            }
            var ytdl = new YoutubeDl(Settings.YoutubedlPath);
            var PL = await ytdl.GetPlaylist(id);
            if (PL == null || PL.Length == 0)
            {
                return NotFound("Playlist empty or does not exist");
            }
            Tools.SetExpiration(Response, TimeSpan.FromHours(1));
            return Json(PL);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Info(string id)
        {
            if (IsHead())
            {
                return Json(null);
            }
            if (!Tools.IsYoutubeId(id))
            {
                return BadRequest("Supplied argument is not a valid video identifier");
            }
            var ytdl = new YoutubeDl(Settings.YoutubedlPath);
            var Info = await ytdl.GetAudioDetails(id);
            if (Info == null)
            {
                return NotFound("Video id does not exist or is restricted/private");
            }
            Tools.SetExpiration(Response, TimeSpan.FromHours(1));
            //Do not forward the URL since it's unusable
            Info.Url = null;
            return Json(Info);
        }


        public async Task<IActionResult> PlaylistOrder(string id)
        {
            if (!Tools.IsYoutubePlaylist(id))
            {
                return BadRequest("Supplied argument is not a valid playlist identifier");
            }
            if (IsHead())
            {
                Tools.SetAudioHeaders(Response);
                return new EmptyResult();
            }
            var ytdl = new YoutubeDl(Settings.YoutubedlPath);
            var PL = await ytdl.GetPlaylist(id);
            if (PL == null || PL.Length == 0)
            {
                return NotFound("Playlist empty or does not exist");
            }
            return await PerformStream(PL, ShouldPlayAds());
        }

        public async Task<IActionResult> PlaylistRandom(string id)
        {
            if (!Tools.IsYoutubePlaylist(id))
            {
                return BadRequest("Supplied argument is not a valid playlist identifier");
            }
            if (IsHead())
            {
                Tools.SetAudioHeaders(Response);
                return new EmptyResult();
            }
            var ytdl = new YoutubeDl(Settings.YoutubedlPath);
            var PL = await ytdl.GetPlaylist(id);
            if (PL == null || PL.Length == 0)
            {
                return NotFound("Playlist empty or does not exist");
            }
            return await PerformStream(Tools.Shuffle(PL), ShouldPlayAds());
        }

        public async Task<IActionResult> Order(string id)
        {
            return await PerformStream(SplitIds(id), ShouldPlayAds());
        }

        public async Task<IActionResult> Ranges(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound("No id specified");
            }
            if (Tools.IsYoutubeId(id))
            {
                if (IsHead())
                {
                    return Json(null);
                }
                return Json(await GetRanges(id));
            }
            return BadRequest("Invalid youtube id");
        }

        public async Task<IActionResult> Random(string id)
        {
            return await PerformStream(Tools.Shuffle(SplitIds(id)), ShouldPlayAds());
        }

        private async Task<IActionResult> PerformStream(string[] ids, bool IncludeAds)
        {
            var AdHandler = IncludeAds ? new Ads() : null;
            var CurrentAdType = AdType.Intro;

            if (ids == null || ids.Length == 0)
            {
                return NotFound("No id specified");
            }
            var inv = ids.FirstOrDefault(m => !Tools.IsYoutubeId(m));
            if (inv != null)
            {
                return BadRequest($"Invalid id: {inv}");
            }
            if (IsHead())
            {
                _logger.LogInformation("Early termination on HEAD request");
                Tools.SetAudioHeaders(Response);
                return new EmptyResult();
            }
            var MP3 = Cache.GetHandler(Cache.CacheType.MP3, Settings.CacheMp3Lifetime);
            var skipped = 0;
            _logger.LogInformation("Preparing response for {0} ids", ids.Length);

            foreach (var ytid in ids)
            {
                //Stop streaming if the client is gone or the application has been locked
                if (Startup.Locked || HttpContext.RequestAborted.IsCancellationRequested)
                {
                    break;
                }
                var OutputStreams = new MP3CutTargetStreamConfig();
                OutputStreams.AddStream(new MP3CutTargetStreamInfo(Response.Body, false, true, true));
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
                    using (CacheStream)
                    {
                        if (CacheStream.Length > 0)
                        {
                            _logger.LogInformation("Using cache for {0}", filename);
                            Tools.SetAudioHeaders(Response);
                            await PlayAd(AdHandler, CurrentAdType);
                            CurrentAdType = AdType.Inter;
                            await MP3Cut.CutMp3Async(ranges, CacheStream, OutputStreams);
                            await Response.Body.FlushAsync();
                            continue;
                        }
                        else
                        {
                            _logger.LogWarning("{0} is empty. Falling back to stream from YT", filename);
                        }
                    }
                }
                //At this point we need to go live to youtube to get the file
                var ytdl = new YoutubeDl(Settings.YoutubedlPath);
                var converter = new Converter(Settings.FfmpegPath, await ytdl.GetUserAgent());
                converter.AudioFrequency = Settings.AudioFrequency;
                converter.AudioRate = Settings.AudioBitrate;
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
                    OutputStreams.AddStream(new MP3CutTargetStreamInfo(CacheStream, true, false, false));
                }
                using (var Mp3Data = converter.ConvertToMp3(url))
                {
                    Tools.SetAudioHeaders(Response);
                    if (CacheStream != null)
                    {
                        _logger.LogInformation("Downloading {0} from YT and populate cache", ytid);
                        using (CacheStream)
                        {
                            await PlayAd(AdHandler, CurrentAdType);
                            CurrentAdType = AdType.Inter;
                            await MP3Cut.CutMp3Async(ranges, Mp3Data, OutputStreams);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("Downloading {0} from YT without populating cache", ytid);
                        await PlayAd(AdHandler, CurrentAdType);
                        CurrentAdType = AdType.Inter;
                        await MP3Cut.CutMp3Async(ranges, Mp3Data, OutputStreams);
                    }
                    //Flush all data before attempting the next file
                    await Response.Body.FlushAsync();
                }
                if (OutputStreams.HasTimeout)
                {
                    _logger.LogWarning("An output stream had a timeout. Aborting processing");
                    break;
                }
                if (OutputStreams.HasFaultedStreams())
                {
                    _logger.LogWarning("An output stream faulted. Aborting processing");
                    break;
                }
            }
            if (skipped == ids.Length)
            {
                _logger.LogWarning("None of the ids yielded usable results");
                return NotFound();
            }
            await PlayAd(AdHandler, AdType.Outro);
            _logger.LogInformation("Stream request complete");

            return new EmptyResult();
        }

        private async Task PlayAd(Ads Handler, AdType Type)
        {
            if (Handler != null && !HttpContext.RequestAborted.IsCancellationRequested)
            {
                try
                {
                    var Name = Handler.GetRandomAdName(Type);
                    if (Name != null)
                    {
                        using (var FS = Handler.GetAd(Name))
                        {
                            _logger.LogInformation("Playing ad: {0}", Name);
                            await FS.CopyToAsync(Response.Body);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to send ad. Reaso: {0}", ex.Message);
                }
            }
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

        private bool IsHead()
        {
            return HttpContext.Request.Method.ToUpper() == "HEAD";
        }

        private bool ShouldPlayAds()
        {
            return !CurrentUser.DisableAds &&
                (Settings.AdminAds || !CurrentUser.Roles.HasFlag(Accounts.UserRoles.Administrator));
        }
    }
}
