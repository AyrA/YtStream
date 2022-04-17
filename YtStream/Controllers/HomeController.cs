using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using YtStream.Models;

namespace YtStream.Controllers
{
    public class HomeController : BaseController
    {
        public HomeController()
        {
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var Action = ((string)RouteData.Values["action"]).ToLower();
            if (Action == "builder")
            {
                if (Settings.RequireAccount && !User.Identity.IsAuthenticated)
                {
                    context.Result = RedirectToAction("Login", "Account", new { returnUrl = HttpContext.Request.Path });
                    return;
                }
            }
            base.OnActionExecuting(context);
        }

        public IActionResult Index()
        {
            return View(Settings);
        }

        public IActionResult Builder()
        {
            if (string.IsNullOrEmpty(Settings.YtApiKey))
            {
                return NotFound();
            }
            if (Settings.RequireAccount && !User.Identity.IsAuthenticated)
            {

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
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
