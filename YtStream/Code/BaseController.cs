using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Extensions;
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
        /// This is set on the request made after
        /// <see cref="RedirectWithMessage(string?, string, bool)"/> or an overload.
        /// The message is automatically shown by the general layout razor page</remarks>
        public string? CookieMessage { get; private set; }

        /// <summary>
        /// Gets if the cookie message indicates a success, or an error
        /// </summary>
        public bool CookieMessageSuccess { get; private set; }

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
            SetViewBagValues();
            if (!string.IsNullOrEmpty(CookieMessage))
            {
                HttpContext.Response.Cookies.Delete("status");
                HttpContext.Response.Cookies.Delete("status.success");
            }
            await base.OnActionExecutionAsync(context, next);
        }

        /// <summary>
        /// Early request termination function to be used from within
        /// <see cref="OnActionExecutionAsync(ActionExecutingContext, ActionExecutionDelegate)"/>
        /// </summary>
        /// <param name="context">Action context</param>
        /// <param name="statusCode">HTTP status code</param>
        /// <param name="message">Status message</param>
        protected void EarlyTermination(ActionExecutingContext context, int statusCode, string message)
        {
            SetViewBagValues();
            SetMessage(message, false);
            context.HttpContext.Response.StatusCode = statusCode;
            context.Result = View("Error", new ErrorViewModel() { Status = 403 });
        }

        /// <summary>
        /// Sets the minimum required viewbag values to display any page
        /// </summary>
        private void SetViewBagValues()
        {
            ViewBag.User = CurrentUser;
            ViewBag.Settings = Settings;
            ViewBag.CookieMessage = CookieMessage = HttpContext.Request.Cookies["status"];
            ViewBag.CookieMessageSuccess = CookieMessageSuccess = HttpContext.Request.Cookies["status.success"] == "y";
        }

        /// <summary>
        /// Sets <see cref="CurrentUser"/> by API key.
        /// Can also be used if the property has already a value set
        /// </summary>
        /// <param name="apiKey">API key</param>
        /// <remarks>If the user is not found, CurrentUser will be set to null</remarks>
        [MemberNotNullWhen(true, nameof(CurrentUser))]
        protected bool SetApiUser(Guid apiKey)
        {
            CurrentUser = _userManager.GetUser(apiKey, false);
            return CurrentUser != null;
        }

        [MemberNotNullWhen(true, nameof(CurrentUser))]
        protected bool SetUser(string? username)
        {
            CurrentUser = _userManager.GetUser(username);
            return CurrentUser != null;
        }

        /// <summary>
        /// Sets the message to be shown on the layout view
        /// without reloading the website
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="isSuccess">Success or error</param>
        protected void SetMessage(string message, bool isSuccess)
        {
            ViewBag.CookieMessage = CookieMessage = message;
            ViewBag.CookieMessageSuccess = CookieMessageSuccess = isSuccess;
        }

        protected IActionResult RedirectWithMessage(string? action, string message, bool isSuccess)
        {
            return RedirectWithMessage(action, message, isSuccess, null);
        }

        protected IActionResult RedirectWithMessage(string? action, string? controller, string message, bool isSuccess)
        {
            return RedirectWithMessage(action, controller, message, isSuccess, null);
        }

        /// <summary>
        /// Redirects the user and temporarily stores a message that can be retrieved on the next request
        /// </summary>
        /// <param name="action">Redirection action</param>
        /// <param name="message">Message to store</param>
        /// <param name="isSuccess">Success or error</param>
        /// <param name="routeData">Additional route data</param>
        /// <returns><see cref="RedirectToActionResult"/></returns>
        protected IActionResult RedirectWithMessage(string? action, string message, bool isSuccess, object? routeData)
        {
            HttpContext.Response.Cookies.Append("status", message);
            HttpContext.Response.Cookies.Append("status.success", isSuccess ? "y" : "n");
            return RedirectToAction(action, routeData);
        }

        /// <summary>
        /// Redirects the user and temporarily stores a message that can be retrieved on the next request
        /// </summary>
        /// <param name="action">Redirection action</param>
        /// <param name="controller">Controller that holds the <paramref name="action"/></param>
        /// <param name="message">Message to store</param>
        /// <param name="isSuccess">Success or error</param>
        /// <param name="routeData">Additional route data</param>
        /// <returns><see cref="RedirectToActionResult"/></returns>
        protected IActionResult RedirectWithMessage(string? action, string? controller, string message, bool isSuccess, object? routeData)
        {
            HttpContext.Response.Cookies.Append("status", message);
            HttpContext.Response.Cookies.Append("status.success", isSuccess ? "y" : "n");
            return RedirectToAction(action, controller, routeData);
        }

        /// <summary>
        /// Redirect to login page, and preserve current URL
        /// </summary>
        /// <returns>Redirection to /Account/Login</returns>
        protected RedirectToActionResult RequestLogin()
        {
            return RedirectToAction("Login", "Account", new { returnUrl = HttpContext.Request.GetEncodedPathAndQuery() });
        }

        [MemberNotNull(nameof(Settings))]
        protected void RequireSettings()
        {
            if (Settings == null)
            {
                throw new InvalidOperationException("Settings object has not been initialized because the application is not configured or the settings are corrupt.");
            }
        }

        protected bool IsHead()
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
