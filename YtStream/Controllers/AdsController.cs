using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YtStream.Enums;
using YtStream.Models;
using YtStream.Services;
using YtStream.Services.Accounts;
using YtStream.Services.Mp3;

namespace YtStream.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class AdsController : BaseController
    {
        public const long ReqLimit = 50_000_000;

        private readonly ILogger _logger;
        private readonly CacheService _cacheService;
        private readonly AdsService _adsService;
        private readonly Mp3ConverterService _mp3ConverterService;
        private readonly Mp3InfoService _mp3InfoService;
        private readonly Mp3CutService _mp3CutService;
        private readonly CacheHandler _cacheHandler;

        public AdsController(ILogger<AdsController> Logger, ConfigService config,
            UserManagerService userManager, CacheService cacheService,
            AdsService adsService, Mp3ConverterService mp3ConverterService,
            Mp3InfoService mp3InfoService, Mp3CutService mp3CutService) : base(config, userManager)
        {
            _logger = Logger;
            _cacheService = cacheService;
            _adsService = adsService;
            _mp3ConverterService = mp3ConverterService;
            _mp3InfoService = mp3InfoService;
            _mp3CutService = mp3CutService;
            _cacheHandler = cacheService.GetHandler(CacheTypeEnum.AudioSegments, 0);
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Play(string id)
        {
            try
            {
                return File(_cacheHandler.ReadFile(id), "audio/mpeg");
            }
            catch (Exception ex)
            {
                return View("Error", new ErrorViewModel(ex));
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Add(AdTypeEnum Type, string Filename)
        {
            try
            {
                if (!Enum.IsDefined(typeof(AdTypeEnum), Type))
                {
                    throw new ArgumentException($"Invalid ad type: {Type}");
                }
                if (string.IsNullOrWhiteSpace(Filename))
                {
                    throw new ArgumentException($"'{nameof(Filename)}' cannot be null or whitespace.", nameof(Filename));
                }
                if (!_cacheHandler.HasFileInCache(Filename))
                {
                    throw new FileNotFoundException($"{Filename} not found in server library");
                }
                _adsService.AddFile(Filename, Type);
                return RedirectWithMessage("Index", $"File added to {Type}");
            }
            catch (Exception ex)
            {
                return View("Error", new ErrorViewModel(ex));
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Remove(string Filename, AdTypeEnum Type)
        {
            try
            {
                if (!Enum.IsDefined(typeof(AdTypeEnum), Type))
                {
                    throw new ArgumentException($"Invalid ad type: {Type}");
                }
                if (string.IsNullOrWhiteSpace(Filename))
                {
                    throw new ArgumentException($"'{nameof(Filename)}' cannot be null or whitespace.", nameof(Filename));
                }
                _adsService.RemoveFile(Filename, Type);
                return RedirectWithMessage("Index", $"File removed from category '{Type}'");
            }
            catch (Exception ex)
            {
                return View("Error", new ErrorViewModel(ex));
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Delete(string Filename, AdTypeEnum Type)
        {
            try
            {
                if (!Enum.IsDefined(typeof(AdTypeEnum), Type))
                {
                    throw new ArgumentException($"Invalid ad type: {Type}");
                }
                if (string.IsNullOrWhiteSpace(Filename))
                {
                    throw new ArgumentException($"'{nameof(Filename)}' cannot be null or whitespace.", nameof(Filename));
                }
                _adsService.DeleteFile(Filename);
                return RedirectWithMessage("Index", $"File deleted from the system");
            }
            catch (Exception ex)
            {
                return View("Error", new ErrorViewModel(ex));
            }
        }

        [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(ReqLimit)]
        public async Task<IActionResult> Upload()
        {
            //For each uploaded file:
            //1 . Check if it's an MP3, and if so, if it's in the currently configured format
            //2a. If the check was successful, just filter the file and store in the cache
            //2b. If the check failed, convert using ffmpeg
            //3 . If conversion succeeded, filter and store in the cache

            if (Request.Form.Files.Count == 0)
            {
                return View("Error", new ErrorViewModel(new ArgumentException("No file uploaded")));
            }
            var uploadedFiles = new List<string>();
            foreach (var F in Request.Form.Files)
            {
                _logger.LogDebug("Processing ad: {0}", F.FileName);
                if (F.Length == 0)
                {
                    _logger.LogWarning("Empty ad uploaded: {0}", F.FileName);
                    continue;
                }
                var destPath = _cacheHandler.GetNoConfictName(Path.ChangeExtension(F.FileName, ".mp3"));
                using var MS = new MemoryStream();
                await F.CopyToAsync(MS);
                MS.Position = 0;
                _logger.LogDebug("Copied {0} into memory", F.FileName);
                if (F.FileName.ToLower().EndsWith(".mp3"))
                {
                    _logger.LogDebug("Check if {0} is good MP3", F.FileName);
                    try
                    {
                        var Header1 = _mp3InfoService.GetFirstHeader(MS);
                        MS.Position = 0;
                        if (_mp3InfoService.IsCBR(MS))
                        {
                            if (Header1.AudioFrequency == Settings.AudioFrequency && Header1.AudioRate == Settings.AudioBitrate)
                            {
                                _logger.LogDebug("{0} is good MP3", F.FileName);
                                //File OK as is. Copy to cache
                                using (var cacheTarget = _cacheHandler.WriteFile(destPath))
                                {
                                    await _mp3CutService.FilterMp3Async(MS, cacheTarget);
                                }
                                uploadedFiles.Add(destPath);
                                _logger.LogInformation("{0} was copied as-is", F.FileName);
                                continue;
                            }
                        }
                        _logger.LogInformation("{0} is different from current MP3 settings. Force conversion", F.FileName);
                    }
                    catch
                    {
                        //NOOP. Invalid MP3. Try regular conversion
                    }
                }
                //If we're here the MP3 is either not in the required format, or it's not an MP3
                MS.Position = 0;
                var convertTask = _mp3ConverterService.ConvertToMp3(MS);
                using var convertTarget = new MemoryStream();
                _logger.LogDebug("Begin conversion of {0}", F.FileName);
                using (convertTask.StandardOutputStream)
                {
                    var outputTask = convertTask.StandardOutputStream.CopyToAsync(convertTarget);
                    await convertTask.CopyStreamResult;
                    //Close input stream or ffmpeg will never exit as it waits for more data
                    convertTask.StandardInputStream.Close();
                    await outputTask;
                }
                _logger.LogDebug("Waiting for source to drain");
                await convertTask.CopyStreamResult;
                _logger.LogInformation("{0} converted. Result is {1} bytes", F.FileName, convertTarget.Length);
                convertTarget.Position = 0;
                try
                {
                    _mp3InfoService.GetFirstHeader(convertTarget);
                }
                catch
                {
                    _logger.LogWarning("{0} failed to convert. Output lacks MP3 frames", F.FileName);
                    //File is invalid
                    continue;
                }
                convertTarget.Position = 0;
                using (var FS = _cacheHandler.WriteFile(destPath))
                {
                    await _mp3CutService.FilterMp3Async(convertTarget, FS);
                }
                uploadedFiles.Add(destPath);
                _logger.LogInformation("{0} added to cache", destPath);
            }

            return RedirectWithMessage("Index", "Files uploaded and processed: " + string.Join(", ", uploadedFiles));
        }
    }
}
