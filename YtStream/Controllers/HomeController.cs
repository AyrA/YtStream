using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using YtStream.Models;
using YtStream.Services;
using YtStream.Services.Accounts;
using YtStream.Services.YT;

namespace YtStream.Controllers
{
    public class HomeController : BaseController
    {
        private readonly YtApiService _ytApiService;
        private readonly YtCacheService _ytCacheService;

        public HomeController(ConfigService config, UserManagerService userManager, YtApiService ytApiService, YtCacheService ytCacheService) : base(config, userManager)
        {
            _ytApiService = ytApiService;
            _ytCacheService = ytCacheService;
        }

        public IActionResult Index()
        {
            return View(Settings);
        }

        public IActionResult Builder()
        {
            RequireSettings();

            if (string.IsNullOrEmpty(Settings.YtApiKey))
            {
                return NotFound();
            }
            if (Settings.RequireAccount && !IsAuthenticated)
            {
                return RequestLogin();
            }
            var vm = new BuilderViewModel();
            if (CurrentUser?.ApiKeys != null)
            {
                foreach (var k in CurrentUser.ApiKeys)
                {
                    vm.StreamKeys.Add(k.Key, k.Name!);
                }
            }
            return View(vm);
        }

        public async Task<IActionResult> Player(string playlist, Guid? key)
        {
            RequireSettings();

            if (string.IsNullOrEmpty(Settings.YtApiKey))
            {
                return NotFound();
            }
            if (Settings.RequireAccount && !IsAuthenticated && key.HasValue)
            {
                if (!SetApiUser(key.Value))
                {
                    return Forbid();
                }
            }

            if (!string.IsNullOrEmpty(playlist))
            {
                if (Settings.RequireAccount && !IsAuthenticated)
                {
                    return RequestLogin();
                }
                var plId = Tools.ExtractPlaylistFromUrl(playlist);
                //Redirect the user to the correct URL if the playlist Id doesn't matches the original input
                if (plId != playlist)
                {
                    return RedirectToAction("Player", new
                    {
                        playlist = plId,
                        key
                    });
                }

                var cacheKey = $"Player-{plId}";

                if (_ytCacheService.Get(cacheKey) is PlayerViewModel obj)
                {
                    if (key.HasValue)
                    {
                        obj.StreamKey = key.Value;
                    }
                    return View(obj);
                }

                if (!Tools.IsYoutubePlaylist(plId))
                {
                    return RedirectWithMessage("Player", "Invalid playlist id format", false);
                }
                //Extract playlist and video information
                var pl = await _ytApiService.GetPlaylistInfoAsync(plId)
                    ?? throw new Exception("Playlist not found or not public");
                var videos = await _ytApiService.GetPlaylistItemsAsync(plId)
                    ?? throw new Exception("Failed to get playlist videos");
                if (videos.Length == 0)
                {
                    return RedirectWithMessage("Player", "This playlist is empty", false);
                }
                var vm = new PlayerViewModel(pl, videos.Select(m => m.ToApiModel()).ToArray());
                if (key.HasValue)
                {
                    vm.StreamKey = key.Value;
                }
                _ytCacheService.Set(cacheKey, vm, TimeSpan.FromHours(1));
                return View(vm);
            }

            return View();
        }

        public IActionResult Info()
        {
            var Base = string.Format("http{0}://{1}", Request.IsHttps ? "s" : "", Request.Host);
            ViewBag.Host = Base;
            return View(Settings);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                Status = HttpContext.Response.StatusCode
            });
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Exception()
        {
            var pathFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            var exception = pathFeature?.Error;
            int statusCode = 500;
            var model = new ErrorViewModel
            {
                Status = statusCode,
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                Error = exception,
                //CurrentUser is not null if IsAuthenticated is true
                ShowDetails = IsAuthenticated && CurrentUser!.Roles.HasFlag(Enums.UserRoles.Administrator)
            };
            //_logger.LogError(exception, "Server error");
            return View("Error", model);
        }
    }
}
