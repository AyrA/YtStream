using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Threading.Tasks;
using YtStream.Models;

namespace YtStream
{
    public class BaseController : Controller
    {
        public ConfigModel Settings { get; }
        public AccountInfo CurrentUser { get; private set; }

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
            await base.OnActionExecutionAsync(context, next);
        }
    }
}
