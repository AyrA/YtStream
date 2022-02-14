using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using YtStream.Accounts;
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

        public IActionResult AccountList()
        {
            return View(UserManager.GetUsers());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult AccountDisable(string Username)
        {
            return ChangeUserEnabled(UserManager.GetUser(Username), false);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult AccountEnable(string Username)
        {
            return ChangeUserEnabled(UserManager.GetUser(Username), true);
        }

        [HttpPost, ActionName("ChangeLock"), ValidateAntiForgeryToken]
        public IActionResult ChangeLockPost()
        {
            if (Settings != null)
            {
                Startup.Locked = !Startup.Locked;
            }
            return RedirectToAction("ChangeLock");
        }

        [HttpGet, ActionName("ChangeLock")]
        public IActionResult ChangeLockGet()
        {
            return View();
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

        private IActionResult ChangeUserEnabled(AccountInfo Acc, bool State)
        {
            try
            {
                if (Acc == null)
                {
                    throw new ArgumentNullException(nameof(Acc), "User not found");
                }
                if (Acc == CurrentUser)
                {
                    throw new InvalidOperationException($"Cannot {(State ? "enable" : "disable")} current user");
                }
            }
            catch (Exception ex)
            {
                return View("Error", new ErrorViewModel(ex));
            }
            Acc.Enabled = State;
            UserManager.Save();
            return RedirectToAction("AccountList");
        }
    }
}
