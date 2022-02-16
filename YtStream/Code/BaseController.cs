using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Threading.Tasks;
using YtStream.Accounts;
using YtStream.Models;

namespace YtStream
{
    public class BaseController : Controller
    {
        public ConfigModel Settings { get; }

        public AccountInfo CurrentUser { get; private set; }

        public string CookieMessage { get; private set; }

        public BaseController()
        {
            try
            {
                Settings = ConfigModel.Load();
            }
            catch
            {
                Settings = null;
            }
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            CookieMessage = null;
            if (User.Identity.IsAuthenticated)
            {
                CurrentUser = UserManager.GetUser(User.Identity.Name);
                //Terminate user session if the user is no longer existing or enabled
                if (CurrentUser == null || !CurrentUser.Enabled)
                {
                    await HttpContext.SignOutAsync();
                    context.Result = RedirectToAction("Index", "Home");
                    return;
                }
            }
            ViewBag.User = CurrentUser;
            ViewBag.Settings = Settings;
            ViewBag.CookieMessage = CookieMessage = HttpContext.Request.Cookies["status"];
            if (!string.IsNullOrEmpty(CookieMessage))
            {
                HttpContext.Response.Cookies.Delete("status");
            }
            await base.OnActionExecutionAsync(context, next);
        }

        public void SetApiUser(Guid ApiKey)
        {
            CurrentUser = UserManager.GetUser(ApiKey);
        }

        public IActionResult RedirectWithMessage(string Action, string Message, object RouteData = null)
        {
            HttpContext.Response.Cookies.Append("status", Message);
            return RedirectToAction(Action, RouteData);
        }

        public IActionResult RedirectWithMessage(string Action, string Controller, string Message, object RouteData = null)
        {
            HttpContext.Response.Cookies.Append("status", Message);
            return RedirectToAction(Action, Controller, RouteData);
        }
    }
}
