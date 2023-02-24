using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YtStream.Ad;
using YtStream.MP3;
using YtStream.YtDl;

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

        public async Task<IActionResult> Order(string id)
        {
            return await PerformStream(await ExpandIdList(SplitIds(id)), ShouldPlayAds(), ShouldMarkAds());
        }

        public async Task<IActionResult> Random(string id)
        {
            return await PerformStream(Tools.Shuffle(await ExpandIdList(SplitIds(id))), ShouldPlayAds(), ShouldMarkAds());
        }

        private async Task<IActionResult> PerformStream(string[] ids, bool IncludeAds, bool MarkAds)
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
            MP3CutTargetStreamConfig OutputStreams = null;
            var CToken = HttpContext.RequestAborted;
            CToken.Register(delegate ()
            {
                _logger.LogInformation("Connection gone");
                var os = OutputStreams;
                if (os != null)
                {
                    os.SetTimeout(false);
                }
            });
            _logger.LogInformation("Preparing response for {0} ids", ids.Length);

            foreach (var ytid in ids)
            {
                //Stop streaming if the client is gone or the application has been locked
                if (Startup.Locked || CToken.IsCancellationRequested)
                {
                    break;
                }
                OutputStreams = new MP3CutTargetStreamConfig();
                OutputStreams.AddStream(new MP3CutTargetStreamInfo(Response.Body, false, true, true, Settings.SimulateRealStream));
                var setCache = true;
                var filename = Tools.GetIdName(ytid) + ".mp3";
                var ranges = await SponsorBlockCache.GetRangesAsync(ytid, Settings);
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
                    //In that case we directly go to youtube but do not create a cached file.
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
                            using (var S = GetAd(AdHandler, CurrentAdType))
                            {
                                await MP3Cut.SendAd(S, Response.Body, MarkAds);
                            }
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
                var converter = new Converter(Settings.FfmpegPath, await ytdl.GetUserAgent())
                {
                    AudioFrequency = Settings.AudioFrequency,
                    AudioRate = Settings.AudioBitrate
                };
                string url;
                try
                {
                    url = await ytdl.GetAudioUrl(ytid);
                }
                catch (YoutubeDlException ex)
                {
                    //If this is the only id, return an error to the client.
                    //We can't do this otherwise.
                    if (ids.Length == 1)
                    {
                        return Error(ex);
                    }
                    url = null;
                }
                catch (Exception)
                {
                    throw;
                }
                if (string.IsNullOrEmpty(url))
                {
                    _logger.LogWarning("No YT url for {0} (invalid or restricted video id)", ytid);
                    ++skipped;
                    continue;
                }
                if (setCache)
                {
                    CacheStream = MP3.WriteFile(filename);
                    OutputStreams.AddStream(new MP3CutTargetStreamInfo(CacheStream, true, false, false, false));
                }
                using (var Mp3Data = converter.ConvertToMp3(url))
                {
                    Tools.SetAudioHeaders(Response);
                    if (CacheStream != null)
                    {
                        _logger.LogInformation("Downloading {0} from YT and populate cache", ytid);
                        using (CacheStream)
                        {
                            using (var S = GetAd(AdHandler, CurrentAdType))
                            {
                                await MP3Cut.SendAd(S, Response.Body, MarkAds);
                            }
                            CurrentAdType = AdType.Inter;
                            await MP3Cut.CutMp3Async(ranges, Mp3Data, OutputStreams);
                            if (CacheStream.Position == 0)
                            {
                                _logger.LogError("Error downloading {0} from YT. Output is empty", url);
                                return Error(new Exception($"Error downloading {url} from YT. Output is empty"));
                            }
                        }
                    }
                    else
                    {
                        _logger.LogInformation("Downloading {0} from YT without populating cache", ytid);
                        using (var S = GetAd(AdHandler, CurrentAdType))
                        {
                            await MP3Cut.SendAd(S, Response.Body, MarkAds);
                        }
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
            using (var S = GetAd(AdHandler, AdType.Outro))
            {
                await MP3Cut.SendAd(S, Response.Body, MarkAds);
            }
            _logger.LogInformation("Stream request complete");

            return new EmptyResult();
        }

        private async Task<string[]> ExpandIdList(IEnumerable<string> IdList)
        {
            var ret = new List<string>();
            //Treat zero as maximum
            var max = Settings.MaxStreamIds > 0 ? Settings.MaxStreamIds : int.MaxValue;
            foreach (var item in IdList)
            {
                if (Tools.IsYoutubePlaylist(item))
                {
                    ret.AddRange(await EnumeratePlaylists(new string[] { item }, true, max - ret.Count));
                }
                else
                {
                    ret.Add(item);
                }
                if (ret.Count >= max)
                {
                    break;
                }
            }
            return ret.Take(max).ToArray();
        }

        private async Task<string[]> EnumeratePlaylists(IEnumerable<string> PlaylistIds, bool Skip = false, int MaxItems = 0)
        {
            var Ids = new List<string>();
            var ytdl = new YoutubeDl(Settings.YoutubedlPath);
            foreach (var id in PlaylistIds)
            {
                var PL = await ytdl.GetPlaylist(id, MaxItems);
                if (PL == null || PL.Length == 0)
                {
                    if (!Skip)
                    {
                        throw new Exception($"Playlist empty or does not exist: {id}");
                    }
                    continue;
                }
                Ids.AddRange(PL);
            }
            return Ids.ToArray();
        }

        private Stream GetAd(Ads Handler, AdType Type)
        {
            if (Handler != null && !HttpContext.RequestAborted.IsCancellationRequested)
            {
                try
                {
                    var Name = Handler.GetRandomAdName(Type);
                    if (Name != null)
                    {
                        _logger.LogInformation("Playing ad: {0}", Name);
                        return Handler.GetAd(Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to send ad. Reason: {0}", ex.Message);
                }
            }
            return Stream.Null;
        }

        private bool ShouldPlayAds()
        {
            if (CurrentUser == null)
            {
                return true;
            }
            return !CurrentUser.DisableAds &&
                (Settings.AdminAds || !CurrentUser.Roles.HasFlag(Accounts.UserRoles.Administrator));
        }

        private bool ShouldMarkAds()
        {
            return Settings.MarkAds;
        }
    }
}
