using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using YtStream.Enums;
using YtStream.Models;
using YtStream.Models.Accounts;
using YtStream.Services;
using YtStream.Services.Accounts;

namespace YtStream.Controllers
{
#nullable disable
    [Authorize]
    public class AccountController : BaseController
    {
        private readonly ILogger _logger;

        public AccountController(ILogger<StreamController> Logger, ConfigService config,
            UserManagerService userManager) : base(config, userManager)
        {
            _logger = Logger;
        }

        public IActionResult Index()
        {
            return View(CurrentUser);
        }

        public IActionResult ChangeAdSetting()
        {
            if (CurrentUser.Roles.HasFlag(UserRoles.Administrator))
            {
                CurrentUser.DisableAds = !CurrentUser.DisableAds;
                _userManager.Save();
                return RedirectWithMessage("Index", "Ad settings changed", true);
            }
            return Forbid();
        }

        [HttpPost, ActionName("DeleteAccount"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAccountPost(string Password)
        {
            if (CurrentUser.CheckPassword(Password))
            {
                var CanDelete = _userManager.CanDeleteOrDisable(CurrentUser.Username);
                if (CanDelete)
                {
                    _userManager.DeleteUser(CurrentUser.Username);
                    _userManager.Save();
                    await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    _logger.LogInformation("User deleted: {username}", CurrentUser.Username);
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
            ViewBag.CanDelete = _userManager.CanDeleteOrDisable(CurrentUser.Username);
            return View();
        }

        [HttpPost, ActionName("ChangePassword"), ValidateAntiForgeryToken]
        public IActionResult ChangePasswordPost(PasswordChangeModel model)
        {
            ViewBag.Changed = false;
            if (CurrentUser.CheckPassword(model.OldPassword))
            {
                if (_userManager.Rules.IsComplexPassword(model.NewPassword))
                {
                    if (model.NewPassword == model.OldPassword)
                    {
                        CurrentUser.SetPassword(model.NewPassword);
                        ViewBag.Changed = true;
                        _logger.LogInformation("Password change for {username}", CurrentUser.Username);
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
            var TestUser = _userManager.GetUser(Username);
            if (TestUser == null)
            {
                var OldUser = CurrentUser.Username;
                ViewBag.Changed = true;
                CurrentUser.Username = Username;
                _userManager.Save();
                _logger.LogInformation("Username change: {old} --> {new}", OldUser, Username);
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
                _userManager.Save();
            }
            return RedirectToAction("ManageKeys");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult CreateKey(string KeyName)
        {
            if (!string.IsNullOrEmpty(KeyName))
            {
                if (CurrentUser.ApiKeys.Length < _userManager.MaxKeysPerUser)
                {
                    CurrentUser.AddKey(new UserApiKeyModel() { Name = KeyName });
                    _userManager.Save();
                }
            }
            return RedirectToAction("ManageKeys");
        }

        public IActionResult ManageKeys()
        {
            return View();
        }

        public IActionResult ManageFavs()
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
                    AccountInfoModel NewUser;
                    try
                    {
                        NewUser = _userManager.AddUser(userName, password, _userManager.HasUsers ? UserRoles.User : UserRoles.User | UserRoles.Administrator);
                    }
                    catch (Exception ex)
                    {
                        ViewBag.ErrMsg = ex.Message;
                        return View();
                    }
                    _logger.LogInformation("User registered: {username}", NewUser.Username);
                    if (!IsAuthenticated)
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
            if (!_userManager.HasUsers)
            {
                return RedirectToAction("Register");
            }
            if (IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [AllowAnonymous, HttpPost, ActionName("Login"), ValidateAntiForgeryToken]
        public async Task<IActionResult> LoginPost(string userName, string password, string returnUrl)
        {
            if (IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            if (!string.IsNullOrEmpty(userName) && string.IsNullOrEmpty(password))
            {
                return RedirectToAction("Login");
            }
            var Account = _userManager.GetUser(userName);
            if (Account != null && Account.Enabled && Account.CheckPassword(password))
            {
                _logger.LogInformation("User authenticated: {username}", Account.Username);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, Account.GetIdentity());
                //Redirect back to the source but ensure the Uri is local
                if (!string.IsNullOrEmpty(returnUrl))
                {
                    var baseUrl = new Uri(HttpContext.Request.GetEncodedUrl());
                    if (Uri.TryCreate(baseUrl, returnUrl, out Uri dest))
                    {
                        return Redirect(dest.PathAndQuery);
                    }
                }
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            if (CurrentUser != null)
            {
                _logger.LogInformation("User logout: {username}", CurrentUser.Username);
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
            return (Settings?.PublicRegistration ?? false) ||
                !_userManager.HasUsers ||
                (IsAuthenticated && User.IsInRole(UserRoles.Administrator.ToString()));
        }
    }
}
