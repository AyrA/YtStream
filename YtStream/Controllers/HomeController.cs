using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using YtStream.Models;

namespace YtStream.Controllers
{
    public class HomeController : BaseController
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View(Settings);
        }

        public IActionResult Info()
        {
            var Base = string.Format("http{0}://{1}", Request.IsHttps ? "s" : "", Request.Host);
            ViewBag.Host = Base;
            return View(Settings);
        }

        [Authorize(Roles = "Administrator"), HttpPost, ValidateAntiForgeryToken]
        public IActionResult ChangeLock()
        {
            if (Settings != null)
            {
                Startup.Locked = !Startup.Locked;
            }
            return RedirectToAction("Config");
        }

        [Authorize(Roles = "Administrator")]
        public IActionResult ConfigSaved()
        {
            return View(Settings);
        }

        [Authorize(Roles = "Administrator"), HttpGet, ActionName("Config")]
        public IActionResult ConfigGet()
        {
            return View(Settings);
        }

        [Authorize(Roles = "Administrator"), HttpPost, ActionName("Config"), ValidateAntiForgeryToken]
        public IActionResult ConfigPost(ConfigModel model)
        {
            ViewBag.ErrorMessage = "";
            if (model == null)
            {
                return RedirectToAction("Config");
            }
            if (!model.IsValid())
            {
                return View(model);
            }
            model.Save();
            if (model.UseCache)
            {
                Cache.SetBaseDirectory(model.CachePath);
            }
            return RedirectToAction("ConfigSaved");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
