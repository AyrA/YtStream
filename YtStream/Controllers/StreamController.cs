using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YtStream.Code;
using YtStream.Enums;
using YtStream.Models;
using YtStream.Models.Favs;
using YtStream.Models.Mp3;
using YtStream.Models.YtDl;
using YtStream.Services;
using YtStream.Services.Accounts;
using YtStream.Services.Mp3;
using YtStream.YtDl;

namespace YtStream.Controllers
{

    [ApiController, Route("[controller]/[action]/{id}")]
    public class StreamController : BaseController
    {
        private readonly ILogger<StreamController> _logger;
        private readonly ApplicationLockService _lockService;
        private readonly YoutubeDlService _youtubeDlService;
        private readonly Mp3ConverterService _mp3ConverterService;
        private readonly Mp3CutService _mp3CutService;
        private readonly CacheService _cacheService;
        private readonly AdsService _adsService;
        private readonly SponsorBlockCacheService _sponsorBlockCacheService;
        private readonly StreamKeyLockService _keyLockService;
        private readonly IHostApplicationLifetime _applicationLifetime;
        private Guid streamKey;

        public StreamController(ILogger<StreamController> logger, ConfigService config, UserManagerService userManager,
            ApplicationLockService lockService, YoutubeDlService youtubeDlService,
            Mp3ConverterService mp3ConverterService, Mp3CutService mp3CutService, CacheService cacheService,
            AdsService adsService, SponsorBlockCacheService sponsorBlockCacheService,
            StreamKeyLockService keyLockService, IHostApplicationLifetime applicationLifetime) : base(config, userManager)
        {
            _logger = logger;
            _lockService = lockService;
            _youtubeDlService = youtubeDlService;
            _mp3ConverterService = mp3ConverterService;
            _mp3CutService = mp3CutService;
            _cacheService = cacheService;
            _adsService = adsService;
            _sponsorBlockCacheService = sponsorBlockCacheService;
            _keyLockService = keyLockService;
            _applicationLifetime = applicationLifetime;
        }

        private static string[] SplitIds(string idList)
        {
            return string.IsNullOrEmpty(idList) ? Array.Empty<string>() : idList.Split(',');
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var Controller = (ControllerBase)context.Controller;
            if (_lockService.Locked)
            {
                context.HttpContext.Response.StatusCode = 503;
                context.Result = View("Locked");
                return;
            }
            if (Settings == null || !Settings.IsValid())
            {
                EarlyTermination(context, 500, "Misconfiguration. Please check the application settings");
                return;
            }
            //handle session requirement
            if (Settings.RequireAccount)
            {
                //Check if streaming key was supplied
                var key = context.HttpContext.Request.Query["key"];
                if (key.Count == 1)
                {
                    if (Guid.TryParse(key.ToString(), out streamKey))
                    {
                        if (SetApiUser(streamKey))
                        {
                            _logger.LogInformation("User {username}: Key based authentication success", CurrentUser.Username);
                        }
                    }
                }
                else if (IsAuthenticated)
                {
                    if (SetUser(User.Identity?.Name))
                    {
                        //If authenticated but no key was used, use the username as key
                        streamKey = CurrentUser.GetNameBasedId();
                        _logger.LogInformation("User {username}: Using name based key: {key}",
                            CurrentUser.Username, streamKey);
                    }
                }
                else
                {
                    //Neither user authenticated, nor key supplied. Redirect to login.
                    context.Result = RequestLogin();
                    return;
                }

                if (streamKey != Guid.Empty && CurrentUser != null && CurrentUser.Enabled)
                {
                    if (!await _keyLockService.UseKeyAsync(streamKey, TimeSpan.FromSeconds(5)))
                    {
                        _logger.LogInformation("Key {key} already in use", streamKey);
                        EarlyTermination(context, 403, "Stream key is already in use");
                        return;
                    }
                    _logger.LogInformation("Locking key {key} for streaming", streamKey);
                }
                else
                {
                    //Authentication failure
                    streamKey = default;
                    EarlyTermination(context, 403, "Authentication failure");
                    return;
                }
            }
            //Set converter user agent to match youtube-dl UA
            _mp3ConverterService.SetUserAgent(await _youtubeDlService.GetUserAgent());
            await base.OnActionExecutionAsync(context, next);
        }

        public override void OnActionExecuted(ActionExecutedContext context)
        {
            try
            {
                base.OnActionExecuted(context);
            }
            finally //Ensure key is always freed, even on server errors
            {
                if (streamKey != Guid.Empty)
                {
                    _keyLockService.FreeKey(streamKey);
                    _logger.LogInformation("Unlocking key {key} for streaming", streamKey);
                }
            }
        }

        [HttpGet, ApiExplorerSettings(IgnoreApi = true)]
        public IActionResult Locked()
        {
            return View();
        }

        /// <summary>
        /// Calls the "Send" endpoint with the ids obtained from the given favorite
        /// </summary>
        /// <param name="id">Id of a user favorite entry</param>
        /// <param name="buffer">
        /// Amount of data buffer in seconds. The default is 5.
        /// Has no effect if "<paramref name="stream"/>" argument is not enabled
        /// </param>
        /// <param name="repeat">Number of repetitions. The default is 1</param>
        /// <param name="key">
        /// Streaming key.
        /// Required if the system requires an account to stream, and the device is not logged in
        /// </param>
        /// <param name="stream">
        /// Send as live stream rather than as fast as possible.
        /// If supplied, data will be sent at the speed required for playback,
        /// plus a few extra seconds buffer as specified in "<paramref name="buffer"/>" argument
        /// </param>
        /// <param name="raw">
        /// Do not cut non-music sections.
        /// If supplied, the system will not try to cut sections marked as non-music,
        /// and instead sends the raw MP3 data as-is.
        /// This parameter is ignored if the admin disables non-music cutting
        /// </param>
        /// <param name="random">
        /// Randomize id list.
        /// If specified, it randomizes the id list before playback.
        /// This is done for every repeat iteration if there are multiple
        /// </param>
        /// <returns>MP3 data</returns>
        [HttpGet, ActionName("Favorite"), Produces("audio/mpeg", "text/plain")]
        public async Task<IActionResult> FavAsync(
            [FromRoute] string id,
            [FromQuery] int? buffer,
            [FromQuery] int? repeat,
            [FromQuery] string? key,
            [FromQuery] ApiBoolEnum? stream = ApiBoolEnum.N,
            [FromQuery] ApiBoolEnum? raw = ApiBoolEnum.N,
            [FromQuery] ApiBoolEnum? random = ApiBoolEnum.N)
        {
            if (Guid.TryParse(id, out var favKey))
            {
                var fav = _userManager.GetFavorite(favKey);
                if (fav == null)
                {
                    return NotFound("A favorite with the given id was not found");
                }
                if (fav.Type == FavoriteType.Stream)
                {
                    var sFav = (StreamFavoriteModel)fav;
                    var ids = string.Join(",", sFav.Ids ?? Array.Empty<string>());
                    return await SendAsync(ids, buffer, repeat, key, stream, raw, random);
                }
                else
                {
                    return BadRequest("This favorite is for the media player, not the streaming system");
                }
            }
            return BadRequest("Invalid id");
        }

        /// <summary>
        /// Converts and sends YT data as a single, continuous MP3 stream
        /// </summary>
        /// <param name="id">List of video and/or playlist ids. Items are separated by comma</param>
        /// <param name="buffer">
        /// Amount of data buffer in seconds. The default is 5.
        /// Has no effect if "<paramref name="stream"/>" argument is not enabled
        /// </param>
        /// <param name="repeat">Number of repetitions. The default is 1</param>
        /// <param name="key">
        /// Streaming key.
        /// Required if the system requires an account to stream, and the device is not logged in
        /// </param>
        /// <param name="stream">
        /// Send as live stream rather than as fast as possible.
        /// If supplied, data will be sent at the speed required for playback,
        /// plus a few extra seconds buffer as specified in "<paramref name="buffer"/>" argument
        /// </param>
        /// <param name="raw">
        /// Do not cut non-music sections.
        /// If supplied, the system will not try to cut sections marked as non-music,
        /// and instead sends the raw MP3 data as-is.
        /// This parameter is ignored if the admin disables non-music cutting
        /// </param>
        /// <param name="random">
        /// Randomize id list.
        /// If specified, it randomizes the id list before playback.
        /// This is done for every repeat iteration if there are multiple
        /// </param>
        /// <returns>MP3 data</returns>
        [HttpGet, ActionName("Send"), Produces("audio/mpeg", "text/plain")]
        public async Task<IActionResult> SendAsync(
            [FromRoute] string id,
            [FromQuery] int? buffer,
            [FromQuery] int? repeat,
            [FromQuery] string? key,
            [FromQuery] ApiBoolEnum? stream = ApiBoolEnum.N,
            [FromQuery] ApiBoolEnum? raw = ApiBoolEnum.N,
            [FromQuery] ApiBoolEnum? random = ApiBoolEnum.N)
        {
            if (Settings == null)
            {
                return Error(new Exception("Settings object not present"));
            }
            //This parameter is already processed at this point
            if (!Guid.TryParse(key, out _))
            {
                key = null;
            }
            //We do not bind the model as parameter in the method because we want the oparameter documentation
            var model = new StreamOptionsModel(stream, buffer, repeat, random, raw);

            if (!model.IsValid())
            {
                throw new ArgumentException("The supplied arguments are invalid:" + Environment.NewLine +
                    "-> " + string.Join(Environment.NewLine + "-> ", model.GetValidationMessages()));
            }


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
            var requestCancelToken = HttpContext.RequestAborted;
            var applicationTerminationToken = _applicationLifetime.ApplicationStopping;
            var includeAds = ShouldPlayAds();
            var markAds = ShouldMarkAds();
            var currentAdType = AdTypeEnum.Intro;
            var mp3CacheHandler = _cacheService.GetHandler(CacheTypeEnum.MP3, Settings.CacheMp3Lifetime);
            var skipped = 0;
            Mp3CutTargetStreamConfigModel? outputStreams = null;

            var killOutput = delegate ()
            {
                _logger.LogInformation("Connection gone");
                var os = outputStreams;
                os?.SetTimeout(false);
            };

            requestCancelToken.Register(killOutput);
            applicationTerminationToken.Register(killOutput);
            _logger.LogInformation("Preparing response for {count} ids", ids.Length);

            for (var iteration = 0; iteration < model.Repeat; iteration++)
            {
                if (model.Random)
                {
                    ids = Tools.Shuffle(ids) ?? throw null!;
                }
                foreach (var ytid in ids)
                {
                    //Stop streaming if the client is gone or the application has been locked
                    if (_lockService.Locked ||
                        requestCancelToken.IsCancellationRequested ||
                        applicationTerminationToken.IsCancellationRequested)
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
                    FileStream? cacheStream = null;
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

                    YoutubeDlResultModel? details;
                    string? url;
                    try
                    {
                        details = await _youtubeDlService.GetAudioDetails(ytid);
                        url = details.Url;
                        if (Settings.MaxVideoDuration > 0 && details.Duration > Settings.MaxVideoDuration)
                        {
                            throw new YoutubeDlException("Video exceeds permitted duration. " +
                                $"{TimeSpan.FromSeconds(details.Duration)} > {TimeSpan.FromSeconds(Settings.MaxVideoDuration)}");
                        }
                    }
                    catch (YoutubeDlException ex)
                    {
                        //If this is the only id, return an error to the client.
                        //Otherwise we just skip it silently
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
            if (Settings == null)
            {
                throw new InvalidOperationException("Settings object is not set");
            }
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
            if (Settings == null)
            {
                throw new InvalidOperationException("Settings object is not set");
            }
            if (CurrentUser == null)
            {
                return true;
            }
            return !CurrentUser.DisableAds &&
                (Settings.AdminAds || !CurrentUser.Roles.HasFlag(UserRoles.Administrator));
        }

        private bool ShouldMarkAds()
        {
            if (Settings == null)
            {
                throw new InvalidOperationException("Settings object is not set");
            }
            return Settings.MarkAds;
        }
    }
}
