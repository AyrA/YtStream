using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YtStream.Enums;
using YtStream.Models.Mp3;
using YtStream.Services;
using YtStream.Services.Accounts;
using YtStream.Services.Mp3;
using YtStream.YtDl;

namespace YtStream.Controllers
{
    public class StreamController : BaseController
    {
        private readonly ILogger<StreamController> _logger;
        private readonly LockService _lockService;
        private readonly YoutubeDlService _youtubeDlService;
        private readonly Mp3ConverterService _mp3ConverterService;
        private readonly Mp3CutService _mp3CutService;
        private readonly CacheService _cacheService;
        private readonly AdsService _adsService;
        private readonly SponsorBlockCacheService _sponsorBlockCacheService;

        public StreamController(ILogger<StreamController> logger, ConfigService config, UserManagerService userManager,
            LockService lockService, YoutubeDlService youtubeDlService,
            Mp3ConverterService mp3ConverterService, Mp3CutService mp3CutService, CacheService cacheService,
            AdsService adsService, SponsorBlockCacheService sponsorBlockCacheService) : base(config, userManager)
        {
            _logger = logger;
            _lockService = lockService;
            _youtubeDlService = youtubeDlService;
            _mp3ConverterService = mp3ConverterService;
            _mp3CutService = mp3CutService;
            _cacheService = cacheService;
            _adsService = adsService;
            _sponsorBlockCacheService = sponsorBlockCacheService;
        }

        private static string[] SplitIds(string id)
        {
            return string.IsNullOrEmpty(id) ? Array.Empty<string>() : id.Split(',');
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var Controller = (ControllerBase)context.Controller;
            if (_lockService.Locked)
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
            //Set converter user agent to match youtube-dl UA
            _mp3ConverterService.SetUserAgent(await _youtubeDlService.GetUserAgent());
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

        private async Task<IActionResult> PerformStream(string[] ids, bool includeAds, bool markAds)
        {
            var CurrentAdType = AdTypeEnum.Intro;

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
                if (Tools.SetAudioHeaders(Response))
                {
                    await Response.StartAsync();
                }
                return new EmptyResult();
            }
            var mp3CacheHandler = _cacheService.GetHandler(CacheTypeEnum.MP3, Settings.CacheMp3Lifetime);
            var skipped = 0;
            Mp3CutTargetStreamConfigModel outputStreams = null;
            var cancelToken = HttpContext.RequestAborted;
            cancelToken.Register(delegate ()
            {
                _logger.LogInformation("Connection gone");
                var os = outputStreams;
                os?.SetTimeout(false);
            });
            _logger.LogInformation("Preparing response for {0} ids", ids.Length);

            foreach (var ytid in ids)
            {
                //Stop streaming if the client is gone or the application has been locked
                if (_lockService.Locked || cancelToken.IsCancellationRequested)
                {
                    break;
                }
                outputStreams = new Mp3CutTargetStreamConfigModel();
                outputStreams.AddStream(new Mp3CutTargetStreamInfoModel(Response.Body, false, true, true, Settings.SimulateRealStream));
                var setCache = true;
                var filename = Tools.GetIdName(ytid) + ".mp3";
                var ranges = await _sponsorBlockCacheService.GetRangesAsync(ytid);
                _logger.LogInformation("{0} has {1} ranges", filename, ranges.Length);
                FileStream CacheStream = null;
                try
                {
                    CacheStream = mp3CacheHandler.OpenIfNotStale(filename);
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
                            if (Tools.SetAudioHeaders(Response))
                            {
                                await Response.StartAsync();
                            }
                            using (var S = GetAd(CurrentAdType))
                            {
                                await _mp3CutService.SendAd(S, Response.Body, markAds);
                            }
                            CurrentAdType = AdTypeEnum.Inter;
                            await _mp3CutService.CutMp3Async(ranges, CacheStream, outputStreams);
                            await Response.Body.FlushAsync();
                            continue;
                        }
                        else
                        {
                            _logger.LogWarning("{0} is empty. Falling back to stream from YT", filename);
                        }
                    }
                }

                /////////////////////////////////////////////////////////////
                //At this point we need to go live to youtube to get the file

                string url;
                try
                {
                    url = await _youtubeDlService.GetAudioUrl(ytid);
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
                    CacheStream = mp3CacheHandler.WriteFile(filename);
                    outputStreams.AddStream(new Mp3CutTargetStreamInfoModel(CacheStream, true, false, false, false));
                }
                using (var Mp3Data = _mp3ConverterService.ConvertToMp3(url))
                {
                    if (Tools.SetAudioHeaders(Response))
                    {
                        await Response.StartAsync();
                    }

                    if (CacheStream != null)
                    {
                        _logger.LogInformation("Downloading {0} from YT and populate cache", ytid);
                        using (CacheStream)
                        {
                            using (var S = GetAd(CurrentAdType))
                            {
                                await _mp3CutService.SendAd(S, Response.Body, markAds);
                            }
                            CurrentAdType = AdTypeEnum.Inter;
                            await _mp3CutService.CutMp3Async(ranges, Mp3Data, outputStreams);
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
                        using (var S = GetAd(CurrentAdType))
                        {
                            await _mp3CutService.SendAd(S, Response.Body, markAds);
                        }
                        CurrentAdType = AdTypeEnum.Inter;
                        await _mp3CutService.CutMp3Async(ranges, Mp3Data, outputStreams);
                    }
                    //Flush all data before attempting the next file
                    await Response.Body.FlushAsync();
                }
                if (outputStreams.HasTimeout)
                {
                    _logger.LogWarning("An output stream had a timeout. Aborting processing");
                    break;
                }
                if (outputStreams.HasFaultedStreams())
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
            using (var S = GetAd(AdTypeEnum.Outro))
            {
                await _mp3CutService.SendAd(S, Response.Body, markAds);
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
            foreach (var id in PlaylistIds)
            {
                var PL = await _youtubeDlService.GetPlaylist(id, MaxItems);
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

        private Stream GetAd(AdTypeEnum Type)
        {
            if (ShouldPlayAds() && !HttpContext.RequestAborted.IsCancellationRequested)
            {
                try
                {
                    var Name = _adsService.GetRandomAdName(Type);
                    if (Name != null)
                    {
                        _logger.LogInformation("Playing ad: {0}", Name);
                        return _adsService.GetAd(Name);
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
                (Settings.AdminAds || !CurrentUser.Roles.HasFlag(UserRoles.Administrator));
        }

        private bool ShouldMarkAds()
        {
            return Settings.MarkAds;
        }
    }
}
