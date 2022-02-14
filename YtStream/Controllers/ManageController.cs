using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YtStream.Models;

namespace YtStream.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class ManageController : BaseController
    {
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult ChangeLock()
        {
            if (Settings != null)
            {
                Startup.Locked = !Startup.Locked;
            }
            return RedirectToAction("Config");
        }

        public IActionResult ConfigSaved()
        {
            return View(Settings);
        }

        [HttpGet, ActionName("Config")]
        public IActionResult ConfigGet()
        {
            return View(Settings);
        }

        [HttpPost, ActionName("Config"), ValidateAntiForgeryToken]
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
            Startup.ApplySettings(model);
            return RedirectToAction("ConfigSaved");
        }
    }
}
