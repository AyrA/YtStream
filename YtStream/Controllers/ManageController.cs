using Microsoft.AspNetCore.Mvc;

namespace YtStream.Controllers
{
    public class ManageController : BaseController
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
