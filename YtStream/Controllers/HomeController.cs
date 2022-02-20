using Microsoft.AspNetCore.Mvc;
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

        public IActionResult Index()
        {
            return View(Settings);
        }

        public IActionResult Builder()
        {
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
