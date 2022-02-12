using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using YtStream.Models;

namespace YtStream.Controllers
{
    public class AccountController : BaseController
    {
        [Authorize]
        public IActionResult Index()
        {
            return View(CurrentUser);
        }

        [Authorize, HttpPost, ValidateAntiForgeryToken]
        public IActionResult ChangePassword(PasswordChangeModel model)
        {
            if (CurrentUser.CheckPassword(model.OldPassword))
            {
                if (UserManager.Rules.IsComplexPassword(model.NewPassword))
                {
                    if (model.NewPassword == model.OldPassword)
                    {
                        CurrentUser.SetPassword(model.NewPassword);
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
            return View("Index", CurrentUser);
        }

        [Authorize, HttpPost, ValidateAntiForgeryToken]
        public IActionResult DeleteKey(Guid Key)
        {
            if (Key != Guid.Empty)
            {
                CurrentUser.RemoveKey(Key);
                UserManager.Save();
            }
            return RedirectToAction("ManageKeys");
        }

        [Authorize, HttpPost, ValidateAntiForgeryToken]
        public IActionResult CreateKey(string KeyName)
        {
            if (!string.IsNullOrEmpty(KeyName))
            {
                CurrentUser.AddKey(new UserApiKey() { Name = KeyName });
                UserManager.Save();
            }
            return RedirectToAction("ManageKeys");
        }

        [Authorize]
        public IActionResult ManageKeys()
        {
            return View();
        }

        [HttpPost, ActionName("Register"), ValidateAntiForgeryToken]
        public IActionResult RegisterPost(string userName, string password, string passwordRepeat)
        {
            if (password == passwordRepeat)
            {
                try
                {
                    UserManager.AddUser(userName, password, UserManager.HasUsers ? UserRoles.User : UserRoles.User | UserRoles.Administrator);
                }
                catch (Exception ex)
                {
                    ViewBag.ErrMsg = ex.Message;
                    return View();
                }
                return RedirectToAction("Login");
            }
            else
            {
                ViewBag.ErrMsg = "Passwords do not match";
            }
            return View();
        }

        [HttpGet, ActionName("Register")]
        public IActionResult RegisterGet()
        {
            if ((User.Identity.IsAuthenticated && User.IsInRole(UserRoles.Administrator.ToString())) || !UserManager.HasUsers)
            {
                return View();
            }
            return RedirectToAction("Login");
        }

        [HttpGet, ActionName("Login")]
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

        [HttpPost, ActionName("Login"), ValidateAntiForgeryToken]
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
                //Create the identity for the user  
                var identity = new ClaimsIdentity(new[] {
                    new Claim(ClaimTypes.Name, userName)
                }, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);
                foreach (var Role in Account.GetRoleStrings())
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, Role));
                }
                //identity.AddClaim(new Claim(ClaimTypes.Role, string.Join(",", User.GetRoleStrings())));

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
