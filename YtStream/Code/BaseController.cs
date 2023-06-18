using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using YtStream.Models;
using YtStream.Models.Accounts;
using YtStream.Services;
using YtStream.Services.Accounts;

namespace YtStream
{
    /// <summary>
    /// Represents the base controller in use by all controllers of YtStream
    /// </summary>
    public class BaseController : Controller
    {
        protected readonly UserManagerService _userManager;

        /// <summary>
        /// Current system configuration
        /// </summary>
        public ConfigModel? Settings { get; }

        /// <summary>
        /// Currently logged on user
        /// </summary>
        /// <remarks>This is null if not logged on</remarks>
        public AccountInfoModel? CurrentUser { get; private set; }

        /// <summary>
        /// Gets the cookie message
        /// </summary>
        /// <remarks>
        /// Automatically cleared on every request.
        /// This is set on the request made after <see cref="RedirectWithMessage"/> or an overload.
        /// The message is automatically shown by the general layout razor page</remarks>
        public string? CookieMessage { get; private set; }

        /// <summary>
        /// Gets if the current user is authenticated
        /// </summary>
        public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

        /// <summary>
        /// Basic initialization
        /// </summary>
        public BaseController(ConfigService config, UserManagerService userManager)
        {
            _userManager = userManager;
            try
            {
                Settings = config.GetConfiguration();
            }
            catch
            {
                Settings = null;
            }
        }

        /// <summary>
        /// Fills internal properties and handles cookie message
        /// </summary>
        /// <param name="context">Request context</param>
        /// <param name="next">Next function</param>
        /// <returns>Task</returns>
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            CookieMessage = null;
            if (IsAuthenticated)
            {
                CurrentUser = _userManager.GetUser(User.Identity?.Name);
                //Terminate user session if the user is no longer existing or enabled
                if (CurrentUser == null || !CurrentUser.Enabled)
                {
                    await HttpContext.SignOutAsync();
                    context.Result = RedirectToAction("Index", "Home");
                    return;
                }
            }
            ViewBag.User = CurrentUser;
            ViewBag.Settings = Settings;
            ViewBag.CookieMessage = CookieMessage = HttpContext.Request.Cookies["status"];
            if (!string.IsNullOrEmpty(CookieMessage))
            {
                HttpContext.Response.Cookies.Delete("status");
            }
            await base.OnActionExecutionAsync(context, next);
        }

        /// <summary>
        /// Sets <see cref="CurrentUser"/> by API key.
        /// Can also be used if the property has already a value set
        /// </summary>
        /// <param name="ApiKey">API key</param>
        /// <remarks>If the user is not found, CurrentUser will be set to null</remarks>
        public void SetApiUser(Guid ApiKey)
        {
            CurrentUser = _userManager.GetUser(ApiKey);
        }

        /// <summary>
        /// Redirects the user and temporarily stores a message that can be retrieved on the next request
        /// </summary>
        /// <param name="Action">Redirection action</param>
        /// <param name="Message">Message to store</param>
        /// <param name="RouteData">Additional route data</param>
        /// <returns><see cref="RedirectToActionResult"/></returns>
        public IActionResult RedirectWithMessage(string? Action, string Message, object? RouteData = null)
        {
            HttpContext.Response.Cookies.Append("status", Message);
            return RedirectToAction(Action, RouteData);
        }

        /// <summary>
        /// Redirects the user and temporarily stores a message that can be retrieved on the next request
        /// </summary>
        /// <param name="Action">Redirection action</param>
        /// <param name="Controller">Controller that holds the <paramref name="Action"/></param>
        /// <param name="Message">Message to store</param>
        /// <param name="RouteData">Additional route data</param>
        /// <returns><see cref="RedirectToActionResult"/></returns>
        public IActionResult RedirectWithMessage(string? Action, string? Controller, string Message, object? RouteData = null)
        {
            HttpContext.Response.Cookies.Append("status", Message);
            return RedirectToAction(Action, Controller, RouteData);
        }

        [MemberNotNull(nameof(Settings))]
        public void RequireSettings()
        {
            if (Settings == null)
            {
                throw new InvalidOperationException("Settings object has not been initialized because the application is not configured or the settings are corrupt.");
            }
        }

        internal bool IsHead()
        {
            return HttpContext.Request.Method.ToUpper() == "HEAD";
        }

        /// <summary>
        /// Generates an error view with the supplied exception
        /// </summary>
        /// <param name="ex">Exception</param>
        /// <returns>Error view</returns>
        protected IActionResult Error(Exception ex)
        {
            if (HttpContext.Response.StatusCode < 400)
            {
                HttpContext.Response.StatusCode = 500;
            }
            return View("Error", new ErrorViewModel(ex));
        }
    }
}
