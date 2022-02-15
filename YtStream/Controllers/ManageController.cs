﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using YtStream.Accounts;
using YtStream.Models;

namespace YtStream.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class ManageController : BaseController
    {
        private readonly ILogger _logger;

        public ManageController(ILogger<StreamController> Logger)
        {
            _logger = Logger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Backup(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                ViewBag.Msg = CookieMessage;
                return View();
            }
            string Filename, Fakename;
            try
            {
                switch (id)
                {
                    case "accounts":
                        Filename = System.IO.Path.Combine(Startup.BasePath, UserManager.FileName);
                        Fakename = "backup.ytacc";
                        break;
                    case "config":
                        Filename = System.IO.Path.Combine(Startup.BasePath, ConfigModel.ConfigFileName);
                        Fakename = "backup.ytconf";
                        break;
                    default:
                        throw new ArgumentException("Unknown backup type");
                }
            }
            catch (Exception ex)
            {
                return View("Error", new ErrorViewModel(ex));
            }
            if (!string.IsNullOrEmpty(Filename))
            {
                using (var FS = System.IO.File.OpenRead(Filename))
                {
                    byte[] Result = await Brotli.Compress(FS);
                    HttpContext.Response.Headers.Add("Content-Disposition", $"attachment; filename=\"{Fakename}\"");
                    return File(Result, "application/octet-stream");
                }
            }
            return NotFound();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(string id)
        {
            try
            {
                if (Request.Form.Files.Count != 1)
                {
                    throw new Exception($"Expected 1 file, but got {Request.Form.Files.Count}");
                }
                if (id == null)
                {
                    throw new Exception("No upload type specified");
                }
                switch (id)
                {
                    case "accounts":
                        return await RestoreAccounts(Request.Form.Files[0]);
                    case "config":
                        return await RestoreConfig(Request.Form.Files[0]);
                    default:
                        throw new Exception("Unknown upload type");
                }
            }
            catch (Exception ex)
            {
                return View("Error", new ErrorViewModel(ex));
            }
        }

        [HttpGet]
        public IActionResult AccountList()
        {
            return View(UserManager.GetUsers());
        }

        [HttpGet]
        public IActionResult AccountEdit(string id)
        {
            AccountInfo Acc;
            try
            {
                Acc = GetAccount(id);
            }
            catch (Exception ex)
            {
                return View("Error", new ErrorViewModel(ex));
            }
            ViewBag.Status = HttpContext.Request.Cookies["status-success"];
            HttpContext.Response.Cookies.Delete("status-success");
            return View(Acc);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult AccountRename(string id, string username)
        {
            if (id != null && username != null && id.ToLower() == username.ToLower())
            {
                HttpContext.Response.Cookies.Append("status-success", "No change");
                return RedirectToAction("AccountEdit", new { id = username });
            }
            AccountInfo Acc;
            try
            {
                Acc = GetAccount(id);
                if (UserManager.GetUser(username) != null)
                {
                    throw new InvalidOperationException("An account with this name already exists");
                }
                if (!UserManager.IsValidUsername(username, false))
                {
                    throw new FormatException("Invalid user name");
                }
                Acc.Username = username;
                UserManager.Save();
            }
            catch (Exception ex)
            {
                return View("Error", new ErrorViewModel(ex));
            }
            HttpContext.Response.Cookies.Append("status-success", "Username changed");
            return RedirectToAction("AccountEdit", new { id = username });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult AccountAdd(string userName, string password, string passwordRepeat)
        {
            try
            {
                if (password == passwordRepeat)
                {
                    AccountInfo NewUser = UserManager.AddUser(userName, password, UserRoles.User);
                    _logger.LogInformation("User {0} registered by {1}", NewUser.Username, CurrentUser.Username);
                }
                else
                {
                    throw new ArgumentException("Passwords do not match");
                }
            }
            catch (Exception ex)
            {
                return View("Error", new ErrorViewModel(ex));
            }
            return RedirectToAction("AccountList");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult AccountDisable(string Username)
        {
            return ChangeUserEnabled(Username, false);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult AccountEnable(string Username)
        {
            return ChangeUserEnabled(Username, true);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult AccountPasswordChange(string id, string password, string passwordRepeat)
        {
            AccountInfo Acc;
            var PI = new UserPasswordRules();
            try
            {
                Acc = GetAccount(id);
                if (!PI.IsComplexPassword(password))
                {
                    throw new FormatException("Supplied password is not complex enough");
                }
                if (password != passwordRepeat)
                {
                    throw new ArgumentException("passwords do not match");
                }
                Acc.SetPassword(password);
                UserManager.Save();
            }
            catch (Exception ex)
            {
                return View("Error", new ErrorViewModel(ex));
            }
            HttpContext.Response.Cookies.Append("status-success", "Password changed");
            return RedirectToAction("AccountEdit", new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult AccountPermission(string id)
        {
            AccountInfo Acc;
            UserRoles Role = 0;
            try
            {
                if (!HttpContext.Request.HasFormContentType)
                {
                    return BadRequest();
                }
                Acc = GetAccount(id);
                var Roles = HttpContext.Request.Form["permission"];
                if (Roles.Count == 0)
                {
                    throw new InvalidOperationException("At least one role must be set");
                }
                foreach (var Str in Roles)
                {
                    var Parsed = (UserRoles)Enum.Parse(typeof(UserRoles), Str);
                    Role |= Parsed;
                    if (Parsed == UserRoles.Administrator)
                    {
                        Role |= UserRoles.User;
                    }
                }
                Acc.Roles = Role;
                UserManager.Save();
            }
            catch (Exception ex)
            {
                return View("Error", new ErrorViewModel(ex));
            }
            HttpContext.Response.Cookies.Append("status-success", "Permissions adjusted");
            return RedirectToAction("AccountEdit", new { id });
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

        private IActionResult ChangeUserEnabled(string Username, bool State)
        {
            AccountInfo Acc;
            try
            {
                Acc = GetAccount(Username, false);
            }
            catch (Exception ex)
            {
                return View("Error", new ErrorViewModel(ex));
            }
            Acc.Enabled = State;
            UserManager.Save();
            return RedirectToAction("AccountList");
        }

        private AccountInfo GetAccount(string Username, bool AllowSelf = false)
        {
            if (string.IsNullOrEmpty(Username))
            {
                throw new ArgumentNullException(nameof(Username), "User name not specified");
            }
            var Acc = UserManager.GetUser(Username);
            if (Acc == null)
            {
                throw new ArgumentException("User not found: " + nameof(Username));
            }
            if (!AllowSelf && Acc == CurrentUser)
            {
                throw new Exception(Username + " is the currently logged on user. " +
                    "To edit the current account, click on the user name in the top right corner.");
            }
            return Acc;
        }

        private async Task<IActionResult> RestoreConfig(IFormFile formFile)
        {
            using (var S = formFile.OpenReadStream())
            {
                var data = (await Brotli.Decompress(S)).Utf8().FromJson<ConfigModel>(true);
                if (!data.IsValid())
                {
                    throw new Exception(string.Join("\r\n", data.GetValidationMessages()));
                }
                data.Save();
                Startup.ApplySettings(data);
            }
            return RedirectWithMessage("Backup", "Settings imported");
        }

        private async Task<IActionResult> RestoreAccounts(IFormFile formFile)
        {
            using (var S = formFile.OpenReadStream())
            {
                var data = (await Brotli.Decompress(S)).Utf8().FromJson<AccountInfo[]>(true);
                if (data.Length < 1)
                {
                    throw new Exception("Imported user list is empty");
                }
                if (!data.Any(m => m.Enabled && m.Roles.HasFlag(UserRoles.Administrator)))
                {
                    throw new Exception("Imported user list has no enabled administrator");
                }
                var Invalid = data.FirstOrDefault(m => !m.IsValid());
                if (Invalid != null)
                {
                    throw new Exception("Imported user list has invalid entries. " +
                        $"First invalid user is '{Invalid.Username}'. Error: '{Invalid.GetValidationMessages()[0]}'");
                }
                await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(Startup.BasePath, UserManager.FileName), data.ToJson(true));
                UserManager.Reload();
            }
            return RedirectWithMessage("Backup", "User accounts imported");
        }
    }
}
