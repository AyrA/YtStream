using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Threading.Tasks;
using YtStream.Models;

namespace YtStream
{
    public class BaseController : Controller
    {
        public readonly ConfigModel Settings;

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
                var Acc = UserManager.GetUser(User.Identity.Name);
                //Terminate user session if the user is no longer existing or enabled
                if (Acc == null || !Acc.Enabled)
                {
                    await HttpContext.SignOutAsync();
                    context.Result = RedirectToAction("Index", "Home");
                    return;
                }
            }
            await base.OnActionExecutionAsync(context, next);
        }
    }
}
