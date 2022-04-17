using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace YtStream.Controllers
{
    public class ApiController : BaseController
    {
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
            var data = YT.YtCache.Get(id, out bool found);
            if (Tools.IsYoutubePlaylist(id))
            {
                if (found || IsHead())
                {
                    return Json(data);
                }
                var API = new YT.YtApi(Settings.YtApiKey);
                var Result = await API.GetPlaylistInfoAsync(id);
                if (Result == null)
                {
                    YT.YtCache.Set(id, null, TimeSpan.FromDays(1));
                    Tools.SetExpiration(Response, TimeSpan.FromDays(1));
                    return NotFound();
                }
                YT.YtCache.Set(id, Result, TimeSpan.FromHours(1));
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
                var API = new YT.YtApi(Settings.YtApiKey);
                var Result = await API.GetVideoInfo(id);
                if (Result == null)
                {
                    YT.YtCache.Set(id, null, TimeSpan.FromDays(1));
                    Tools.SetExpiration(Response, TimeSpan.FromDays(1));
                    return NotFound();
                }
                var Obj = new
                {
                    title = Result.Title,
                    id = id
                };
                YT.YtCache.Set(id, Obj, TimeSpan.FromHours(1));
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
                return Json(await SponsorBlockCache.GetRangesAsync(id, Settings));
            }
            return BadRequest("Invalid youtube id");
        }
    }
}
