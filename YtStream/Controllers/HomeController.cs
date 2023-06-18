using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using YtStream.Models;
using YtStream.Services;
using YtStream.Services.Accounts;

namespace YtStream.Controllers
{
    public class HomeController : BaseController
    {
        public HomeController(ConfigService config, UserManagerService userManager) : base(config, userManager)
        {
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
                return RedirectToAction("Login", "Account", new { returnUrl = HttpContext.Request.Path });
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
