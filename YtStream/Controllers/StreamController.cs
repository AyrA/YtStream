using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YtStream.Enums;
using YtStream.Models;
using YtStream.Models.Mp3;
using YtStream.Models.YtDl;
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
                            _logger.LogInformation("User {username}: Key based authentication success", CurrentUser.Username);
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

        [HttpGet, ActionName("Send")]
        public async Task<IActionResult> SendAsync(string id)
        {
            //We do not bind the model as parameter in the method because we want custom boolean parser
            var model = new StreamOptionsModel(Request.Query);

            var ids = await ExpandIdList(SplitIds(id));
            if (ids == null || ids.Length == 0)
            {
                return NotFound("No id specified");
            }
            if (ids.Any(m => !Tools.IsYoutubeId(m)))
            {
                return BadRequest($"Invalid id: {ids.FirstOrDefault(m => !Tools.IsYoutubeId(m))}");
            }
            if (IsHead())
            {
                _logger.LogInformation("Early termination on HEAD request");
                Tools.SetAudioHeaders(Response);
                return new EmptyResult();
            }
            var includeAds = ShouldPlayAds();
            var markAds = ShouldMarkAds();
            var currentAdType = AdTypeEnum.Intro;
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
            _logger.LogInformation("Preparing response for {count} ids", ids.Length);

            for (var iteration = 0; iteration < model.Repeat; iteration++)
            {
                if (model.Random)
                {
                    ids = Tools.Shuffle(ids);
                }
                foreach (var ytid in ids)
                {
                    //Stop streaming if the client is gone or the application has been locked
                    if (_lockService.Locked || cancelToken.IsCancellationRequested)
                    {
                        break;
                    }
                    //Configure client output
                    outputStreams = new Mp3CutTargetStreamConfigModel();
                    outputStreams.AddStream(new Mp3CutTargetStreamInfoModel(Response.Body, model.Raw, true, true, model.Stream));

                    var setCache = true;
                    var filename = Tools.GetIdName(ytid) + ".mp3";
                    var ranges = model.Raw || !Settings.UseSponsorBlock ? Array.Empty<TimeRangeModel>() : await _sponsorBlockCacheService.GetRangesAsync(ytid);
                    _logger.LogInformation("{file} has {count} ranges", filename, ranges.Length);

                    //Try to get file from cache first
                    FileStream cacheStream = null;
                    try
                    {
                        cacheStream = mp3CacheHandler.OpenIfNotStale(filename);
                        if (cacheStream != null)
                        {
                            mp3CacheHandler.Poke(filename);
                        }
                    }
                    catch
                    {
                        _logger.LogWarning("Could not open {file} from cache. Will perform a direct YT stream", filename);
                        //If this doesn't works, the file is currently being written to.
                        //In that case we directly go to youtube but do not create a cached file.
                        setCache = false;
                    }

                    //File in cache found. Use that
                    if (cacheStream != null)
                    {
                        using (cacheStream)
                        {
                            if (cacheStream.Length > 0)
                            {
                                _logger.LogInformation("Using cache for {file}", filename);
                                if (Tools.SetAudioHeaders(Response))
                                {
                                    await Response.StartAsync();
                                }
                                using (var S = GetAd(currentAdType))
                                {
                                    var delay = await _mp3CutService.SendAd(S, Response.Body, markAds);
                                    if (model.Stream)
                                    {
                                        await Task.Delay(TimeSpan.FromMilliseconds(delay));
                                    }
                                }
                                currentAdType = AdTypeEnum.Inter;
                                await _mp3CutService.CutMp3Async(ranges, cacheStream, outputStreams, model.Buffer);
                                await Response.Body.FlushAsync();
                                continue;
                            }
                            else
                            {
                                _logger.LogWarning("{filename} is empty. Falling back to stream from YT", filename);
                            }
                        }
                    }

                    /////////////////////////////////////////////////////////////
                    //At this point we need to go live to youtube to get the file

                    YoutubeDlResultModel details;
                    string url;
                    try
                    {
                        details = await _youtubeDlService.GetAudioDetails(ytid);
                        url = details.Url;
                        if (details.Duration > Settings.MaxVideoDuration)
                        {
                            throw new YoutubeDlException("Video exceeds permitted duration. " +
                                $"{TimeSpan.FromSeconds(details.Duration)} > {TimeSpan.FromSeconds(Settings.MaxVideoDuration)}");
                        }
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
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Non-YT related download error from YTDL for id {id}", ytid);
                        throw;
                    }

                    if (string.IsNullOrEmpty(url))
                    {
                        _logger.LogWarning("No YT url for {id} (invalid or restricted video id)", ytid);
                        ++skipped;
                        continue;
                    }
                    if (setCache)
                    {
                        _logger.LogInformation("Saving video {id} to cache as {filename}", ytid, filename);
                        cacheStream = mp3CacheHandler.WriteFile(filename);
                        outputStreams.AddStream(new Mp3CutTargetStreamInfoModel(cacheStream, true, false, false, false));
                    }

                    //This is set to false if the converter encounters problems,
                    //which indicates that the cached file should be deleted
                    //Note: Currently doesn't works properly because ffmpeg often won't exit with an error code
                    bool keepCache = true;

                    using (var mp3raw = _mp3ConverterService.ConvertToMp3(url))
                    {
                        using var mp3Data = new Code.BufferedStream(1024 * 1024);
                        var mp3ConverterCopyTask = mp3raw.CopyToAsync(mp3Data).ContinueWith(delegate
                        {
                            mp3Data.EndWriteOperations();
                            _logger.LogInformation("Buffered stream write ended after {count} bytes", mp3Data.Length);
                        });
                        if (Tools.SetAudioHeaders(Response))
                        {
                            await Response.StartAsync();
                        }

                        if (cacheStream != null)
                        {
                            _logger.LogInformation("Downloading video {id} from YT and populate cache", ytid);
                            using (cacheStream)
                            {
                                using (var S = GetAd(currentAdType))
                                {
                                    var delay = await _mp3CutService.SendAd(S, Response.Body, markAds);
                                    if (model.Stream)
                                    {
                                        if (currentAdType == AdTypeEnum.Intro)
                                        {
                                            var sleep = delay - (model.Buffer * 1000);
                                            if (sleep > 0)
                                            {
                                                await Task.Delay(TimeSpan.FromMilliseconds(sleep));
                                            }
                                        }
                                        else
                                        {
                                            await Task.Delay(TimeSpan.FromMilliseconds(delay));
                                        }
                                    }
                                }
                                currentAdType = AdTypeEnum.Inter;
                                await _mp3CutService.CutMp3Async(ranges, mp3Data, outputStreams, model.Buffer);
                                await mp3ConverterCopyTask;
                                await _mp3ConverterService.WaitForExitAsync();
                                if (_mp3ConverterService.LastExitCode != 0)
                                {
                                    keepCache = false;
                                    _logger.LogWarning("Error downloading video {id} from YT. FFMpeg exited with code {code}",
                                        url, _mp3ConverterService.LastExitCode);
                                }
                                else if (cacheStream.Position == 0)
                                {
                                    keepCache = false;
                                    _logger.LogWarning("Error downloading video {id} from YT. Output is empty", url);
                                }
                                else
                                {
                                    _logger.LogInformation("Converter exited with code {code}", _mp3ConverterService.LastExitCode);
                                }
                            }
                            if (!keepCache)
                            {
                                try
                                {
                                    mp3CacheHandler.DeleteFile(filename);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Unable to delete {file} from cache", filename);
                                }
                            }
                        }
                        else
                        {
                            //Same download task as above but without writing to cache
                            _logger.LogInformation("Downloading video {id} from YT without populating cache", ytid);
                            using (var S = GetAd(currentAdType))
                            {
                                var delay = await _mp3CutService.SendAd(S, Response.Body, markAds);
                                if (model.Stream)
                                {
                                    await Task.Delay(TimeSpan.FromMilliseconds(delay));
                                }
                            }
                            currentAdType = AdTypeEnum.Inter;
                            await _mp3CutService.CutMp3Async(ranges, mp3Data, outputStreams, model.Buffer);
                            await mp3ConverterCopyTask;
                        }
                        //Flush all data before attempting the next file
                        await Response.Body.FlushAsync();

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
                        _logger.LogWarning("None of the ids {list} yielded usable results", ids);
                        return NotFound();
                    }

                }
            }
            using (var S = GetAd(AdTypeEnum.Outro))
            {
                var delay = await _mp3CutService.SendAd(S, Response.Body, markAds);
                if (model.Stream)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(delay));
                }
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
                        _logger.LogInformation("Playing ad: {name}", Name);
                        return _adsService.GetAd(Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send ad. Reason: {reason}", ex.Message);
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
