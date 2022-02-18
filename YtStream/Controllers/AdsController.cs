using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YtStream.Ad;
using YtStream.Models;

namespace YtStream.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class AdsController : BaseController
    {
        public const long ReqLimit = 50_000_000;

        private ILogger _logger;
        private readonly CacheHandler Handler;

        public AdsController(ILogger<AdsController> Logger) : base()
        {
            _logger = Logger;
            Handler = Cache.GetHandler(Cache.CacheType.AudioSegments, 0);
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Play(string id)
        {
            try
            {
                return File(Handler.ReadFile(id), "audio/mpeg");
            }
            catch (Exception ex)
            {
                return View("Error", new ErrorViewModel(ex));
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Add(AdType Type, string Filename)
        {
            try
            {
                if (!Enum.IsDefined(typeof(AdType), Type))
                {
                    throw new ArgumentException($"Invalid ad type: {Type}");
                }
                if (string.IsNullOrWhiteSpace(Filename))
                {
                    throw new ArgumentException($"'{nameof(Filename)}' cannot be null or whitespace.", nameof(Filename));
                }
                if (!Handler.HasFileInCache(Filename))
                {
                    throw new FileNotFoundException($"{Filename} not found in server library");
                }
                var AdHandler = new Ads();
                AdHandler.AddFile(Filename, Type);
                return RedirectWithMessage("Index", $"File added to {Type}");
            }
            catch (Exception ex)
            {
                return View("Error", new ErrorViewModel(ex));
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Remove(string Filename, AdType Type)
        {
            try
            {
                if (!Enum.IsDefined(typeof(AdType), Type))
                {
                    throw new ArgumentException($"Invalid ad type: {Type}");
                }
                if (string.IsNullOrWhiteSpace(Filename))
                {
                    throw new ArgumentException($"'{nameof(Filename)}' cannot be null or whitespace.", nameof(Filename));
                }
                var AdHandler = new Ads();
                AdHandler.RemoveFile(Filename, Type);
                return RedirectWithMessage("Index", $"File removed from category '{Type}'");
            }
            catch (Exception ex)
            {
                return View("Error", new ErrorViewModel(ex));
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Delete(string Filename, AdType Type)
        {
            try
            {
                if (!Enum.IsDefined(typeof(AdType), Type))
                {
                    throw new ArgumentException($"Invalid ad type: {Type}");
                }
                if (string.IsNullOrWhiteSpace(Filename))
                {
                    throw new ArgumentException($"'{nameof(Filename)}' cannot be null or whitespace.", nameof(Filename));
                }
                var AdHandler = new Ads();
                AdHandler.DeleteFile(Filename);
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
            var Uploaded = new List<string>();
            using (var FFmpeg = new Converter(Settings.FfmpegPath, null))
            {
                FFmpeg.AudioFrequency = Settings.AudioFrequency;
                FFmpeg.AudioRate = Settings.AudioBitrate;
                foreach (var F in Request.Form.Files)
                {
                    _logger.LogDebug("Processing ad: {0}", F.FileName);
                    if (F.Length == 0)
                    {
                        _logger.LogWarning("Empty ad uploaded: {0}", F.FileName);
                        continue;
                    }
                    var Dest = Handler.GetNoConfictName(Path.ChangeExtension(F.FileName, ".mp3"));
                    using (var MS = new MemoryStream())
                    {
                        await F.CopyToAsync(MS);
                        MS.Position = 0;
                        _logger.LogDebug("Copied {0} into memory", F.FileName);
                        if (F.FileName.ToLower().EndsWith(".mp3"))
                        {
                            _logger.LogDebug("Check if {0} is good MP3", F.FileName);
                            try
                            {
                                var Header1 = MP3.MP3.GetFirstHeader(MS);
                                MS.Position = 0;
                                if (MP3.MP3.IsCBR(MS))
                                {
                                    if (Header1.AudioFrequency == Settings.AudioFrequency && Header1.AudioRate == Settings.AudioBitrate)
                                    {
                                        _logger.LogDebug("{0} is good MP3", F.FileName);
                                        //File OK as is. Copy to cache
                                        using (var Target = Handler.WriteFile(Dest))
                                        {
                                            await MP3.MP3Cut.FilterMp3Async(MS, Target);
                                        }
                                        Uploaded.Add(Dest);
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
                        var ConvertTask = FFmpeg.ConvertToMp3(MS);
                        using (var Target = new MemoryStream())
                        {
                            _logger.LogDebug("Begin conversion of {0}", F.FileName);
                            using (ConvertTask.StandardOutputStream)
                            {
                                var OutputTask = ConvertTask.StandardOutputStream.CopyToAsync(Target);
                                await ConvertTask.CopyStreamResult;
                                //Close input stream or ffmpeg will never exit as it waits for more data
                                ConvertTask.StandardInputStream.Close();
                                await OutputTask;
                            }
                            _logger.LogDebug("Waiting for source to drain");
                            await ConvertTask.CopyStreamResult;
                            _logger.LogInformation("{0} converted. Result is {1} bytes", F.FileName, Target.Length);
                            Target.Position = 0;
                            try
                            {
                                MP3.MP3.GetFirstHeader(Target);
                            }
                            catch
                            {
                                _logger.LogWarning("{0} failed to convert. Output lacks MP3 frames", F.FileName);
                                //File is invalid
                                continue;
                            }
                            Target.Position = 0;
                            using (var FS = Handler.WriteFile(Dest))
                            {
                                await MP3.MP3Cut.FilterMp3Async(Target, FS);
                            }
                            Uploaded.Add(Dest);
                            _logger.LogInformation("{0} added to cache", Dest);
                        }
                    }
                }
            }
            return RedirectWithMessage("Index", "Files uploaded and processed: " + string.Join(", ", Uploaded));
        }
    }
}
