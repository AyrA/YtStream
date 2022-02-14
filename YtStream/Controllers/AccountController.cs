using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using YtStream.Accounts;
using YtStream.Models;

namespace YtStream.Controllers
{
    [Authorize]
    public class AccountController : BaseController
    {
        private readonly ILogger _logger;

        public AccountController(ILogger<StreamController> Logger)
        {
            _logger = Logger;
        }

        public IActionResult Index()
        {
            return View(CurrentUser);
        }


        [HttpPost, ActionName("DeleteAccount"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAccountPost(string Password)
        {
            if (CurrentUser.CheckPassword(Password))
            {
                var CanDelete = UserManager.CanDeleteOrDisable(CurrentUser.Username);
                if (CanDelete)
                {
                    UserManager.DeleteUser(CurrentUser.Username);
                    UserManager.Save();
                    await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    _logger.LogInformation("User deleted: {0}", CurrentUser.Username);
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    ViewBag.ErrMsg = "Cannot delete the last administrator";
                }
            }
            else
            {
                ViewBag.ErrMsg = "Invalid password";
            }
            return View();
        }

        [HttpGet, ActionName("DeleteAccount")]
        public IActionResult DeleteAccountGet()
        {
            ViewBag.CanDelete = UserManager.CanDeleteOrDisable(CurrentUser.Username);
            return View();
        }

        [HttpPost, ActionName("ChangePassword"), ValidateAntiForgeryToken]
        public IActionResult ChangePasswordPost(PasswordChangeModel model)
        {
            ViewBag.Changed = false;
            if (CurrentUser.CheckPassword(model.OldPassword))
            {
                if (UserManager.Rules.IsComplexPassword(model.NewPassword))
                {
                    if (model.NewPassword == model.OldPassword)
                    {
                        CurrentUser.SetPassword(model.NewPassword);
                        ViewBag.Changed = true;
                        _logger.LogInformation("Password change for {0}", CurrentUser.Username);
                        return View();
                    }
                    else
                    {
                        ViewBag.ErrMsg = "New passwords do not match";
                    }
                }
                else
                {
                    ViewBag.ErrMsg = "New password is not long and complex enough";
                }
            }
            else
            {
                ViewBag.ErrMsg = "Old password did not match";
            }
            return View();
        }

        [HttpGet, ActionName("ChangePassword")]
        public IActionResult ChangePasswordGet()
        {
            return View();
        }

        [HttpPost, ActionName("ChangeName"), ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeNamePost(string Username)
        {
            ViewBag.Changed = false;
            var TestUser = UserManager.GetUser(Username);
            if (TestUser == null)
            {
                var OldUser = CurrentUser.Username;
                ViewBag.Changed = true;
                CurrentUser.Username = Username;
                UserManager.Save();
                _logger.LogInformation("Username change: {0} --> {1}", OldUser, Username);
                //Change username by signing in again
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, CurrentUser.GetIdentity());
            }
            else
            {
                ViewBag.ErrMsg = "This user name already exists";
            }
            return View();
        }

        [HttpGet, ActionName("ChangeName")]
        public IActionResult ChangeNameGet()
        {
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult DeleteKey(Guid Key)
        {
            if (Key != Guid.Empty)
            {
                CurrentUser.RemoveKey(Key);
                UserManager.Save();
            }
            return RedirectToAction("ManageKeys");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult CreateKey(string KeyName)
        {
            if (!string.IsNullOrEmpty(KeyName))
            {
                if (CurrentUser.ApiKeys.Length < UserManager.MaxKeysPerUser)
                {
                    CurrentUser.AddKey(new UserApiKey() { Name = KeyName });
                    UserManager.Save();
                }
            }
            return RedirectToAction("ManageKeys");
        }

        public IActionResult ManageKeys()
        {
            return View();
        }

        [AllowAnonymous, HttpPost, ActionName("Register"), ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterPost(string userName, string password, string passwordRepeat)
        {
            if (AllowRegister())
            {
                if (password == passwordRepeat)
                {
                    AccountInfo NewUser;
                    try
                    {
                        NewUser = UserManager.AddUser(userName, password, UserManager.HasUsers ? UserRoles.User : UserRoles.User | UserRoles.Administrator);
                    }
                    catch (Exception ex)
                    {
                        ViewBag.ErrMsg = ex.Message;
                        return View();
                    }
                    _logger.LogInformation("User registered: {0}", NewUser.Username);
                    if (!User.Identity.IsAuthenticated)
                    {
                        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, NewUser.GetIdentity());
                    }
                    return RedirectToAction("Index");
                }
                else
                {
                    ViewBag.ErrMsg = "Passwords do not match";
                }
                return View();
            }
            return RedirectToAction("Login");
        }

        [AllowAnonymous, HttpGet, ActionName("Register")]
        public IActionResult RegisterGet()
        {
            if (AllowRegister())
            {
                return View();
            }
            return RedirectToAction("Login");
        }

        [AllowAnonymous, HttpGet, ActionName("Login")]
        public IActionResult LoginGet()
        {
            if (!UserManager.HasUsers)
            {
                return RedirectToAction("Register");
            }
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [AllowAnonymous, HttpPost, ActionName("Login"), ValidateAntiForgeryToken]
        public async Task<IActionResult> LoginPost(string userName, string password)
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            if (!string.IsNullOrEmpty(userName) && string.IsNullOrEmpty(password))
            {
                return RedirectToAction("Login");
            }
            var Account = UserManager.GetUser(userName);
            if (Account != null && Account.Enabled && Account.CheckPassword(password))
            {
                _logger.LogInformation("User authenticated: {0}", Account.Username);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, Account.GetIdentity());
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        [Authorize, HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            if (CurrentUser != null)
            {
                _logger.LogInformation("User logout: {0}", CurrentUser.Username);
            }
            await HttpContext.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }

        private bool AllowRegister()
        {
            return Settings.PublicRegistration ||
                !UserManager.HasUsers ||
                (User.Identity.IsAuthenticated && User.IsInRole(UserRoles.Administrator.ToString()));
        }
    }
}
