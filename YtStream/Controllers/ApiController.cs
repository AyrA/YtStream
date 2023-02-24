using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Linq;
using System.Threading.Tasks;
using YtStream.Services;
using YtStream.Services.Accounts;
using YtStream.Services.YT;

namespace YtStream.Controllers
{
    public class ApiController : BaseController
    {
        private readonly YtCacheService _ytCacheService;
        private readonly SponsorBlockCacheService _sponsorBlockCacheService;
        private readonly YtApiService _ytApiService;

        public ApiController(ConfigService config, UserManagerService userManager,
            YtCacheService ytCacheService,
            SponsorBlockCacheService sponsorBlockCacheService,
            YtApiService ytApiService) : base(config, userManager)
        {
            _ytCacheService = ytCacheService;
            _sponsorBlockCacheService = sponsorBlockCacheService;
            _ytApiService = ytApiService;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (Settings.RequireAccount && !User.Identity.IsAuthenticated)
            {
                context.Result = RedirectToAction("Login", "Account", new { returnUrl = HttpContext.Request.Path });
                return;
            }
            base.OnActionExecuting(context);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Info(string id)
        {
            if (string.IsNullOrEmpty(Settings.YtApiKey))
            {
                try
                {
                    throw new InvalidOperationException("Youtube API key has not been configured");
                }
                catch (Exception ex)
                {
                    return Error(ex);
                }
            }
            var data = _ytCacheService.Get(id, out bool found);
            if (Tools.IsYoutubePlaylist(id))
            {
                if (found || IsHead())
                {
                    return Json(data);
                }
                var Result = await _ytApiService.GetPlaylistInfoAsync(id);
                if (Result == null)
                {
                    _ytCacheService.Set(id, null, TimeSpan.FromDays(1));
                    Tools.SetExpiration(Response, TimeSpan.FromDays(1));
                    return NotFound();
                }
                _ytCacheService.Set(id, Result, TimeSpan.FromHours(1));
                Tools.SetExpiration(Response, TimeSpan.FromHours(1));
                return Json(Result.Select(m => new
                {
                    title = m.Title,
                    id = m.ResourceId.VideoId
                }));
            }
            else if (Tools.IsYoutubeId(id))
            {
                if (found || IsHead())
                {
                    return Json(data);
                }
                var Result = await _ytApiService.GetVideoInfo(id);
                if (Result == null)
                {
                    _ytCacheService.Set(id, null, TimeSpan.FromDays(1));
                    Tools.SetExpiration(Response, TimeSpan.FromDays(1));
                    return NotFound();
                }
                var Obj = new
                {
                    title = Result.Title,
                    id = id
                };
                _ytCacheService.Set(id, Obj, TimeSpan.FromHours(1));
                Tools.SetExpiration(Response, TimeSpan.FromHours(1));
                return Json(Obj);
            }
            return BadRequest("Supplied argument is not a valid video or playlist identifier");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Ranges(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound("No id specified");
            }
            if (Tools.IsYoutubeId(id))
            {
                //The actual data for head request doesn't matters
                if (IsHead())
                {
                    return Json(null);
                }
                return Json(await _sponsorBlockCacheService.GetRangesAsync(id));
            }
            return BadRequest("Invalid youtube id");
        }
    }
}
